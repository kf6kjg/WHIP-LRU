// CacheManager.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LightningDB;
using log4net;

namespace LibWhipLru.Cache {
	public class CacheManager : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly string DEFAULT_DB_FOLDER_PATH = "cache";
		public static readonly ulong DEFAULT_DB_MAX_DISK_BYTES = 1024UL * 1024UL * 1024UL * 1024UL/*1TB*/;
		public static readonly string DEFAULT_WC_FILE_PATH = "whip_lru.whipwcache";
		public static readonly uint DEFAULT_WC_RECORD_COUNT = 1024U * 1024U * 1024U/*1GB*/ / IdWriteCacheNode.BYTE_SIZE;

		private LightningEnvironment _dbenv;
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		private readonly OrderedGuidCache _activeIds;
		private ChattelReader _assetReader;
		private ChattelWriter _assetWriter;

		/// <summary>
		/// The assets failing disk storage.  Used to feed the try-again thread.  However any assets in here have no chance of survival if a crash happens.
		/// </summary>
		private readonly BlockingCollection<StratusAsset> _assetsFailingStorage = new BlockingCollection<StratusAsset>();
		private readonly Thread _localAssetStoreRetryTask;

		public IEnumerable<Guid> ActiveIds(string prefix) => _activeIds?.ItemsWithPrefix(prefix);

		// Storage for assets that are WIP for remote storage.
		private static readonly byte[] WRITE_CACHE_MAGIC_NUMBER = Encoding.ASCII.GetBytes("WHIPLRU1");
		private string _pathToWriteCacheFile;
		private BlockingCollection<IdWriteCacheNode> _assetsToWriteToRemoteStorage;
		private IdWriteCacheNode[] _writeCacheNodes;
		private IdWriteCacheNode _nextAvailableWriteCacheNode;
		private readonly object _writeCacheNodeLock = new object();
		private readonly Thread _remoteAssetStoreTask;



		// TODO: write a negative cache to store IDs that are failures.  Remember to remove any IDs that wind up being Put.  Don't need to disk-backup this.

		public CacheManager(
			string pathToDatabaseFolder,
			ulong maxAssetCacheDiskSpaceByteCount,
			string pathToWriteCacheFile,
			uint maxWriteCacheRecordCount
		) {
			if (string.IsNullOrWhiteSpace(pathToDatabaseFolder)) {
				throw new ArgumentNullException(nameof(pathToDatabaseFolder), "No database path means no go.");
			}
			try {
				_dbenv = new LightningEnvironment(pathToDatabaseFolder) {
					MapSize = (long)maxAssetCacheDiskSpaceByteCount,
					MaxDatabases = 1,
				};

				_dbenv.Open(EnvironmentOpenFlags.None, UnixAccessMode.OwnerRead | UnixAccessMode.OwnerWrite);
			}
			catch (LightningException e) {
				throw new ArgumentException($"Given path invalid: '{pathToDatabaseFolder}'", nameof(pathToDatabaseFolder), e);
			}

			_activeIds = new OrderedGuidCache();

			_assetReader = null;
			_assetWriter = null;

			_pathToWriteCacheFile = pathToWriteCacheFile;
			_assetsToWriteToRemoteStorage = new BlockingCollection<IdWriteCacheNode>();

			LOG.Info($"Restoring index from DB.");
			try {
				using (var tx = _dbenv.BeginTransaction(TransactionBeginFlags.None))
				using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
					// Probably not the most effecient way to do this.
					var assetData = tx.CreateCursor(db)
						.Select(kvp => {
							var str = Encoding.UTF8.GetString(kvp.Key);
							Guid assetId;
							return Guid.TryParse(str, out assetId) ? new Tuple<Guid, uint>(assetId, (uint)kvp.Value.Length) : null;
						})
						.Where(assetId => assetId != null)
					;

					foreach (var assetDatum in assetData) {
						_activeIds.TryAdd(assetDatum.Item1, assetDatum.Item2);
					}
				}
			}
			catch (Exception e) {
				throw new CacheException($"Attempting to restore index from db threw an exception!", e);
			}
			LOG.Debug($"Restoring index complete.");

