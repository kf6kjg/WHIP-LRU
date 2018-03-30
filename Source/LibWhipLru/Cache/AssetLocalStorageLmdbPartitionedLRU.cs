// AssetLocalStorageLmdbPartitionedLRU.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2018
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
using System.IO;
using System.Linq;
using System.Threading;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LightningDB;

namespace LibWhipLru.Cache {
	/// <summary>
	/// Local storage using the LMDB data storage backend for speed, reliability, and minimal overhead in CPU, memory, or disk space.
	/// Note that an LMDB file only ever grows in size, even across purges - just space internally is opened up. Please read about LMDB to learn why.
	/// </summary>
	public class AssetLocalStorageLmdbPartitionedLRU : IChattelLocalStorage, IDisposable {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// The mininum recommended setting for the database maximum disk storage.  Any smaller than this and you might have trouble with a single asset.
		/// </summary>
		public static readonly ulong DB_MAX_DISK_BYTES_MIN_RECOMMENDED = uint.MaxValue/*4TB, maximum size of single asset*/;
		private static readonly string DB_NAME; // null to use default DB.

		private readonly ChattelConfiguration _config;

		private readonly ConcurrentDictionary<Guid, StratusAsset> _assetsBeingWritten = new ConcurrentDictionary<Guid, StratusAsset>();

		private readonly ConcurrentDictionary<string, LightningEnvironment> _dbEnvironments = new ConcurrentDictionary<string, LightningEnvironment>();

		private readonly PartitionedTemporalGuidCache _activeIds;
		public IEnumerable<Guid> ActiveIds(string prefix) => _activeIds?.ItemsWithPrefix(prefix);

