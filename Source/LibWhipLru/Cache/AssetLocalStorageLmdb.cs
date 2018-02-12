// AssetLocalStorageLmdb.cs
//
// Author:
//       Ricky C <>
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
using System.Text;
using System.Threading;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LightningDB;

namespace LibWhipLru.Cache {
	/// <summary>
	/// Local storage using the LMDB data storage backend for speed, reliability, and minimal overhead in CPU, memory, or disk space.
	/// Note that an LMDB file only ever grows in size, even across purges - just space internally is opened up. Please read about LMDB to learn why.
	/// </summary>
	public class AssetLocalStorageLmdb : IChattelLocalStorage, IDisposable {
		public static readonly ulong DEFAULT_DB_MAX_DISK_BYTES = uint.MaxValue/*4TB, maximum size of single asset*/;

		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly ChattelConfiguration _config;

		private readonly ConcurrentDictionary<Guid, StratusAsset> _assetsBeingWritten = new ConcurrentDictionary<Guid, StratusAsset>();

		private LightningEnvironment _dbenv;
		private object _dbenv_lock = new object();
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		private readonly OrderedGuidCache _activeIds;
		public IEnumerable<Guid> ActiveIds(string prefix) => _activeIds?.ItemsWithPrefix(prefix);

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Cache.AssetLocalStorageLmdb"/> class specified to be limited to the given amount of disk space.
		/// It's highly recommended to set the disk space limit to a multiple of the block size so that you don't waste space you could be using.
		/// </summary>
		/// <param name="config">ChattelConfiguration object.</param>
		/// <param name="maxAssetLocalStorageDiskSpaceByteCount">Max asset local storage disk space, in bytes.</param>
		public AssetLocalStorageLmdb(
			ChattelConfiguration config,
			ulong maxAssetLocalStorageDiskSpaceByteCount
		) {
			_config = config ?? throw new ArgumentNullException(nameof(config));

			if (maxAssetLocalStorageDiskSpaceByteCount < uint.MaxValue) {
				throw new ArgumentOutOfRangeException(nameof(maxAssetLocalStorageDiskSpaceByteCount), $"Asset local storage disk space should be able to fit at least one maximum-sized asset, and thus should be at least {uint.MaxValue} bytes.");
			}

			if (maxAssetLocalStorageDiskSpaceByteCount > long.MaxValue) {
				throw new ArgumentOutOfRangeException(nameof(maxAssetLocalStorageDiskSpaceByteCount), $"Asset local storage underlying system doesn't support sizes larger than {long.MaxValue} bytes.");
			}

			if (!_config.LocalStorageEnabled) {
				// No local storage? Don't do squat.
				return;
			}

			try {
				_dbenv = new LightningEnvironment(_config.LocalStorageFolder.FullName) {
					MapSize = (long)maxAssetLocalStorageDiskSpaceByteCount,
					MaxDatabases = 1,
				};

				_dbenv.Open(EnvironmentOpenFlags.None, UnixAccessMode.OwnerRead | UnixAccessMode.OwnerWrite);
			}
			catch (LightningException e) {
				throw new LocalStorageException($"Given path invalid: '{_config.LocalStorageFolder.FullName}'", e);
			}

			_activeIds = new OrderedGuidCache();

			LOG.Info($"Restoring index from DB.");
			try {
				using (var tx = _dbenv.BeginTransaction(TransactionBeginFlags.None))
				using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
					// Probably not the most effecient way to do this.
					var assetData = tx.CreateCursor(db)
						.Select(kvp => {
							var str = Encoding.UTF8.GetString(kvp.Key);
							return Guid.TryParse(str, out Guid assetId) ? new Tuple<Guid, uint>(assetId, (uint)kvp.Value.Length) : null;
						})
						.Where(assetId => assetId != null)
					;

					foreach (var assetDatum in assetData) {
						_activeIds.TryAdd(assetDatum.Item1, assetDatum.Item2);
					}
				}
			}
			catch (Exception e) {
				throw new LocalStorageException($"Attempting to restore index from db threw an exception!", e);
			}
			LOG.Debug($"Restoring index complete.");
		}