			// If the file doesn't exist, create it and zero the needed records.
			if (!File.Exists(pathToWriteCacheFile)) {
				LOG.Info($"Write cache file doesn't exist, creating and formatting file '{pathToWriteCacheFile}'");
				using (var fileStream = new FileStream(pathToWriteCacheFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
					var maxLength = WRITE_CACHE_MAGIC_NUMBER.Length + ((long)maxWriteCacheRecordCount * IdWriteCacheNode.BYTE_SIZE);
					fileStream.SetLength(maxLength);
					// On some FSs the file is all 0s at this point, but it behooves us to make sure of the critical points.

					// Write the header
					fileStream.Seek(0, SeekOrigin.Begin);
					fileStream.Write(WRITE_CACHE_MAGIC_NUMBER, 0, WRITE_CACHE_MAGIC_NUMBER.Length);

					// Make sure to flag all record as available, just in case the FS didn't give us a clean slate.
					for (var offset = (long)WRITE_CACHE_MAGIC_NUMBER.Length; offset < maxLength; offset += IdWriteCacheNode.BYTE_SIZE) {
						fileStream.Seek(offset, SeekOrigin.Begin);
						fileStream.WriteByte(0); // First byte is always the status: 0 means it's an open slot.
					}
				}
				LOG.Debug($"Write cache formatting complete.");
			}

			LOG.Info($"Reading write cache from file '{pathToWriteCacheFile}'");
			using (var mmf = MemoryMappedFile.CreateFromFile(pathToWriteCacheFile, FileMode.Open, "whiplruwritecache")) {
				using (var accessor = mmf.CreateViewAccessor(0, WRITE_CACHE_MAGIC_NUMBER.Length)) {
					var magic_number = new byte[WRITE_CACHE_MAGIC_NUMBER.Length];
					accessor.ReadArray(0, magic_number, 0, WRITE_CACHE_MAGIC_NUMBER.Length);
					if (!magic_number.SequenceEqual(WRITE_CACHE_MAGIC_NUMBER)) {
						throw new ArgumentException($"Magic number mismatch when given path: ", nameof(pathToWriteCacheFile));
					}
				}

				using (var stream = mmf.CreateViewStream()) {
					var offset = 0UL;
					{
						var magic_number = new byte[WRITE_CACHE_MAGIC_NUMBER.Length];
						stream.Read(magic_number, 0, WRITE_CACHE_MAGIC_NUMBER.Length);
						if (!magic_number.SequenceEqual(WRITE_CACHE_MAGIC_NUMBER)) {
							throw new ArgumentException($"Magic number mismatch when given path: ", nameof(pathToWriteCacheFile));
						}
						offset += (ulong)WRITE_CACHE_MAGIC_NUMBER.Length;
					}

					var nodes = new List<IdWriteCacheNode>((int)maxWriteCacheRecordCount);

					var buffer = new byte[IdWriteCacheNode.BYTE_SIZE];

					while (nodes.Count < maxWriteCacheRecordCount) {
						stream.Read(buffer, 0, (int)IdWriteCacheNode.BYTE_SIZE);
						var node = new IdWriteCacheNode(buffer, offset);
						nodes.Add(node);
						offset += IdWriteCacheNode.BYTE_SIZE;

						// If the node isn't available that means it's an ID that still needs to be written to long-term storage.
						if (!node.IsAvailable) {
							_assetsToWriteToRemoteStorage.Add(node);
						}
					}

					_writeCacheNodes = nodes.ToArray();
				}
			}
			LOG.Debug($"Reading write cache complete.");

			// Allows assets that failed first attempt at local or remote storage to be tried again.
			_localAssetStoreRetryTask = new Thread(() => {
				var crashExceptions = new List<Exception>();
				var safeExit = false;

				while (crashExceptions.Count < 10) {
					try {
						var token = _cancellationTokenSource.Token;
						foreach (var asset in _assetsFailingStorage.GetConsumingEnumerable()) {
							if (token.IsCancellationRequested) break;
							LOG.Debug($"Attempting to locally store {asset.Id} again.");
							PutAsset(asset);
							crashExceptions.Clear(); // Success means ignore the past.
						}
						if (token.IsCancellationRequested) {
							safeExit = true;
							break;
						}
					}
					catch (Exception e) {
						if (e is OperationCanceledException) {
							safeExit = true;
							break;
						}

						LOG.Warn($"Unhandled exception in localAssetStoreTask thread. Thread restarting.", e);
						crashExceptions.Add(e);
					}
				}

				if (!safeExit && crashExceptions.Count > 0) {
					LOG.Error("Multiple crashes in localAssetStoreTask thread.", new AggregateException("Multiple crashes in localAssetStoreTask thread.", crashExceptions));
				}
			});

			_localAssetStoreRetryTask.Start();

			// Set up the task for storing the assets to the remote server.
			if (_assetWriter != null) {
				_remoteAssetStoreTask = new Thread(() => {
					var crashExceptions = new List<Exception>();
					var safeExit = false;

					while (crashExceptions.Count < 10) {
						try {
							var token = _cancellationTokenSource.Token;
							foreach (var assetCacheNode in _assetsToWriteToRemoteStorage.GetConsumingEnumerable()) {
								if (token.IsCancellationRequested) break;
								LOG.Debug($"Attempting to remotely store {assetCacheNode.AssetId}.");

								var asset = ReadAssetFromDisk(assetCacheNode.AssetId);

								try {
									_assetWriter.PutAssetSync(asset);
								}
								catch (AssetExistsException) {
									// Ignore these.
									LOG.Info($"Remote server reports that the asset with ID {assetCacheNode.AssetId} already exists.");
								}

								// Clear the byte on disk before clearing in memory.
								using (var mmf = MemoryMappedFile.CreateFromFile(pathToWriteCacheFile, FileMode.Open, "whiplruwritecache")) {
									using (var accessor = mmf.CreateViewAccessor((long)assetCacheNode.FileOffset, IdWriteCacheNode.BYTE_SIZE)) {
										accessor.Write(0, (byte)0);
									}
								}
								assetCacheNode.IsAvailable = true;

								crashExceptions.Clear(); // Success means ignore the past.
							}
							if (token.IsCancellationRequested) {
								safeExit = true;
								break;
							}
						}
						catch (Exception e) {
							if (e is OperationCanceledException) {
								safeExit = true;
								break;
							}

							LOG.Warn($"Unhandled exception in localAssetStoreTask thread. Thread restarting.", e);
							crashExceptions.Add(e);
						}
					}

					if (!safeExit && crashExceptions.Count > 0) {
						LOG.Error("Multiple crashes in localAssetStoreTask thread.", new AggregateException("Multiple crashes in localAssetStoreTask thread.", crashExceptions));
					}
				});

				_remoteAssetStoreTask.Start();
			}
		}