		private readonly ulong _dbMaxDiskBytes;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Cache.AssetLocalStorageLmdbPartitioned"/> class specified to be limited to the given amount of disk space.
		/// It's highly recommended to set the disk space limit to a multiple of the block size so that you don't waste space you could be using.
		/// </summary>
		/// <param name="config">ChattelConfiguration object.</param>
		/// <param name="maxAssetLocalStorageDiskSpaceByteCount">Max asset local storage disk space, in bytes.</param>
		public AssetLocalStorageLmdbPartitionedLRU(
			ChattelConfiguration config,
			ulong maxAssetLocalStorageDiskSpaceByteCount,
			TimeSpan partitioningInterval
		) {
			_config = config ?? throw new ArgumentNullException(nameof(config));

			if (maxAssetLocalStorageDiskSpaceByteCount < DB_MAX_DISK_BYTES_MIN_RECOMMENDED) {
				LOG.Warn($"Asset local storage disk space should be able to fit at least one maximum-sized asset, and thus should be at least {uint.MaxValue} bytes.");
			}

			if (maxAssetLocalStorageDiskSpaceByteCount > long.MaxValue) {
				throw new ArgumentOutOfRangeException(nameof(maxAssetLocalStorageDiskSpaceByteCount), $"Asset local storage underlying system doesn't support sizes larger than {long.MaxValue} bytes.");
			}

			_dbMaxDiskBytes = maxAssetLocalStorageDiskSpaceByteCount;

			if (!_config.LocalStorageEnabled) {
				// No local storage? Don't do squat.
				return;
			}

			_activeIds = new PartitionedTemporalGuidCache(
				_config.LocalStorageFolder.FullName,
				partitioningInterval,
				HandleOpenOrCreateEnvironment,
				HandleDeleteEnvironment,
				HandleCopyAsset,
				HandleDbFound
			);

			#region Ctor private functions

			void HandleOpenOrCreateEnvironment(string path) {
				try {
					var env = new LightningEnvironment(path) {
						MapSize = (long)maxAssetLocalStorageDiskSpaceByteCount,
						MaxDatabases = 1,
					};

					env.Open(EnvironmentOpenFlags.None, UnixAccessMode.OwnerRead | UnixAccessMode.OwnerWrite);

					if (!_dbEnvironments.TryAdd(path, env)) {
						env.Dispose();
						LOG.Warn($"Attempted to initialize/open an environment at an already known path: {path}");
					}
				}
				catch (LightningException e) {
					throw new LocalStorageException($"Given path invalid: '{_config.LocalStorageFolder.FullName}'", e);
				}
			}

			Dictionary<Guid, uint> HandleDbFound(string dbPath) {
				LOG.Info($"Restoring index from DB at '{dbPath}'.");
				if (!_dbEnvironments.TryGetValue(dbPath, out var dbEnv)) {
					HandleOpenOrCreateEnvironment(dbPath);
					if (!_dbEnvironments.TryGetValue(dbPath, out var dbEnvRetry)) {
						throw new InvalidOperationException($"Failure to prepare environment before loading DB at '{dbPath}'!");
					}

					dbEnv = dbEnvRetry;
				}

				var foundAssets = new Dictionary<Guid, uint>();

				try {
					using (var tx = dbEnv.BeginTransaction(TransactionBeginFlags.None))
					using (var db = tx.OpenDatabase(DB_NAME, new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
						// Probably not the most effecient way to do this.
						var assetData = tx.CreateCursor(db)
							.Select(kvp => {
								try {
									var assetId = new Guid(kvp.Key);
									return new Tuple<Guid, uint>(assetId, (uint)kvp.Value.Length);
								}
								catch (ArgumentException) {
									return null;
								}
							})
							.Where(assetId => assetId != null)
						;

						foreach (var assetDatum in assetData) {
							foundAssets.Add(assetDatum.Item1, assetDatum.Item2);
						}
					}
				}
				catch (Exception e) {
					throw new LocalStorageException($"Attempting to restore index from db threw an exception!", e);
				}
				LOG.Debug($"Restoring index complete.");

				return foundAssets;
			}

			void HandleDeleteEnvironment(string path) {
				LOG.Debug($"Got request to delete DB environment at '{path}'");
				if (_dbEnvironments.TryRemove(path, out var dbEnv)) {
					dbEnv.Dispose();

					Directory.Delete(path, true);
					LOG.Debug($"Deleted DB environment at '{path}'");
				}
				else {
					LOG.Warn($"Unable to delete unknown DB environment at '{path}'");
				}
			}

			void HandleCopyAsset(Guid assetId, string sourcePath, string destPath) {
				LOG.Debug($"Got request to copy asset {assetId} from DB environment at '{sourcePath}' to '{destPath}'");
				if (_dbEnvironments.TryGetValue(sourcePath, out var dbEnvSource)) {
					if (_dbEnvironments.TryGetValue(destPath, out var dbEnvDest)) {
						try {
							var assetIdBytes = assetId.ToByteArray();

							using (var txS = dbEnvSource.BeginTransaction(TransactionBeginFlags.ReadOnly))
							using (var dbS = txS.OpenDatabase(DB_NAME))
							using (var txD = dbEnvDest.BeginTransaction())
							using (var dbD = txD.OpenDatabase(DB_NAME, new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
								if (txS.TryGet(dbS, assetIdBytes, out byte[] buffer)) {
									txD.Put(dbD, assetIdBytes, buffer);
								}
							}
						}
						catch (LightningException e) {
							throw new LocalStorageException($"Attempting to read locally stored asset with ID {assetId} threw an exception!", e);
						}
						catch (ProtoBuf.ProtoException e) {
							throw new LocalStorageException($"Attempting to deserialize locally stored asset with ID {assetId} threw an exception!", e);
						}

						// Cleaning up after the copy means I allow the disk usage to bounce past the limit,
						// but cleaning up before means that I might wipe the source location before I can read it, as the asset might be in the old location.

						CheckDiskAndCleanup();
					}
					else {
						LOG.Warn($"Unable to find destination DB environment '{destPath}' during copy of asset {assetId}");
					}
				}
				else {
					LOG.Warn($"Unable to find source DB environment '{sourcePath}' during copy of asset {assetId}");
				}
			}

			#endregion
		}

		/// <summary>
		/// Whether or not the ID is known to local storage - might or might not actually be available yet.
		/// </summary>
		/// <returns>If the asset ID is known.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public bool Contains(Guid assetId) {
			return _activeIds?.Contains(assetId) ?? false;
		}

		/// <summary>
		/// Whether or not the asset with that ID was stored in local storage and therefore should be available for reading.
		/// This is a quick memory-based check, not an actual disk hit.
		/// </summary>
		/// <returns>If the asset ID is known and probably available.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public bool AssetWasWrittenToDisk(Guid assetId) {
			return _activeIds?.AssetSize(assetId) > 0;
		}

		/// <summary>
		/// Whether or not the asset with that ID is actually located in local storage by directly querying the storage.
		/// </summary>
		/// <returns>If the asset ID is known and on disk.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public bool AssetOnDisk(Guid assetId) {
			if (!_config.LocalStorageEnabled) {
				return false;
			}

			if (_activeIds.TryGetAssetPartition(assetId, out var dbPath)) {
				if (_dbEnvironments.TryGetValue(dbPath, out var dbEnv)) {
					try {
						using (var tx = dbEnv.BeginTransaction(TransactionBeginFlags.ReadOnly))
						using (var db = tx.OpenDatabase(DB_NAME)) {
							return tx.ContainsKey(db, assetId.ToByteArray());
						}
					}
					catch (LightningException e) {
						throw new LocalStorageException($"Attempting to read locally stored asset with ID {assetId} threw an exception!", e);
					}
				}
			}

			return false;
		}

		#region IChattelLocalStorage

