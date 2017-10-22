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
using System.Reflection;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LightningDB;
using log4net;

namespace LibWhipLru.Cache {
	public class CacheManager : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private LightningEnvironment _dbenv;
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		private readonly OrderedGuidCache _activeIds;
		private readonly ChattelReader _assetReader;
		private readonly ChattelWriter _assetWriter;

		/// <summary>
		/// The assets failing disk storage.  Used to feed the try-again thread.  However any assets in here have no chance of survival if a crash happens.
		/// </summary>
		private readonly BlockingCollection<StratusAsset> _assetsFailingStorage = new BlockingCollection<StratusAsset>();
		private readonly Thread _localAssetStoreTask;

		public IEnumerable<Guid> ActiveIds(string prefix) => _activeIds?.ItemsWithPrefix(prefix);

		// TODO: write a negative cache to store IDs that are failures.  Remember to remove any ODs that wind up being Put.  Don't need to disk-backup this.

		// TODO: restore _activeIds from the LMDB on startup.

		// TODO: keep and restore on restart a list of assets that haven't yet been uploaded.

		// BUG: how to sanely handle a PUT of an asset that's NOT in local storage but IS in remote storage?  Asking remote every time is inviting disaster...

		public CacheManager(string pathToDatabaseFolder, float freeDiskSpacePercentage, ChattelReader assetReader, ChattelWriter assetWriter) {
			if (string.IsNullOrWhiteSpace(pathToDatabaseFolder)) {
				throw new ArgumentNullException(nameof(pathToDatabaseFolder), "No database path means no go.");
			}
			if (freeDiskSpacePercentage < 0f || freeDiskSpacePercentage > 1f) {
				throw new ArgumentOutOfRangeException(nameof(freeDiskSpacePercentage), "Must be in range 0.0 to 1.0 inclusive.");
			}
			try {
				_dbenv = new LightningEnvironment(pathToDatabaseFolder);

				_dbenv.Open(EnvironmentOpenFlags.None, UnixAccessMode.OwnerRead | UnixAccessMode.OwnerWrite);
			}
			catch (LightningException e) {
				throw new ArgumentException($"Given path invalid: '{pathToDatabaseFolder}'", nameof(pathToDatabaseFolder), e);
			}

			_activeIds = new OrderedGuidCache();

			_assetReader = assetReader;
			_assetWriter = assetWriter;

			_localAssetStoreTask = new Thread(() => {
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

			_localAssetStoreTask.Start();
		}

		public void PutAsset(StratusAsset asset) {
			Contract.Requires(asset != null);

			if (!_activeIds.Contains(asset.Id)) {
				// The asset ID didn't exist in the cache, so let's add it to the local and remote storage.
				LightningException lightningException = null;

				using (var memStream = new MemoryStream()) {
					ProtoBuf.Serializer.Serialize(memStream, asset);
					memStream.Position = 0;

					try {
						using (var tx = _dbenv.BeginTransaction())
						using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
							tx.Put(db, System.Text.Encoding.UTF8.GetBytes(asset.Id.ToString()), memStream.GetBuffer());
							tx.Commit();
						}
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
							LOG.Info($"According to local storage asset {asset.Id} already exists.", lightningException);
							lightningException = null;
							break;
						case LightningDB.Native.Lmdb.MDB_DBS_FULL:
						case LightningDB.Native.Lmdb.MDB_MAP_FULL:
							LOG.Warn($"Got storage space full during local asset storage for {asset.Id}, clearing some room...", lightningException);

							int bytesRemoved;
							var removedAssetIds = _activeIds.Remove(asset.Data.Length * 3, out bytesRemoved);

							try {
								using (var tx = _dbenv.BeginTransaction()) {
									foreach (var assetId in removedAssetIds) {
										using (var db = tx.OpenDatabase("assetstore", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
											tx.Delete(db, System.Text.Encoding.UTF8.GetBytes(assetId.ToString()));
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
				}

				if (lightningException == null) {
					// TODO: put the asset ID into an output queue.

				}
			}
			else {
				LOG.Info($"Dropped store of duplicate asset {asset.Id}");
			}
		}

		/* TODO
		public StratusAsset GetAsset(UUID assetId) {

		}
		*/

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					_dbenv.Dispose();
					_dbenv = null;
					_assetsFailingStorage.CompleteAdding();
					_cancellationTokenSource.Cancel();
					_cancellationTokenSource.Dispose();
					_cancellationTokenSource = null;
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~CacheManager() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
