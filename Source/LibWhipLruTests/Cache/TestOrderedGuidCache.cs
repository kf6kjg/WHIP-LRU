// TestOrderedGuidCache.cs
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
using System.Linq;
using System.Threading;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public static class TestOrderedGuidCache {
		#region Ctor

		[Test]
		public static void TestOrderedGuidCache_Ctor_DoesntThrow() {
			Assert.DoesNotThrow(() => new OrderedGuidCache());
		}

		#endregion

		#region TryAdd

		[Test]
		public static void TestOrderedGuidCache_TryAdd_FirstTime_ReturnsTrue() {
			var cache = new OrderedGuidCache();
			Assert.True(cache.TryAdd(Guid.NewGuid(), 0));
		}

		[Test]
		public static void TestOrderedGuidCache_TryAdd_Multiple_ReturnsTrue() {
			var cache = new OrderedGuidCache();
			Assert.True(cache.TryAdd(Guid.NewGuid(), 1));
			Assert.True(cache.TryAdd(Guid.NewGuid(), 2));
			Assert.True(cache.TryAdd(Guid.NewGuid(), 3));
		}

		[Test]
		public static void TestOrderedGuidCache_TryAdd_Duplicate_ReturnsFalse() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 1);
			Assert.False(cache.TryAdd(guid, 2));
		}

		#endregion

		#region Count

		[Test]
		public static void TestOrderedGuidCache_Count_Fresh_IsZero() {
			var cache = new OrderedGuidCache();
			Assert.AreEqual(0, cache.Count);
		}

		[Test]
		public static void TestOrderedGuidCache_Count_Returns3AfterAdding3() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(Guid.NewGuid(), 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.AreEqual(3, cache.Count);
		}

		#endregion

		#region Clear

		[Test]
		public static void TestOrderedGuidCache_Clear_ResultsInCountZero() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 0);
			cache.Clear();
			Assert.AreEqual(0, cache.Count);
		}

		#endregion

		#region Contains

		[Test]
		public static void TestOrderedGuidCache_Contains_DoesntFindUnknown() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 0);
			Assert.False(cache.Contains(Guid.NewGuid()));
		}

		[Test]
		public static void TestOrderedGuidCache_Contains_FindsKnown() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.True(cache.Contains(guid));
		}

		[Test]
		public static void TestOrderedGuidCache_Contains_DoesntFindRemovedItem() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			cache.TryRemove(guid);
			Assert.False(cache.Contains(guid));
		}

		#endregion

		#region AssetSize Get

		[Test]
		public static void TestOrderedGuidCache_AssetSizeGet_Known_SizeCorrect() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.AreEqual(2, cache.AssetSize(guid));
		}

		[Test]
		public static void TestOrderedGuidCache_AssetSizeGet_Unknown_Null() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.IsNull(cache.AssetSize(guid));
		}

		#endregion

		#region AssetSize Set

		[Test]
		public static void TestOrderedGuidCache_AssetSizeSet_Known_SizeUpdated() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);

			cache.AssetSize(guid, 10);

			Assert.AreEqual(10, cache.AssetSize(guid));
		}

		[Test]
		public static void TestOrderedGuidCache_AssetSizeSet_Unknown_NoChange() {
			var cache = new OrderedGuidCache();
			var guid1 = Guid.NewGuid();
			var guid2 = Guid.NewGuid();
			var guid3 = Guid.NewGuid();
			cache.TryAdd(guid1, 1);
			cache.TryAdd(guid3, 3);

			cache.AssetSize(guid2, 10);

			Assert.AreEqual(1, cache.AssetSize(guid1));
			Assert.AreEqual(3, cache.AssetSize(guid3));
		}

		#endregion

		#region ItemsWithPrefix

		[Test]
		public static void TestOrderedGuidCache_ItemsWithPrefix_DoesntFindUnknown() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 1);
			cache.TryAdd(Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6"), 2);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 3);
			Assert.IsEmpty(cache.ItemsWithPrefix("123"));
		}

		[Test]
		public static void TestOrderedGuidCache_ItemsWithPrefix_FindsSingularKnown() {
			var cache = new OrderedGuidCache();
			var guid = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid, 1);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 2);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 3);

			var result = cache.ItemsWithPrefix(guid.ToString("N").Substring(0, 3));
			Assert.AreEqual(1, result.Count());
			Assert.That(result, Contains.Item(guid));
		}

		[Test]
		public static void TestOrderedGuidCache_ItemsWithPrefix_FindsMultipleKnown() {
			var cache = new OrderedGuidCache();
			var guid1 = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid1, 1);
			var guid2 = Guid.Parse("fcfdbe4a-1f93-4316-8c32-ae7a168a00e4");
			cache.TryAdd(guid2, 2);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 3);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 4);

			var result = cache.ItemsWithPrefix(guid1.ToString("N").Substring(0, 3));
			Assert.AreEqual(2, result.Count());
			Assert.That(result, Contains.Item(guid1));
			Assert.That(result, Contains.Item(guid2));
		}

		#endregion

		#region TryRemove

		[Test]
		public static void TestOrderedGuidCache_TryRemove_DoesntRemoveUnknown() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 0);
			Assert.False(cache.TryRemove(Guid.NewGuid()));
			Assert.AreEqual(1, cache.Count);
		}

		[Test]
		public static void TestOrderedGuidCache_TryRemove_DoesRemoveKnown() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.True(cache.TryRemove(guid));
			Assert.AreEqual(2, cache.Count);
			Assert.False(cache.Contains(guid));
		}

		#endregion

		#region Remove

		[Test]
		public static void TestOrderedGuidCache_Remove_Empty_ReturnsEmptyAndZero() {
			var cache = new OrderedGuidCache();
			var removed = cache.Remove(100, out var sizeCleared);
			Assert.IsEmpty(removed);
			Assert.AreEqual(0, sizeCleared);
		}

		[Test]
		public static void TestOrderedGuidCache_Remove_RemovesItems() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 2);
			cache.TryAdd(Guid.NewGuid(), 4);
			cache.TryAdd(Guid.NewGuid(), 8);

			cache.Remove(5, out var sizeCleared);

			Assert.Less(cache.Count, 3);
		}

		[Test]
		public static void TestOrderedGuidCache_Remove_ReportsCorrectSizeCleared() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 2);
			cache.TryAdd(Guid.NewGuid(), 4);
			cache.TryAdd(Guid.NewGuid(), 8);

			cache.Remove(5, out var sizeCleared);

			Assert.AreEqual(6, sizeCleared);
		}

		[Test]
		public static void TestOrderedGuidCache_Remove_ReturnsLeastRecentlyAccessedItems() {
			var cache = new OrderedGuidCache();

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();
			var guidStays2 = Guid.NewGuid();
			var guidStays3 = Guid.NewGuid();

			cache.TryAdd(guidStays1, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidStays2, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidRemoved1, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidRemoved2, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidStays3, 2);
			Thread.Sleep(100);

			// Touch the timestamps
			cache.Contains(guidStays1);
			Thread.Sleep(100);
			cache.Contains(guidStays3);
			Thread.Sleep(100);
			cache.ItemsWithPrefix(guidStays2.ToString("N").Substring(0, 3));
			Thread.Sleep(100);

			var removed = cache.Remove(3, out var sizeCleared);

			Assert.That(removed, Contains.Item(guidRemoved1));
			Assert.That(removed, Contains.Item(guidRemoved2));
		}

		[Test]
		public static void TestOrderedGuidCache_Remove_CacheDoesntContainRemovedItems() {
			var cache = new OrderedGuidCache();

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();
			var guidStays2 = Guid.NewGuid();
			var guidStays3 = Guid.NewGuid();

			cache.TryAdd(guidStays1, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidStays2, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidRemoved1, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidRemoved2, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidStays3, 2);
			Thread.Sleep(100);

			// Touch the timestamps
			cache.Contains(guidStays1);
			Thread.Sleep(100);
			cache.Contains(guidStays3);
			Thread.Sleep(100);
			cache.ItemsWithPrefix(guidStays2.ToString("N").Substring(0, 3));
			Thread.Sleep(100);

			cache.Remove(3, out var sizeCleared);

			Assert.False(cache.Contains(guidRemoved1));
			Assert.False(cache.Contains(guidRemoved2));
		}

		[Test]
		public static void TestOrderedGuidCache_Remove_LeavesMostRecentlyAccessedItems() {
			var cache = new OrderedGuidCache();

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();
			var guidStays2 = Guid.NewGuid();
			var guidStays3 = Guid.NewGuid();

			cache.TryAdd(guidStays1, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidStays2, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidRemoved1, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidRemoved2, 2);
			Thread.Sleep(100);
			cache.TryAdd(guidStays3, 2);
			Thread.Sleep(100);

			// Touch the timestamps
			cache.Contains(guidStays1);
			Thread.Sleep(100);
			cache.Contains(guidStays3);
			Thread.Sleep(100);
			cache.ItemsWithPrefix(guidStays2.ToString("N").Substring(0, 3));
			Thread.Sleep(100);

			cache.Remove(3, out var sizeCleared);

			Assert.True(cache.Contains(guidStays1));
			Assert.True(cache.Contains(guidStays2));
			Assert.True(cache.Contains(guidStays3));
		}

		#endregion
	}
}