		/// <summary>
		/// Stores the asset in local storage.
		/// </summary>
		/// <param name="asset">Asset to store.</param>
		void IChattelLocalStorage.StoreAsset(StratusAsset asset) {
			asset = asset ?? throw new ArgumentNullException(nameof(asset));

			if (asset.Id == Guid.Empty) {
				throw new ArgumentException("Asset cannot have zero ID.", nameof(asset));
			}

			if (!_config.LocalStorageEnabled) {
				return;
			}

			if (!_assetsBeingWritten.TryAdd(asset.Id, asset)) {
				LOG.Debug($"Attempted to write an asset to local storage, but another thread is already doing so.  Skipping write of {asset.Id} - please report as this shoudln't happen.");
				// Can't add it, which means it's already being written to disk by another thread.  No need to continue.
				// Shouldn't be possible to get here if Chattel's working correctly, so I'm not going to worry about the rapid return timing issue this creates: Chattel has code to handle it.
				return;
			}

			WriteAssetToDisk(asset);

			// Writing is done, remove it from the work list.
			_assetsBeingWritten.TryRemove(asset.Id, out StratusAsset temp);
			LOG.Debug($"Wrote an asset to local storage: {asset.Id}");
		}

		/// <summary>
		/// Purges all items that match the passed filter.
		/// Fields in each filter element are handled in as an AND condition, while sibling filters are handled in an OR condition.
		/// Thus if you wanted to purge all assets that have the temp flag set true OR all assets with the local flag set true, you'd have an array of two filter objects, the first would set the temp flag to true, the second would set the local flag to true.
		/// If instead you wanted to purge all assets that have the temp flag set true AND local flag set true, you'd have an array of a single filter object with both the temp flag and the local flag set to true.
		/// A null or blank list results in all assets being purged.
		/// </summary>
		void IChattelLocalStorage.PurgeAll(IEnumerable<AssetFilter> assetFilter) {
			if (!_config.LocalStorageEnabled) {
				return;
			}

			if (assetFilter == null || !assetFilter.Any()) {
				if (_activeIds.Count > 0) {
					LOG.Warn("Unfiltered purge of all assets called. Proceeding with purge of all locally stored assets!");
					_activeIds.Clear(); // Will call delete on each path.  See HandleDeleteEnvironment.
				}
				else {
					LOG.Info("Unfiltered purge of all assets called, but the DB was already empty.");
				}

				return;
			}

			LOG.Info($"Starting to purge assets that match any one of {assetFilter.Count()} filters...");

			throw new InvalidOperationException("Purging on type not supported.");
		}

		/// <summary>
		/// Purge the specified asset from local storage.
		/// </summary>
		/// <param name="assetId">Asset identifier.</param>
		void IChattelLocalStorage.Purge(Guid assetId) {
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Asset Id should not be empty.", nameof(assetId));
			}

			if (!_config.LocalStorageEnabled) {
				return;
			}

