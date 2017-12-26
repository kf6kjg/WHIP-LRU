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
		public static readonly uint DEFAULT_NC_LIFETIME_SECONDS = 60 * 2;

		private LightningEnvironment _dbenv;
		private object _dbenv_lock = new object();
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		private readonly OrderedGuidCache _activeIds;
		private ChattelReader _assetReader;
		private ChattelWriter _assetWriter;

		public IEnumerable<Guid> ActiveIds(string prefix) => _activeIds?.ItemsWithPrefix(prefix);

		// Storage for assets that are WIP for remote storage.
		private static readonly byte[] WRITE_CACHE_MAGIC_NUMBER = Encoding.ASCII.GetBytes("WHIPLRU1");
		private string _pathToWriteCacheFile;
		private BlockingCollection<IdWriteCacheNode> _assetsToWriteToRemoteStorage;
		private IdWriteCacheNode[] _writeCacheNodes;
		private IdWriteCacheNode _nextAvailableWriteCacheNode;
		private readonly object _writeCacheNodeLock = new object();
		private readonly Thread _remoteAssetStoreTask;

		/// <summary>
		/// Stores IDs that are failures.  No need to disk backup, it's OK to lose this info in a restart.
		/// </summary>
		private readonly System.Runtime.Caching.ObjectCache _negativeCache;
		private readonly System.Runtime.Caching.CacheItemPolicy _negativeCachePolicy;
		private readonly ReaderWriterLockSlim _negativeCacheLock;

		public CacheManager(
			string pathToDatabaseFolder,
			ulong maxAssetCacheDiskSpaceByteCount,
			string pathToWriteCacheFile,
			uint maxWriteCacheRecordCount,
			TimeSpan negativeCacheItemLifetime
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

			if (negativeCacheItemLifetime.TotalSeconds > 0) {
				_negativeCache = System.Runtime.Caching.MemoryCache.Default;
				_negativeCacheLock = new ReaderWriterLockSlim();

				_negativeCachePolicy = new System.Runtime.Caching.CacheItemPolicy {
					SlidingExpiration = negativeCacheItemLifetime,
				};
			}

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

			// Set up the task for storing the assets to the remote server.
			if (_assetWriter != null && _assetWriter.HasUpstream) {
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
								using (var mmf = MemoryMappedFile.CreateFromFile(pathToWriteCacheFile, FileMode.Open, "whiplruwritecache"))
								using (var accessor = mmf.CreateViewAccessor((long)assetCacheNode.FileOffset, IdWriteCacheNode.BYTE_SIZE)) {
									accessor.Write(0, (byte)0);
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
		public PutResult PutAsset(StratusAsset asset) {
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

				if (_negativeCache != null) {
					_negativeCacheLock.EnterWriteLock();
					try {
						_negativeCache.Remove(asset.Id.ToString("N"));
					}
					finally {
						_negativeCacheLock.ExitWriteLock();
					}
				}

				// If it's safely in local get it on the upload path to remote.
				if (lightningException == null) {
					if (_assetWriter != null && _assetWriter.HasUpstream) { // If there's no asset writer to send to then there's no point in trying to store against a write failure.
						var writeCacheNode = GetNextAvailableWriteCacheNode();

						writeCacheNode.AssetId = asset.Id;

						// Queue up for remote storage.
						_assetsToWriteToRemoteStorage.Add(writeCacheNode);

						// Write to writecache file. In this way if we crash after this point we can recover.
						try {
							using (var mmf = MemoryMappedFile.CreateFromFile(_pathToWriteCacheFile, FileMode.Open, "whiplruwritecache"))
							using (var accessor = mmf.CreateViewAccessor((long)writeCacheNode.FileOffset, IdWriteCacheNode.BYTE_SIZE)) {
								var nodeBytes = writeCacheNode.ToByteArray();

								accessor.WriteArray(0, nodeBytes, 0, (int)IdWriteCacheNode.BYTE_SIZE);
							}
						}
						catch (Exception e) {
							LOG.Warn($"{asset.Id} failed to write to disk-based write cache!", e);
							// As long as the queue thread processes the asset this should be OK.
							return PutResult.WIP;
						}
					}
				}
				else {
					// Only known cause of an exception not causing an immediate retry is the case of already exists. And that already logs the details.
					return PutResult.DUPLICATE;
				}
			}
			else {
				LOG.Info($"{asset.Id} was rejected from storage as a duplicate.");
				return PutResult.DUPLICATE;
			}

			return PutResult.DONE;
		}

		/// <summary>
		/// Retrieves the asset. Tries the local cache first, then moves on to the remote storage systems.
		/// If neither could find the data, or if there is no remote storage set up, null is returned.
		/// 
		/// Can throw, but only if there were problems with the remote storage calls or you passed a zero ID.
		/// </summary>
		/// <returns>The asset.</returns>
		/// <param name="assetId">Asset identifier.</param>
		/// <param name="cacheResult">Specifies to locally store the asset if it was fetched from a remote.</param>
		public StratusAsset GetAsset(Guid assetId, bool cacheResult = true) {
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Asset ID cannot be zero.", nameof(assetId));
			}

			if (_negativeCache != null) {
				_negativeCacheLock.EnterReadLock();
				try {
					if (_negativeCache.Contains(assetId.ToString("N"))) {
						return null;
					}
				}
				finally {
					_negativeCacheLock.ExitReadLock();
				}
			}

			var assetSize = _activeIds?.AssetSize(assetId);

			if (assetSize != null) { // Asset exists, just might not be on disk yet.
				if (assetSize <= 0) {
					// Wait here until the asset makes it to disk.
					SpinWait.SpinUntil(() => _activeIds?.AssetSize(assetId) > 0);
				}

				try {
					return ReadAssetFromDisk(assetId);
				}
				catch (CacheException e) {
					LOG.Debug("Error in the cache system, see inner exception.", e);
					// Cache miss. Bummer.
				}
			}

			if (_assetReader != null && _assetReader.HasUpstream) {
				var asset = _assetReader.GetAssetSync(assetId);

				if (cacheResult) {
					WriteAssetToDisk(asset); // Don't care if this reports a problem.
				}

				return asset;
			}

			if (_negativeCache != null) {
				_negativeCacheLock.EnterWriteLock();
				try {
					_negativeCache.Set(new System.Runtime.Caching.CacheItem(assetId.ToString("N"), 0), _negativeCachePolicy);
				}
				finally {
					_negativeCacheLock.ExitWriteLock();
				}
			}

			return null;
		}

		/// <summary>
		/// Attempts to verify if the asset is known or not. Tries the local cache first, then moves on to the remote storage systems.
		/// 
		/// Can throw, but only if there were problems with the remote calls or you passed a zero ID.
		/// </summary>
		/// <returns>Whether the asset was found or not.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public bool CheckAsset(Guid assetId) {
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Asset ID cannot be zero.", nameof(assetId));
			}

			if (_negativeCache != null) {
				_negativeCacheLock.EnterReadLock();
				try {
					if (_negativeCache.Contains(assetId.ToString("N"))) {
						return false;
					}
				}
				finally {
					_negativeCacheLock.ExitReadLock();
				}
			}

			if (_activeIds?.Contains(assetId) ?? false) {
				return true;
			}

			if (_assetReader != null && _assetReader.HasUpstream) {
				var asset = _assetReader.GetAssetSync(assetId);

				WriteAssetToDisk(asset); // Don't care if this reports a problem.

				if (asset != null) {
					return true;
				}
			}

			if (_negativeCache != null) {
				_negativeCacheLock.EnterWriteLock();
				try {
					_negativeCache.Set(new System.Runtime.Caching.CacheItem(assetId.ToString("N"), 0), _negativeCachePolicy);
				}
				finally {
					_negativeCacheLock.ExitWriteLock();
				}
			}

			return false;
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
			if (asset == null) {
				return null;
			}

			LightningException lightningException;

			ulong spaceNeeded;

			_activeIds.TryAdd(asset.Id, 0); // Register the asset as existing, but not yet on disk; size of 0. Failure simply indicates that the asset ID already exists.

			using (var memStream = new MemoryStream()) {
				ProtoBuf.Serializer.Serialize(memStream, asset); // This can throw, but only if something is VERY and irrecoverably wrong.
				spaceNeeded = (ulong)memStream.Length;
				memStream.Position = 0;

				var buffer = new byte[spaceNeeded];

				Buffer.BlockCopy(memStream.GetBuffer(), 0, buffer, 0, (int)spaceNeeded);
				retryStorageLabel:
				try {
					using (var tx = _dbenv.BeginTransaction())
					using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
						tx.Put(db, Encoding.UTF8.GetBytes(asset.Id.ToString("N")), buffer);
						tx.Commit();
					}

					_activeIds.AssetSize(asset.Id, spaceNeeded); // Set the size now that it's on disk.

					return null;
				}
				catch (LightningException e) {
					lightningException = e;
				}

				switch (lightningException.StatusCode) {
					case -30799:
						//LightningDB.Native.Lmdb.MDB_KEYEXIST: Not available in lib ATM...
						// Ignorable.
						LOG.Warn($"{asset.Id} already exists according to local storage. Adding to memory list - please report this as it should not be able to happen.", lightningException);
						lightningException = null;

						_activeIds.AssetSize(asset.Id, spaceNeeded); // Set the size now that it's on disk.

						break;
					case LightningDB.Native.Lmdb.MDB_DBS_FULL:
					case LightningDB.Native.Lmdb.MDB_MAP_FULL:
						var lockTaken = Monitor.TryEnter(_dbenv_lock);
						try {
							if (lockTaken) {
								LOG.Warn($"{asset.Id} got storage space full during local storage, clearing some room...", lightningException);

								ulong bytesRemoved;
								var removedAssetIds = _activeIds.Remove(spaceNeeded * 2, out bytesRemoved);

								try {
									using (var tx = _dbenv.BeginTransaction())
									using (var db = tx.OpenDatabase("assetstore")) {
										foreach (var assetId in removedAssetIds) {
											tx.Delete(db, Encoding.UTF8.GetBytes(assetId.ToString("N")));
										}
										tx.Commit();
									}
								}
								catch (LightningException e) {
									LOG.Warn($"{asset.Id} had an exception while attempting to clear some space in the local asset store.", e);
								}
							}
							// else skip as another thread is already clearing some space.
						}
						finally {
							if (lockTaken) Monitor.Exit(_dbenv_lock);
						}

						// Retry the asset storage now that we've got some space.
						goto retryStorageLabel;
					default:
						LOG.Warn($"{asset.Id} got an unexpected exception during local storage.", lightningException);

						Thread.Sleep(200); // Give it some time. The time is a hipshot, not some magic numebr.
						goto retryStorageLabel;
				}
			}

			return lightningException;
		}

		private StratusAsset ReadAssetFromDisk(Guid assetId) {
			try {
				using (var tx = _dbenv.BeginTransaction(TransactionBeginFlags.ReadOnly))
				using (var db = tx.OpenDatabase("assetstore")) {
					byte[] buffer;
					if (tx.TryGet(db, Encoding.UTF8.GetBytes(assetId.ToString("N")), out buffer)) {
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

		public enum PutResult {
			DONE,
			DUPLICATE,
			WIP,
		}

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					_assetsToWriteToRemoteStorage.CompleteAdding();
					_cancellationTokenSource.Cancel();
					_cancellationTokenSource.Dispose();

					Thread.Sleep(500);
					_dbenv.Dispose();
					_dbenv = null;
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
