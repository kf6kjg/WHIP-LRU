// StorageManager.cs
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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using log4net;

namespace LibWhipLru.Cache {
	/// <summary>
	/// Controls the coming and going of all assets through WHIP-LRU so that the memory caching and other features run smoothly.
	/// </summary>
	public class StorageManager {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly uint DEFAULT_NC_LIFETIME_SECONDS = 60 * 2;

		public delegate void SuccessCallback(StratusAsset asset);
		public delegate void FailureCallback();
		public delegate void FoundCallback(bool found);
		public delegate void StorageResultCallback(PutResult result);
		public delegate void PurgeResultCallback(PurgeResult result);

		private readonly AssetLocalStorageLmdb _localStorage;
		private ChattelReader _assetReader;
		private ChattelWriter _assetWriter;

		/// <summary>
		/// Stores IDs that are failures.  No need to disk backup, it's OK to lose this info in a restart.
		/// </summary>
		private readonly System.Runtime.Caching.ObjectCache _negativeCache;
		private readonly System.Runtime.Caching.CacheItemPolicy _negativeCachePolicy;
		private readonly ReaderWriterLockSlim _negativeCacheLock;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Cache.StorageManager"/> class.
		/// A zero or negative value for the negativeCacheItemLifetime results in the negative cache being disabled.
		/// </summary>
		/// <param name="localStorage">Local storage for assets.</param>
		/// <param name="negativeCacheItemLifetime">Negative cache item lifetime.</param>
		/// <param name="reader">Reader.</param>
		/// <param name="writer">Writer.</param>
		public StorageManager(
			AssetLocalStorageLmdb localStorage,
			TimeSpan negativeCacheItemLifetime,
			ChattelReader reader,
			ChattelWriter writer
		) {
			_localStorage = localStorage ?? throw new ArgumentNullException(nameof(localStorage));
			_assetReader = reader ?? throw new ArgumentNullException(nameof(reader));
			_assetWriter = writer ?? throw new ArgumentNullException(nameof(writer));

			if (negativeCacheItemLifetime.TotalSeconds > 0) {
				_negativeCache = System.Runtime.Caching.MemoryCache.Default;
				_negativeCacheLock = new ReaderWriterLockSlim();

				_negativeCachePolicy = new System.Runtime.Caching.CacheItemPolicy {
					SlidingExpiration = negativeCacheItemLifetime,
				};
			}
		}

		/// <summary>
		/// Retrieves the asset. Tries local storage first, then moves on to the remote storage systems.
		/// If neither could find the data, or if there is no remote storage set up, the failure callback is called.
		/// </summary>
		/// <param name="assetId">Asset identifier.</param>
		/// <param name="successCallback">Callback called when the asset was successfully found.</param>
		/// <param name="failureCallback">Callback called when there was a failure attempting to get the asset.</param>
		/// <param name="storeResultLocally">Specifies to locally store the asset if it was fetched from a remote.</param>
		public void GetAsset(Guid assetId, SuccessCallback successCallback, FailureCallback failureCallback, bool storeResultLocally = true) {
			successCallback = successCallback ?? throw new ArgumentNullException(nameof(successCallback));
			failureCallback = failureCallback ?? throw new ArgumentNullException(nameof(failureCallback));
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Asset ID cannot be zero.", nameof(assetId));
			}

			if (_negativeCache != null) {
				_negativeCacheLock.EnterReadLock();
				try {
					if (_negativeCache.Contains(assetId.ToString("N"))) {
						failureCallback();
						return;
					}
				}
				finally {
					_negativeCacheLock.ExitReadLock();
				}
			}

			// Solves GET in middle of PUT situation.
			if (_localStorage.Contains(assetId) && !_localStorage.AssetOnDisk(assetId)) {
				// Asset exists, just might not be on disk yet. Wait here until the asset makes it to disk.
				SpinWait.SpinUntil(() => _localStorage.AssetOnDisk(assetId));
			}

			_assetReader.GetAssetAsync(assetId, asset => {
				if (asset != null) {
					successCallback(asset);
					return;
				}

				failureCallback();

				if (_negativeCache != null) {
					_negativeCacheLock.EnterWriteLock();
					try {
						_negativeCache.Set(new System.Runtime.Caching.CacheItem(assetId.ToString("N"), 0), _negativeCachePolicy);
					}
					finally {
						_negativeCacheLock.ExitWriteLock();
					}
				}
			}, storeResultLocally ? ChattelReader.CacheRule.Normal : ChattelReader.CacheRule.SkipWrite);
		}

		/// <summary>
		/// Attempts to verify if the asset is known or not. Tries the local storage first, then moves on to the remote storage systems.
		/// </summary>
		/// <param name="assetId">Asset identifier.</param>
		/// <param name="foundCallback">Callback called when it is known if the asset is found or not.</param>
		public void CheckAsset(Guid assetId, FoundCallback foundCallback) => GetAsset(assetId, asset => foundCallback(asset != null), () => foundCallback(false));

		/// <summary>
		/// Stores the asset.  Mainly just a wrapper for the 
		/// </summary>
		/// <param name="asset">Asset.</param>
		/// <param name="resultCallback">Result callback.</param>
		public void StoreAsset(StratusAsset asset, StorageResultCallback resultCallback) {
			asset = asset ?? throw new ArgumentNullException(nameof(asset));
			resultCallback = resultCallback ?? throw new ArgumentNullException(nameof(resultCallback));

			if (asset.Id == Guid.Empty) {
				throw new ArgumentException("Asset cannot have zero ID.", nameof(asset));
			}

			PutResult result;

			try {
				_assetWriter.PutAssetSync(asset);
				result = PutResult.DONE;
			}
			catch (AssetExistsException) {
				result = PutResult.DUPLICATE;
			}
			catch (Exception e) {
				LOG.Error("Error storing asset.", e);
				result = PutResult.FAILURE;
			}

			if (result == PutResult.DONE) {
				// Clear negative cache entry.
				if (_negativeCache != null) {
					_negativeCacheLock.EnterWriteLock();
					try {
						_negativeCache.Remove(asset.Id.ToString("N"));
					}
					finally {
						_negativeCacheLock.ExitWriteLock();
					}
				}
			}

			resultCallback(result);
		}

		/// <summary>
		/// Purges all assets that have the "local" flag set from both disk and memory.
		/// Never touches the remotes.
		/// </summary>
		public void PurgeAllLocalAssets() {
		}

		/// <summary>
		/// Purges the given asset from disk and memory.
		/// Never touches the remotes.
		/// </summary>
		/// <param name="assetId">Asset identifier.</param>
		/// <param name="resultCallback">Result callback.</param>
		public void PurgeAsset(Guid assetId, PurgeResultCallback resultCallback) {
		}

		/// <summary>
		/// Gets the locally known asset identifiers that match the given prefix.
		/// </summary>
		/// <returns>The locally known asset identifiers.</returns>
		/// <param name="prefix">Prefix.</param>
		public IEnumerable<Guid> GetLocallyKnownAssetIds(string prefix) {
			return _localStorage.ActiveIds(prefix);
		}

		/// <summary>
		/// Result of attempting to store an asset.
		/// </summary>
		public enum PutResult {
			DONE,
			DUPLICATE,
			FAILURE,
		}

		/// <summary>
		/// Result of attempting to purge an asset.
		/// </summary>
		public enum PurgeResult {
			DONE,
			NOT_FOUND_LOCALLY,
		}
	}
}
