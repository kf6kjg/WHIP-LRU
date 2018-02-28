// OrderedGuidCache.cs
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
using System.Linq;

namespace LibWhipLru.Cache {
	/// <summary>
	/// Stores the asset IDs in a manner that makes it easy to clear out the least recently used assets AND gain enough space for the incoming assets.
	/// </summary>
	public class OrderedGuidCache {
		private readonly ConcurrentDictionary<Guid, MetaAsset> _cache;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Cache.OrderedGuidCache"/> class.
		/// </summary>
		public OrderedGuidCache() {
			_cache = new ConcurrentDictionary<Guid, MetaAsset>();
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
		public bool TryAdd(Guid uuid, ulong size) => _cache.TryAdd(uuid, new MetaAsset{ Id = uuid, LastAccessed = DateTimeOffset.UtcNow, Size = size, });

		/// <summary>
		/// Tries to add a previously removed entry to the cache. Does nothing to disk.
		/// </summary>
		/// <returns><c>true</c>, if add was successful, <c>false</c> otherwise.</returns>
		/// <param name="removedObject">Meta asset as returned from the <see cref="Remove"/> method .</param>
		public bool TryAdd(object removedObject) {
			var meta = removedObject as MetaAsset;
			if (meta == null) {
				throw new ArgumentOutOfRangeException(nameof(removedObject), "Null or invalid object type.");
			}

			return _cache.TryAdd(meta.Id, meta);
		}

		/// <summary>
		/// Empties the memory cache. Does nothing to disk.
		/// </summary>
		public void Clear() => _cache.Clear();

		/// <summary>
		/// Checks to see if this contains the specified asset ID.
		/// </summary>
		/// <returns>The contains.</returns>
		/// <param name="uuid">Asset ID.</param>
		public bool Contains(Guid uuid) {
			if (_cache.TryGetValue(uuid, out var ma)) {
				ma.LastAccessed = DateTimeOffset.UtcNow;

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
				ma.LastAccessed = DateTimeOffset.UtcNow;

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
				ma.LastAccessed = DateTimeOffset.UtcNow;

				ma.Size = size;
			}
		}

		/// <summary>
		/// Returns all locally known asset IDs in the cache that start with the given prefix.
		/// </summary>
		/// <returns>Asset IDs.</returns>
		/// <param name="prefix">Prefix.</param>
		public IEnumerable<Guid> ItemsWithPrefix(string prefix) {
			var matchingKvps = _cache.Where(kvp => kvp.Key.ToString("N").StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));

			foreach(var kvp in matchingKvps) {
				kvp.Value.LastAccessed = DateTimeOffset.UtcNow;
			}

			return matchingKvps.Select(kvp => kvp.Key);
		}

		/// <summary>
		/// Tries to remove the given asset ID from the cache. Does not do anything to disk.
		/// </summary>
		/// <returns><c>true</c>, if remove was tryed, <c>false</c> otherwise.</returns>
		/// <param name="uuid">UUID.</param>
		public bool TryRemove(Guid uuid) {
			return _cache.TryRemove(uuid, out var trash);
		}

		/// <summary>
		/// Removes enough of the oldest entries to clear out at least the specified size, returning the UUIDs that were removed.
		/// This is an expensive and slow operation.
		/// </summary>
		/// <param name="size">Size</param>
		public IDictionary<Guid, object> Remove(ulong size, out ulong sizeCleared) {
			var sorted = _cache.Values.OrderBy(ma => ma.LastAccessed);

			var removed = new Dictionary<Guid, object>();
			sizeCleared = 0;

			foreach (var ma in sorted) {
				if (sizeCleared >= size) {
					break;
				}

				if (ma.Size > 0 && TryRemove(ma.Id)) {
					removed.Add(ma.Id, ma);
					sizeCleared += ma.Size;
				}
			}

			return removed;
		}

		private class MetaAsset {
			public Guid Id { get; set; }
			public DateTimeOffset LastAccessed { get; set;}
			/// <summary>
			/// The size in bytes of the asset on disk. If 0, then the asset has yet to be written to disk.
			/// </summary>
			/// <value>The size.</value>
			public ulong Size { get; set; }
		}
	}
}