		/// <summary>
		/// Attempts to put the asset into the local disk storage and the remote storage system.
		/// 
		/// Does not ever throw on a valid storage attempt. Any exceptions thrown are bugs to be fixed as they mean data loss.
		/// The exceptions are if the asset cannot be serialized for some inane reason or if you passed an asset with a zero ID.  Either of those cases are considered invalid storage attempts.
		/// </summary>
		/// <param name="asset">The asset to store.</param>
		public void PutAsset(StratusAsset asset) {
			Contract.Requires(asset != null);

			if (asset == null) {
				throw new ArgumentNullException(nameof(asset));
			}

			if (asset.Id == Guid.Empty) {
				throw new ArgumentException("Asset cannot have zero ID.", nameof(asset));
			}

			// Why must this method never throw on a valid asset store attempt? It HAS TO WORK: if an asset fails all forms and attempts to store it and send it upstream it will be lost forever. That makes customers VERY cranky.

			if (!_activeIds.Contains(asset.Id)) { // The asset ID didn't exist in the cache, so let's add it to the local and remote storage.
				// First step: get it in the local disk cache.
				var lightningException = WriteAssetToDisk(asset);

				// If it's safely in local get it on the upload path to remote.
				if (lightningException == null) {
					var writeCacheNode = GetNextAvailableWriteCacheNode();

					writeCacheNode.AssetId = asset.Id;

					// Queue up for remote storage.
					_assetsToWriteToRemoteStorage.Add(writeCacheNode);

					// Write to writecache file. In this way if we crash after this point we can recover.
					try {
						using (var mmf = MemoryMappedFile.CreateFromFile(_pathToWriteCacheFile, FileMode.Open, "whiplruwritecache")) {
							using (var accessor = mmf.CreateViewAccessor((long)writeCacheNode.FileOffset, IdWriteCacheNode.BYTE_SIZE)) {
								var nodeBytes = writeCacheNode.ToByteArray();

								accessor.WriteArray(0, nodeBytes, 0, (int)IdWriteCacheNode.BYTE_SIZE);
							}
						}
					}
					catch (Exception e) {
						LOG.Warn($"Failed to write asset ID {asset.Id} to disk-based write cache!", e);
						// As long as the queue thread processes the asset this should be OK.
					}
				}
				else {
					LOG.Warn($"There was an exception writing asset {asset.Id} to the local DB. Asset has been queued for retry. Termination of WHIP-LRU could result in data loss!", lightningException);
				}
			}
			else {
				LOG.Info($"Dropped store of duplicate asset {asset.Id}");
			}
		}