		/// <summary>
		/// Whether or not the ID is known to local storage - might or might not actually be available yet.
		/// </summary>
		/// <returns>If the asset ID is known.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public bool Contains(Guid assetId) {
			return _activeIds.Contains(assetId);
		}

		/// <summary>
		/// Whether or not the asset with that ID is stored in local storage and available for reading.
		/// </summary>
		/// <returns>If the asset ID is known and available.</returns>
		/// <param name="assetId">Asset identifier.</param>
		public bool AssetOnDisk(Guid assetId) {
			return _activeIds.AssetSize(assetId) > 0;
		}

		#region IChattelLocalStorage

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

		void IChattelLocalStorage.PurgeAll(IEnumerable<AssetFilter> assetFilter) {
			throw new NotImplementedException();
		}

		void IChattelLocalStorage.Purge(Guid assetId) {
			throw new NotImplementedException();
		}

		bool IChattelLocalStorage.TryGetAsset(Guid assetId, out StratusAsset asset) {
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

			_activeIds.TryAdd(asset.Id, 0); // Register the asset as existing, but not yet on disk; size of 0. Failure simply indicates that the asset ID already exists.

			using (var memStream = new MemoryStream()) {
				ProtoBuf.Serializer.Serialize(memStream, asset); // This can throw, but only if something is VERY and irrecoverably wrong.
				spaceNeeded = (ulong)memStream.Length;
				memStream.Position = 0;

				var buffer = new byte[spaceNeeded];

				Buffer.BlockCopy(memStream.GetBuffer(), 0, buffer, 0, (int)spaceNeeded);
			retryStorageLabel:
				LightningException lightningException = null;
				try {
					using (var tx = _dbenv.BeginTransaction())
					using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
						tx.Put(db, Encoding.UTF8.GetBytes(asset.Id.ToString("N")), buffer);
						tx.Commit();
					}

					_activeIds.AssetSize(asset.Id, spaceNeeded); // Set the size now that it's on disk.

					return;
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

						if (!_activeIds.TryAdd(asset.Id, spaceNeeded)) {
							_activeIds.AssetSize(asset.Id, spaceNeeded);
						}

						throw new AssetExistsException(asset.Id);
					case LightningDB.Native.Lmdb.MDB_DBS_FULL:
					case LightningDB.Native.Lmdb.MDB_MAP_FULL:
						var lockTaken = Monitor.TryEnter(_dbenv_lock);
						try {
							if (lockTaken) {
								LOG.Warn($"{asset.Id} got storage space full during local storage, clearing some room...", lightningException);

								var removedAssetIds = _activeIds.Remove(spaceNeeded * 2, out ulong bytesRemoved);

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
		}

		private StratusAsset ReadAssetFromDisk(Guid assetId) {
			try {
				using (var tx = _dbenv.BeginTransaction(TransactionBeginFlags.ReadOnly))
				using (var db = tx.OpenDatabase("assetstore")) {
					if (tx.TryGet(db, Encoding.UTF8.GetBytes(assetId.ToString("N")), out byte[] buffer)) {
						using (var stream = new MemoryStream(buffer)) {
							return ProtoBuf.Serializer.Deserialize<StratusAsset>(stream);
						}
					}

					throw new LocalStorageException($"Asset with ID {assetId} not found in local storage!");
				}
			}
			catch (LightningException e) {
				throw new LocalStorageException($"Attempting to read locally stored asset with ID {assetId} threw an exception!", e);
			}
			catch (ProtoBuf.ProtoException e) {
				throw new LocalStorageException($"Attempting to deserialize locally stored asset with ID {assetId} threw an exception!", e);
			}
		}

		#endregion

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				//if (disposing) {
				//}

				// Free unmanaged resources (unmanaged objects) and override a finalizer below.
				_dbenv?.Dispose();
				_dbenv = null;

				disposedValue = true;
			}
		}

		// Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		~AssetLocalStorageLmdb() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(false);
		}

		// This code added to correctly implement the disposable pattern.
		void IDisposable.Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// Uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
