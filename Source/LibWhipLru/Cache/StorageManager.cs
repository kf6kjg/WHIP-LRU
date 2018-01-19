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
	public class StorageManager {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly uint DEFAULT_NC_LIFETIME_SECONDS = 60 * 2;

		private ChattelReader _assetReader;
		private ChattelWriter _assetWriter;

		/// <summary>
		/// Stores IDs that are failures.  No need to disk backup, it's OK to lose this info in a restart.
		/// </summary>
		private readonly System.Runtime.Caching.ObjectCache _negativeCache;
		private readonly System.Runtime.Caching.CacheItemPolicy _negativeCachePolicy;
		private readonly ReaderWriterLockSlim _negativeCacheLock;

		public StorageManager(
			AssetCacheLmdb cache,
			TimeSpan negativeCacheItemLifetime
		) {
			_assetReader = null;
			_assetWriter = null;

			if (negativeCacheItemLifetime.TotalSeconds > 0) {
				_negativeCache = System.Runtime.Caching.MemoryCache.Default;
				_negativeCacheLock = new ReaderWriterLockSlim();

				_negativeCachePolicy = new System.Runtime.Caching.CacheItemPolicy {
					SlidingExpiration = negativeCacheItemLifetime,
				};
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
		/// <param name="cacheResult">Specifies to locally store the asset if it was fetched from a remote.</param>
		public StratusAsset GetAsset(Guid assetId, bool cacheResult = true) {
			if (assetId == Guid.Empty) {
				throw new ArgumentException("Asset ID cannot be zero.", nameof(assetId));
			}

			// TODO: Figure out how to prevent handle parallel GET for same ID causing multiple requests.  Additional requests shoudl just wait for the data from the first request.

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
			if (_assetReader != null) {
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

		#region IChattelCache Support

		bool IChattelCache.TryGetCachedAsset(Guid assetId, out StratusAsset asset) {
			throw new NotImplementedException();
		}

		void IChattelCache.CacheAsset(StratusAsset asset) {
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
							return;
						}
					}
				}
				else {
					// Only known cause of an exception not causing an immediate retry is the case of already exists. And that already logs the details.
					throw new AssetExistsException(asset.Id);
				}
			}
			else {
				LOG.Info($"{asset.Id} was rejected from storage as a duplicate.");
				throw new AssetExistsException(asset.Id);
			}
		}

		void IChattelCache.Purge() {
			throw new NotImplementedException();
		}

		void IChattelCache.Purge(Guid assetId) {
			throw new NotImplementedException();
		}

		#endregion
		public enum PutResult {
			DONE,
			DUPLICATE,
			WIP,
		}
	}
}