		/// <summary>
		/// Retrieves the asset. Tries the local cache first, then moves on to the remote storage systems.
		/// If neither could find the data, or if there is no remote storage set up, null is returned.
		/// 
		/// Can throw, but only if there were problems with the remote storage calls or you passed a zero ID.
		/// </summary>
		/// <returns>The asset.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public StratusAsset GetAsset(Guid assetId) {
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Asset ID cannot be zero.", nameof(assetId));
			}

			try {
				return ReadAssetFromDisk(assetId);
			}
			catch (CacheException e) {
				LOG.Debug("Simple cache miss or error in the cache system, see inner exception.", e);
				// Cache miss. Bummer.
			}

			if (_assetReader != null) {
				var asset = _assetReader.GetAssetSync(new OpenMetaverse.UUID(assetId));

				WriteAssetToDisk(asset); // Don't care if this reports a problem.

				return asset;
			}

			return null;
		}

		internal void SetChattelReader(ChattelReader reader) {
			if (_assetWriter != null) {
				throw new CacheException("Cannot change asset reader once initialized.");
			}
			_assetReader = reader;
		}

		internal void SetChattelWriter(ChattelWriter writer) {
			if (_assetWriter != null) {
				throw new CacheException("Cannot change asset writer once initialized.");
			}
			_assetWriter = writer;
		}

		#region Disk IO tools

		private LightningException WriteAssetToDisk(StratusAsset asset) {
			// Remember it's important that this method does not throw exceptions.
			LightningException lightningException;

			ulong spaceNeeded;

			using (var memStream = new MemoryStream()) {
				ProtoBuf.Serializer.Serialize(memStream, asset); // This can throw, but only if something is VERY and irrecoverably wrong.
				spaceNeeded = (ulong)memStream.Length;
				memStream.Position = 0;

				var buffer = new byte[spaceNeeded];

				Buffer.BlockCopy(memStream.GetBuffer(), 0, buffer, 0, (int)spaceNeeded);
				try {
					using (var tx = _dbenv.BeginTransaction())
					using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
						tx.Put(db, Encoding.UTF8.GetBytes(asset.Id.ToString()), buffer);
						tx.Commit();
					}

					_activeIds.TryAdd(asset.Id, spaceNeeded);

					return null;
				}
				catch (LightningException e) {
					lightningException = e;
				}
			}

			switch (lightningException.StatusCode) {
				case -30799:
					//LightningDB.Native.Lmdb.MDB_KEYEXIST: Not available in lib ATM...
					// Ignorable.
					LOG.Info($"According to local storage asset {asset.Id} already exists.", lightningException);
					lightningException = null;
					break;
				case LightningDB.Native.Lmdb.MDB_DBS_FULL:
				case LightningDB.Native.Lmdb.MDB_MAP_FULL:
					LOG.Warn($"Got storage space full during local asset storage for {asset.Id}, clearing some room...", lightningException);

					ulong bytesRemoved;
					var removedAssetIds = _activeIds.Remove(spaceNeeded * 2, out bytesRemoved);

					try {
						using (var tx = _dbenv.BeginTransaction()) {
							foreach (var assetId in removedAssetIds) {
								using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
									tx.Delete(db, Encoding.UTF8.GetBytes(assetId.ToString()));
									tx.Commit();
								}
							}
						}
					}
					catch (LightningException e) {
						LOG.Warn($"Had an exception while attempting to clear some space in the local asset store.", e);
					}

					// Place the asset where we can try it again.
					_assetsFailingStorage.Add(asset);
					break;
				default:
					LOG.Warn($"Got an unexpected exception during local asset storage for {asset.Id}.", lightningException);
					_assetsFailingStorage.Add(asset);
					break;
			}

			return lightningException;
		}

		private StratusAsset ReadAssetFromDisk(Guid assetId) {
			try {
				using (var tx = _dbenv.BeginTransaction(TransactionBeginFlags.ReadOnly))
				using (var db = tx.OpenDatabase("assetstore")) {
					byte[] buffer;
					if (tx.TryGet(db, Encoding.UTF8.GetBytes(assetId.ToString()), out buffer)) {
						using (var stream = new MemoryStream(buffer)) {
							return ProtoBuf.Serializer.Deserialize<StratusAsset>(stream);
						}
					}

					throw new CacheException($"Asset with ID {assetId} not found in local storage!");
				}
			}
			catch (LightningException e) {
				throw new CacheException($"Attempting to read locally stored asset with ID {assetId} threw an exception!", e);
			}
			catch (ProtoBuf.ProtoException e) {
				throw new CacheException($"Attempting to deserialize locally stored asset with ID {assetId} threw an exception!", e);
			}
		}

		private IdWriteCacheNode GetNextAvailableWriteCacheNode() {
			IdWriteCacheNode writeCacheNode;

			lock (_writeCacheNodeLock) {
				// If we've not bootstrapped, do so.
				while (_nextAvailableWriteCacheNode == null) {
					try {
						_nextAvailableWriteCacheNode = _writeCacheNodes.First(node => node.IsAvailable);
					}
					catch (InvalidOperationException) {
						// No available nodes found, which means we are out of ability to safely continue until one becomes available...
						_nextAvailableWriteCacheNode = null;
						Thread.Sleep(100);
					}
				}

				writeCacheNode = _nextAvailableWriteCacheNode;
				writeCacheNode.IsAvailable = false;
				_nextAvailableWriteCacheNode = null;

				// Find the next one.
				while (_nextAvailableWriteCacheNode == null) {
					try {
						_nextAvailableWriteCacheNode = _writeCacheNodes.First(node => node.IsAvailable);
					}
					catch (InvalidOperationException) {
						// No available nodes found, which means we are out of ability to safely continue until one becomes available...
						_nextAvailableWriteCacheNode = null;
						Thread.Sleep(100);
					}
				}
			}

			return writeCacheNode;
		}

		#endregion

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					_dbenv.Dispose();
					_dbenv = null;
					_assetsFailingStorage.CompleteAdding();
					_assetsToWriteToRemoteStorage.CompleteAdding();
					_cancellationTokenSource.Cancel();
					_cancellationTokenSource.Dispose();
				}

				// Free unmanaged resources (unmanaged objects) and override a finalizer below.
				// None at this time.

				// Set large fields to null.
				_cancellationTokenSource = null;
				_writeCacheNodes = null;

				disposedValue = true;
			}
		}

		// Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~CacheManager() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// Uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
