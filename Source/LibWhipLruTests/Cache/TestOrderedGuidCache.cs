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
	public class TestOrderedGuidCache {
		[Test]
		public void TestCtorDoesNotThrow() {
			Assert.DoesNotThrow(() => new OrderedGuidCache());
		}

		[Test]
		public void TestTryAddFirstTimeReturnsTrue() {
			var cache = new OrderedGuidCache();
			Assert.True(cache.TryAdd(Guid.NewGuid(), 0));
		}

		[Test]
		public void TestTryAddMultipleReturnsTrue() {
			var cache = new OrderedGuidCache();
			Assert.True(cache.TryAdd(Guid.NewGuid(), 1));
			Assert.True(cache.TryAdd(Guid.NewGuid(), 2));
			Assert.True(cache.TryAdd(Guid.NewGuid(), 3));
		}

		[Test]
		public void TestTryAddDuplicateReturnsFalse() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 1);
			Assert.False(cache.TryAdd(guid, 2));
		}

		[Test]
		public void TestCountFreshIsZero() {
			var cache = new OrderedGuidCache();
			Assert.AreEqual(0, cache.Count);
		}

		[Test]
		public void TestCountReturns3AfterAdding3() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(Guid.NewGuid(), 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.AreEqual(3, cache.Count);
		}

		[Test]
		public void TestClearResultsInCountZero() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 0);
			cache.Clear();
			Assert.AreEqual(0, cache.Count);
		}

		[Test]
		public void TestContainsDoesntFindUnknown() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 0);
			Assert.False(cache.Contains(Guid.NewGuid()));
		}

		[Test]
		public void TestContainsFindsKnown() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.True(cache.Contains(guid));
		}

		[Test]
		public void TestContainsDoesntFindRemovedItem() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			cache.TryRemove(guid);
			Assert.False(cache.Contains(guid));
		}

		[Test]
		public void TestItemsWithPrefixDoesntFindUnknown() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 1);
			cache.TryAdd(Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6"), 2);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 3);
			Assert.IsEmpty(cache.ItemsWithPrefix("123"));
		}

		[Test]
		public void TestItemsWithPrefixFindsSingularKnown() {
			var cache = new OrderedGuidCache();
			var guid = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid, 1);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 2);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 3);

			var result = cache.ItemsWithPrefix(guid.ToString().Substring(0, 3));
			Assert.AreEqual(1, result.Count());
			Assert.That(result, Contains.Item(guid));
		}

		[Test]
		public void TestItemsWithPrefixFindsMultipleKnown() {
			var cache = new OrderedGuidCache();
			var guid1 = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid1, 1);
			var guid2 = Guid.Parse("fcfdbe4a-1f93-4316-8c32-ae7a168a00e4");
			cache.TryAdd(guid2, 2);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 3);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 4);

			var result = cache.ItemsWithPrefix(guid1.ToString().Substring(0, 3));
			Assert.AreEqual(2, result.Count());
			Assert.That(result, Contains.Item(guid1));
			Assert.That(result, Contains.Item(guid2));
		}

		[Test]
		public void TestTryRemoveDoesntRemoveUnknown() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 0);
			Assert.False(cache.TryRemove(Guid.NewGuid()));
			Assert.AreEqual(1, cache.Count);
		}

		[Test]
		public void TestTryRemoveDoesRemoveKnown() {
			var cache = new OrderedGuidCache();
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1);
			cache.TryAdd(guid, 2);
			cache.TryAdd(Guid.NewGuid(), 3);
			Assert.True(cache.TryRemove(guid));
			Assert.AreEqual(2, cache.Count);
			Assert.False(cache.Contains(guid));
		}

		[Test]
		public void TestRemoveEmptyReturnsEmptyAndZero() {
			var cache = new OrderedGuidCache();
			uint sizeCleared;
			var removed = cache.Remove(100, out sizeCleared);
			Assert.IsEmpty(removed);
			Assert.AreEqual(0, sizeCleared);
		}

		[Test]
		public void TestRemoveRemovesItems() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 2);
			cache.TryAdd(Guid.NewGuid(), 4);
			cache.TryAdd(Guid.NewGuid(), 8);

			uint sizeCleared;
			cache.Remove(5, out sizeCleared);

			Assert.Less(cache.Count, 3);
		}

		[Test]
		public void TestRemoveReportsCorrectSizeCleared() {
			var cache = new OrderedGuidCache();
			cache.TryAdd(Guid.NewGuid(), 2);
			cache.TryAdd(Guid.NewGuid(), 4);
			cache.TryAdd(Guid.NewGuid(), 8);

			uint sizeCleared;
			cache.Remove(5, out sizeCleared);

			Assert.AreEqual(6, sizeCleared);
		}

		[Test]
		public void TestRemoveReturnsLeastRecentlyAccessedItems() {
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
			cache.ItemsWithPrefix(guidStays2.ToString().Substring(0, 3));
			Thread.Sleep(100);

			uint sizeCleared;
			var removed = cache.Remove(3, out sizeCleared);

			Assert.That(removed, Contains.Item(guidRemoved1));
			Assert.That(removed, Contains.Item(guidRemoved2));
		}

		[Test]
		public void TestRemoveCacheDoesntContainRemovedItems() {
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
			cache.ItemsWithPrefix(guidStays2.ToString().Substring(0, 3));
			Thread.Sleep(100);

			uint sizeCleared;
			cache.Remove(3, out sizeCleared);

			Assert.False(cache.Contains(guidRemoved1));
			Assert.False(cache.Contains(guidRemoved2));
		}

		[Test]
		public void TestRemoveLeavesMostRecentlyAccessedItems() {
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
			cache.ItemsWithPrefix(guidStays2.ToString().Substring(0, 3));
			Thread.Sleep(100);

			uint sizeCleared;
			cache.Remove(3, out sizeCleared);

			Assert.True(cache.Contains(guidStays1));
			Assert.True(cache.Contains(guidStays2));
			Assert.True(cache.Contains(guidStays3));
		}
	}
}