			if (_activeIds.TryRemove(assetId)) {
				// Do nothing to disk: it will fall off the LRU eventually.  Unless the server is restarted, then it'll be restored to fall off later.
			}
			else {
				throw new AssetNotFoundException(assetId);
			}
		}

		/// <summary>
		/// Requests that an asset be fetched from local storage.
		/// </summary>
		/// <returns><c>true</c>, if get asset was found, <c>false</c> otherwise.</returns>
		/// <param name="assetId">Asset identifier.</param>
		/// <param name="asset">The resulting asset.</param>
		bool IChattelLocalStorage.TryGetAsset(Guid assetId, out StratusAsset asset) {
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Empty Id not allowed", nameof(assetId));
			}

			if (!_config.LocalStorageEnabled) {
				asset = null;
				return false;
			}

			if (_assetsBeingWritten.TryGetValue(assetId, out asset)) {
				LOG.Debug($"Attempted to read an asset from local storage, but another thread is writing it. Shortcutting read of {assetId}");
				// Asset is currently being pushed to disk, so might as well return it now since I have it in memory.
				return true;
			}

			try {
				asset = ReadAssetFromDisk(assetId);
				return true;
			}
			catch (Exception e) {
				LOG.Warn($"Unable to read requested asset {assetId}:", e);
			}

			asset = null;
			return false;
		}

		#endregion

		#region Disk IO tools

		private void WriteAssetToDisk(StratusAsset asset) {
			ulong spaceNeeded;

			_activeIds.TryAdd(asset.Id, 0, out var dbPath); // Register the asset as existing, but not yet on disk; size of 0. Failure simply indicates that the asset ID already exists.

			using (var memStream = new MemoryStream()) {
				ProtoBuf.Serializer.Serialize(memStream, asset); // This can throw, but only if something is VERY and irrecoverably wrong.
				spaceNeeded = (ulong)memStream.Length;
				memStream.Position = 0;

				try {
					CheckDiskAndCleanup(spaceNeeded);
				}
				catch (Exception e) {
					LOG.Warn($"Got an exceptions while attempting to clear some space just before writing to disk.", e);
				}

				var buffer = new byte[spaceNeeded];

				Buffer.BlockCopy(memStream.GetBuffer(), 0, buffer, 0, (int)spaceNeeded);

				LightningException lightningException = null;

				if (_dbEnvironments.TryGetValue(dbPath, out var dbEnv)) {
					try {
						using (var tx = dbEnv.BeginTransaction())
						using (var db = tx.OpenDatabase(DB_NAME, new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
							tx.Put(db, asset.Id.ToByteArray(), buffer);
							tx.Commit();
						}

						_activeIds.AssetSize(asset.Id, spaceNeeded); // Set the size now that it's on disk.

						return;
					}
					catch (LightningException e) {
						lightningException = e;
					}
				}

				if (lightningException != null) {
					switch (lightningException.StatusCode) {
						case -30799:
							//LightningDB.Native.Lmdb.MDB_KEYEXIST: Not available in lib ATM...
							// Ignorable.
							LOG.Warn($"{asset.Id} already exists according to local storage. Adding to memory list.", lightningException);
							lightningException = null;

							if (!_activeIds.TryAdd(asset.Id, spaceNeeded, out var dbPathx)) {
								_activeIds.AssetSize(asset.Id, spaceNeeded);
							}

							throw new AssetExistsException(asset.Id);
						default:
							throw new AssetWriteException(asset.Id, lightningException);
					}
				}
			}
		}

		private StratusAsset ReadAssetFromDisk(Guid assetId) {
			if (_activeIds.TryGetAssetPartition(assetId, out var dbPath)) {
				if (_dbEnvironments.TryGetValue(dbPath, out var dbEnv)) {
					try {
						using (var tx = dbEnv.BeginTransaction(TransactionBeginFlags.ReadOnly))
						using (var db = tx.OpenDatabase(DB_NAME)) {
							if (tx.TryGet(db, assetId.ToByteArray(), out byte[] buffer)) {
								using (var stream = new MemoryStream(buffer)) {
									return ProtoBuf.Serializer.Deserialize<StratusAsset>(stream);
								}
							}
						}
					}
					catch (LightningException e) {
						throw new LocalStorageException($"Attempting to read locally stored asset with ID {assetId} threw an exception!", e);
					}
					catch (ProtoBuf.ProtoException e) {
						throw new LocalStorageException($"Attempting to deserialize locally stored asset with ID {assetId} threw an exception!", e);
					}
				}
			}

			throw new LocalStorageException($"Asset with ID {assetId} not found in local storage!");
		}

		private void CheckDiskAndCleanup() {
			CheckDiskAndCleanup(0);
		}

		private void CheckDiskAndCleanup(ulong padding) {
			// Find out how much disk space is being used.
			LOG.Info($"Checking to see if near disk limit...");
			var diskSpaceUsed = padding;
			foreach (var dbPath in _activeIds.PartitionPaths) {
				// I'd be happier I think with the byte count of the blocks consumed.
				diskSpaceUsed += (ulong)Directory.EnumerateFiles(dbPath).Select(file => new FileInfo(file).Length).Aggregate((prev, cur) => prev + cur);
			}

			// If at least 98% (hipshot) full, call remove to get down to at least 90% (another hipshot).
			if ((float)diskSpaceUsed / _dbMaxDiskBytes > 0.98f) {
				LOG.Info($"Disk limit exceeded or near exceeding: using {diskSpaceUsed} of {_dbMaxDiskBytes} bytes.");
				var diskSpaceNeeded = diskSpaceUsed - (ulong)(_dbMaxDiskBytes * 0.90f);
				var removedIds = _activeIds.Remove(diskSpaceNeeded, out var byteCountCleared);
				LOG.Debug($"Removed {removedIds.Count} active IDs, and {byteCountCleared} bytes of disk space.");
			}
		}

		#endregion

		#region IDisposable Support

		protected virtual void Dispose(bool disposing) {
			// Clear out managed objects

			foreach (var dbPath in _dbEnvironments.Keys) {
				_dbEnvironments.TryRemove(dbPath, out var dbEnv);
				dbEnv.Dispose();
			}
		}

		/// <summary>
		/// Releases all resource used by this object.
		/// </summary>
		/// <remarks>Call <see cref="IDisposable.Dispose()"/> when you are finished using the object. The
		/// <see cref="IDisposable.Dispose()"/> method leaves the object in an unusable state. After
		/// calling <see cref="IDisposable.Dispose()"/>, you must release all references to the object
		/// so the garbage collector can reclaim the memory that the object was occupying.</remarks>
		void IDisposable.Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// Uncomment the following line if the finalizer is overridden above.
			GC.SuppressFinalize(this);
		}

		#endregion
	}
}
