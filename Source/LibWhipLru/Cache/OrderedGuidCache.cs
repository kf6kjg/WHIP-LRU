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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LibWhipLru.Cache {
	public class OrderedGuidCache {
		private ConcurrentDictionary<Guid, MetaAsset> cache;

		public OrderedGuidCache() {
			cache = new ConcurrentDictionary<Guid, MetaAsset>();
		}

		public uint Count => (uint)cache.Count;

		public bool TryAdd(Guid uuid, uint size) => cache.TryAdd(uuid, new MetaAsset{ Id = uuid, LastAccessed = DateTimeOffset.UtcNow, Size = size, });

		public void Clear() => cache.Clear();

		public bool Contains(Guid uuid) {
			MetaAsset ma;
			if (cache.TryGetValue(uuid, out ma)) {
				ma.LastAccessed = DateTimeOffset.UtcNow;

				return true;
			}

			return false;
		}

		public IEnumerable<Guid> ItemsWithPrefix(string prefix) {
			var matchingKvps = cache.Where(kvp => kvp.Key.ToString("N").StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));

			foreach(var kvp in matchingKvps) {
				kvp.Value.LastAccessed = DateTimeOffset.UtcNow;
			}

			return matchingKvps.Select(kvp => kvp.Key);
		}

		public bool TryRemove(Guid uuid) {
			MetaAsset trash;
			return cache.TryRemove(uuid, out trash);
		}

		/// <summary>
		/// Removes enough of the oldest entries to clear out at least the specified size, returning the UUIDs that were removed.
		/// This is an expensive and slow operation.
		/// </summary>
		/// <param name="size">Size</param>
		public IEnumerable<Guid> Remove(int size, out uint sizeCleared) {
			var sorted = cache.Values.OrderBy(ma => ma.LastAccessed);

			var removed = new List<Guid>();
			sizeCleared = 0;

			foreach (var ma in sorted) {
				if (sizeCleared >= size) {
					break;
				}

				if (TryRemove(ma.Id)) {
					removed.Add(ma.Id);
					sizeCleared += ma.Size;
				}
			}

			return removed;
		}

		private class MetaAsset {
			public Guid Id { get; set; }
			public DateTimeOffset LastAccessed { get; set;}
			public uint Size { get; set; }
		}
	}
}
