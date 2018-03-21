// PartitionedTemporalGuidCache.cs
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
using System.IO;
using System.Linq;

namespace LibWhipLru.Cache {
	/// <summary>
	/// Stores the asset IDs in a manner that makes it easy to clear out the least recently used assets AND gain enough space for the incoming assets.
	/// </summary>
	public class PartitionedTemporalGuidCache {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly TimeSpan MIN_PARTITION_INTERVAL = TimeSpan.FromSeconds(1);

		private readonly ConcurrentDictionary<Guid, MetaAsset> _cache;

		private readonly ConcurrentQueue<TemporalPartition> _partitions;
		private TemporalPartition _activePartition;
		private object _activePartitionUpdateLock = new object();
		private readonly TimeSpan _partitionInterval;
		private readonly string _partitionBasePath;

		private readonly Action<string> _partitionOpenOrCreateCallback;
		private readonly Action<string> _partitionDeletionCallback;
		private readonly Action<Guid, string, string> _partitionTransferAssetCallback;

		public uint PartitionCount {
			get {
				return (uint)(_partitions?.Count ?? 0);
			}
		}

		public IEnumerable<string> PartitionPaths {
			get {
				return _partitions?.Select(partition => partition.DiskPath);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Cache.PartitionedTemporalGuidCache"/> class.
		/// </summary>
		public PartitionedTemporalGuidCache(
			string partitionBasePath,
			TimeSpan partitionInterval,
			Action<string> partitionOpenOrCreateCallback,
			Action<string> partitionDeletionCallback,
			Action<Guid, string, string> partitionTransferAssetCallback,
			Func<string, Dictionary<Guid, uint>> partitionFoundCallback
		) {
			if (string.IsNullOrWhiteSpace(partitionBasePath)) {
				throw new ArgumentNullException(nameof(partitionBasePath), "Cannot be null, empty, or whitespace.");
			}

			if (partitionInterval < MIN_PARTITION_INTERVAL) {
				throw new ArgumentOutOfRangeException(nameof(partitionInterval), $"Cannot be less than {MIN_PARTITION_INTERVAL.TotalSeconds} seconds.");
			}

			_partitionBasePath = partitionBasePath;
			_partitionInterval = partitionInterval;
			_partitionOpenOrCreateCallback = partitionOpenOrCreateCallback ?? throw new ArgumentNullException(nameof(partitionOpenOrCreateCallback));
			_partitionDeletionCallback = partitionDeletionCallback ?? throw new ArgumentNullException(nameof(partitionDeletionCallback));
			_partitionTransferAssetCallback = partitionTransferAssetCallback ?? throw new ArgumentNullException(nameof(partitionTransferAssetCallback));

			_cache = new ConcurrentDictionary<Guid, MetaAsset>();
			_partitions = new ConcurrentQueue<TemporalPartition>();

			// Restore from disk, if there's something to restore from.
			foreach (var dbPath in Directory.EnumerateFileSystemEntries(partitionBasePath, "*", SearchOption.TopDirectoryOnly)) {
				var assetsFound = partitionFoundCallback(dbPath);

				if ((assetsFound?.Count ?? 0) > 0) {
					var partition = new TemporalPartition(dbPath);
					_partitions.Enqueue(partition);

					// Will wind up restoring assets that were previously "purged" as the per-item purge operation only removes the ID from active memory.
					// Currently this is acceptible behavior as the per-item purge is only expectd to be used to clean up inconsistencies by purging the ID then adding a fresh copy.
					// Everything else will just eventually fall out the LRU process.
					foreach (var asset in assetsFound) {
						_cache.AddOrUpdate(
							asset.Key,
							assetId => new MetaAsset { // add
							Id = assetId,
								Partition = partition,
								Size = asset.Value
							},
							(assetId, oldMeta) => { // update
								oldMeta.Partition = partition;
								oldMeta.Size = asset.Value;
								return oldMeta;
							}
						);
					}
				}
			}

			UpdateActivePartition();
		}

		/// <summary>
		/// Gets the count of cached entries.
		/// </summary>
		/// <value>The count.</value>
		public uint Count => (uint)_cache.Count;

		/// <summary>
		/// Tries to add a new entry to the cache. Does nothing to disk.
		/// </summary>
		/// <returns><c>true</c>, if add was successful, <c>false</c> otherwise.</returns>
		/// <param name="uuid">Asset ID.</param>
		/// <param name="size">Size of asset in bytes.</param>
		/// <param name="partitionPath">Path to partition wherein to store the asset.</param>
		public bool TryAdd(Guid uuid, ulong size, out string partitionPath) {
			var meta = new MetaAsset {
				Id = uuid,
				Size = size,
			};

			return TryAdd(meta, out partitionPath);
		}

		/// <summary>
		/// Tries to add a previously removed entry to the cache. Does nothing to disk.
		/// </summary>
		/// <returns><c>true</c>, if add was successful, <c>false</c> otherwise.</returns>
		/// <param name="removedObject">Meta asset as returned from the <see cref="Remove"/> method .</param>
		public bool TryAdd(object removedObject, out string partitionPath) {
			var meta = removedObject as MetaAsset;
			if (meta == null) {
				throw new ArgumentOutOfRangeException(nameof(removedObject), "Null or invalid object type.");
			}

			UpdateActivePartition();

			var activePartition = _activePartition; // Thread safety

			if (_cache.TryAdd(meta.Id, meta)) {
				meta.Partition = activePartition;
				activePartition.ActiveEntries.TryAdd(meta.Id, meta);
				partitionPath = activePartition.DiskPath;
				return true;
			}

			partitionPath = null;
			return false;
		}

		/// <summary>
		/// Empties the memory cache and calls the deletion callback for each partition.
		/// </summary>
		public void Clear() {
			_cache.Clear();
			while (_partitions.TryDequeue(out var partition)) {
				// Spin, spin away.
				try {
					_partitionDeletionCallback(partition.DiskPath);
				}
				catch (Exception e) {
					LOG.Warn($"Exception while calling deletion callback for partition located at '{partition.DiskPath}' during cache clear operation.", e);
				}
			}
		}

		/// <summary>
		/// Checks to see if this contains the specified asset ID.
		/// </summary>
		/// <returns>The contains.</returns>
		/// <param name="uuid">Asset ID.</param>
		public bool Contains(Guid uuid) {
			if (_cache.TryGetValue(uuid, out var ma)) {
				RefreshAssetExpiry(uuid);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the asset size in bytes.
		/// </summary>
		/// <returns>The size in bytes.</returns>
		/// <param name="uuid">Asset ID.</param>
		public ulong? AssetSize(Guid uuid) {
			if (_cache.TryGetValue(uuid, out var ma)) {
				RefreshAssetExpiry(uuid);

				return ma.Size;
			}

			return null;
		}

		/// <summary>
		/// Sets the asset size in bytes.
		/// </summary>
		/// <param name="uuid">Asset ID.</param>
		/// <param name="size">Size.</param>
		public void AssetSize(Guid uuid, ulong size) {
			if (_cache.TryGetValue(uuid, out var ma)) {
				RefreshAssetExpiry(uuid);

				ma.Size = size;
			}
		}

		/// <summary>
		/// Retrieves the asset's partition path.
		/// </summary>
		/// <returns>The partition path.</returns>
		/// <param name="uuid">Asset ID</param>
		public bool TryGetAssetPartition(Guid uuid, out string path) {
			if (_cache.TryGetValue(uuid, out var ma)) {

				path = ma.Partition.DiskPath;
				return true;
			}

			path = null;
			return false;
		}

		/// <summary>
		/// Returns all locally known asset IDs in the cache that start with the given prefix.
		/// </summary>
		/// <returns>Asset IDs.</returns>
		/// <param name="prefix">Prefix.</param>
		public IEnumerable<Guid> ItemsWithPrefix(string prefix) {
			var matchingKvps = _cache.Where(kvp => kvp.Key.ToString("N").StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));

			foreach(var kvp in matchingKvps) {
				RefreshAssetExpiry(kvp.Key);
			}

			return matchingKvps.Select(kvp => kvp.Key);
		}

		/// <summary>
		/// Tries to remove the given asset ID from the cache. Does not do anything to disk.
		/// </summary>
		/// <returns><c>true</c>, if remove was tryed, <c>false</c> otherwise.</returns>
		/// <param name="uuid">UUID.</param>
		public bool TryRemove(Guid uuid) {
			if (_cache.TryRemove(uuid, out var meta)) {
				meta.Partition.ActiveEntries.TryRemove(uuid, out var junk);

				return true;
			}

			return false;
		}

		/// <summary>
		/// Removes enough of the oldest entries to clear out at least the specified size, returning the UUIDs that were removed and an opaque pointer to internal-use objects that can be used to re-enter the removed asset.
		/// This is an expensive and slow operation.  Every returned item has already been wiped from the memory cache.
		/// </summary>
		/// <param name="minByteCountToClear">Byte count to at minimum clear out.</param>
		/// <param name="byteCountCleared">Byte count that was actually removed.</param>
		public IDictionary<Guid, object> Remove(ulong minByteCountToClear, out ulong byteCountCleared) {
			var itemsRemoved = new Dictionary<Guid, object>();

			byteCountCleared = 0UL;

			while (byteCountCleared < minByteCountToClear && _partitions.TryDequeue(out var partition)) {
				var partitionDiskSize = partition.GetDiskSize();

				var deleteSucceeded = false;

				try {
					_partitionDeletionCallback(partition.DiskPath);
					deleteSucceeded = true;
				}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
				catch {
					// BUG: If the files won't delete, we're going to leak disk space. Requeuing the partition isn't right as that'd be saying that it was fresh, and there's no way to push onto the end.
				}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

				if (deleteSucceeded) {
					byteCountCleared += partitionDiskSize;
					// Add the removed items to the output list.
					foreach (var assetId in partition.ActiveEntries.Keys) {
						_cache.TryRemove(assetId, out var meta);
						try {
							itemsRemoved.Add(assetId, meta);
						}
						catch (ArgumentException) {
							// Skip, it's already there.  Technically this means there's a bug as an asset should only ever be active in a single partition.
							// If you see this bug, check for all locations where an item is updated in _cache but the handling of the asset's old partition doesn't get a good clean remove.
							LOG.Debug($"BUG: somehow asset {assetId} was active in multiple partitions!");
						}
					}
				}
			}

			return itemsRemoved;
		}

		private void RefreshAssetExpiry(Guid assetId) {
			UpdateActivePartition();
			var activePartition = _activePartition; // Thread safety!
			// If known and in an older partition, then copy to the new partition.
			if (_cache.TryGetValue(assetId, out var meta) && meta.Partition != activePartition) {
				var oldPartition = meta.Partition;

				_partitionTransferAssetCallback(assetId, oldPartition.DiskPath, activePartition.DiskPath);

				oldPartition.ActiveEntries.TryRemove(assetId, out var junk);
				meta.Partition = activePartition;
			}
		}

		private void UpdateActivePartition() {
			lock (_activePartitionUpdateLock) {
				if (_activePartition == null || DateTimeOffset.UtcNow - _activePartition.Created >= _partitionInterval) {
					// Update the active partition.
					_activePartition = new TemporalPartition(_partitionBasePath, _partitionOpenOrCreateCallback);
					_partitions.Enqueue(_activePartition);
				}
			}
		}

		private class MetaAsset {
			public Guid Id { get; set; }
			/// <summary>
			/// The size in bytes of the asset on disk. If 0, then the asset has yet to be written to disk.
			/// </summary>
			/// <value>The size.</value>
			public ulong Size { get; set; }
			public TemporalPartition Partition { get; set; }
		}

		private class TemporalPartition {
			public DateTimeOffset Created { get; private set; }
			public string DiskPath { get; private set; }

			public ConcurrentDictionary<Guid, MetaAsset> ActiveEntries { get; private set; } = new ConcurrentDictionary<Guid, MetaAsset>();

			public TemporalPartition(string basePath, Action<string> partitionCreationCallback) {
				if (string.IsNullOrWhiteSpace(basePath)) {
					throw new ArgumentNullException(nameof(basePath), "Cannot be null, empty, or whitespace");
				}

				Created = DateTimeOffset.UtcNow;
				DiskPath = Path.Combine(basePath, Created.ToString("s"));

				partitionCreationCallback(DiskPath);
			}

			public TemporalPartition(string diskPath) {
				if (string.IsNullOrWhiteSpace(diskPath)) {
					throw new ArgumentNullException(nameof(diskPath), "Cannot be null, empty, or whitespace");
				}

				Created = new DirectoryInfo(diskPath).CreationTimeUtc;
				DiskPath = diskPath;
			}

			public ulong GetDiskSize() {
				// I'd be happier I think with the byte count of the blocks consumed.
				return (ulong)Directory.EnumerateFiles(DiskPath).Select(file => new FileInfo(file).Length).Aggregate((prev, cur) => prev + cur);
			}
		}
	}
}
