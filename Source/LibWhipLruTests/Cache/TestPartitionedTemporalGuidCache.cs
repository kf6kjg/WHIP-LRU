// TestPartitionedTemporalGuidCache.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public static class TestPartitionedTemporalGuidCache {
		public static readonly string DATABASE_FOLDER_PATH = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_ac_lmdb");

		public static void CleanLocalStorageFolder(string dbFolderPath, string writeCacheFilePath) {
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				File.Delete(writeCacheFilePath);
			}
			catch {
			}
			try {
				Directory.Delete(dbFolderPath, true);
			}
			catch {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
		}

		public static void RebuildLocalStorageFolder(string dbFolderPath, string writeCacheFilePath) {
			CleanLocalStorageFolder(dbFolderPath, writeCacheFilePath);
			Directory.CreateDirectory(dbFolderPath);
		}

		[OneTimeSetUp]
		public static void Startup() {
			// Folder has to be there or the config fails.
			RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		[SetUp]
		public static void BeforeEveryTest() {
			RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		[TearDown]
		public static void CleanupAfterEveryTest() {
			CleanLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		#region Ctor basic param tests

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_DoesntThrow() {
			Assert.DoesNotThrow(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}


		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_PathEmpty_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PartitionedTemporalGuidCache(
				string.Empty,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_PathNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PartitionedTemporalGuidCache(
				null,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}


		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_IntervalNegative_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromTicks(-1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_IntervalZero_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromTicks(0),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_IntervalUnder1Sec_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(0.99),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		#endregion

		#region Ctor OpenCreateHandler

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_OpenCreateHandlerNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				null, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_OpenCreateHandler_IsCalledFresh() {
			var handlerCalled = false;

#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { handlerCalled = true; }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'

			Assert.True(handlerCalled);
		}

		#endregion

		#region Ctor DeleteHandler

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_DeleteHandlerNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				null, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		#endregion

		#region Ctor CopyAssetHandler

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_CopyHandlerNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				null, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			));
		}

		#endregion

		#region Ctor PartitionFoundHandler

		[Test]
		public static void TestPartitionedTemporalGuidCache_Ctor_PartitionFoundHandlerNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				null // partition found. Load it and return the asset IDs and sizes contained.
			));

			// todo: make this test do somehting
		}

		#endregion

		#region TryAdd new asset

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAdd_FirstTime_ReturnsTrue() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			Assert.True(cache.TryAdd(Guid.NewGuid(), 0, out var dbPath));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAdd_Multiple_ReturnsTrue() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			Assert.True(cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1));
			Assert.True(cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2));
			Assert.True(cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAdd_MultipleFast_SamePartition() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2);
			Assert.AreEqual(dbPath1, dbPath2);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAdd_MultipleSlow_DifferentPartition() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			Thread.Sleep(1100);
			cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2);
			Assert.AreNotEqual(dbPath1, dbPath2);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAdd_Duplicate_ReturnsFalse() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 1, out var dbPath1);
			Assert.False(cache.TryAdd(guid, 2, out var dbPath2));
		}

		#endregion

		#region TryAdd restoring old asset

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAddRestore_FirstTime_ReturnsTrue() {
			var assetId1 = Guid.NewGuid();
			IDictionary<Guid, object> assetsRemoved;
			{
				var oldCache = new PartitionedTemporalGuidCache(
					DATABASE_FOLDER_PATH,
					TimeSpan.FromSeconds(1),
					partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
					partPath => { Directory.Delete(partPath, true); }, // delete partition
					(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
					partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
				);

				oldCache.TryAdd(assetId1, 2, out var dbPath);
				File.AppendAllText(Path.Combine(dbPath, "t1"), "12");

				assetsRemoved = oldCache.Remove(2, out var cleared);
			}

			var newCache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetIdToCopy, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			Assert.True(newCache.TryAdd(assetsRemoved[assetId1], out var newDbPath));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAddRestore_Multiple_ReturnsTrue() {
			var assetId1 = Guid.NewGuid();
			var assetId2 = Guid.NewGuid();
			var assetId3 = Guid.NewGuid();

			IDictionary<Guid, object> assetsRemoved;
			{
				var oldCache = new PartitionedTemporalGuidCache(
					DATABASE_FOLDER_PATH,
					TimeSpan.FromSeconds(1),
					partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
					partPath => { Directory.Delete(partPath, true); }, // delete partition
					(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
					partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
				);

				oldCache.TryAdd(assetId1, 2, out var dbPath);
				oldCache.TryAdd(assetId2, 2, out var dbPath1);
				oldCache.TryAdd(assetId3, 2, out var dbPath2);
				File.AppendAllText(Path.Combine(dbPath, "t1"), "123456");

				assetsRemoved = oldCache.Remove(6, out var cleared);
			}

			var newCache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetIdToCopy, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			Assert.True(newCache.TryAdd(assetsRemoved[assetId1], out var newDbPath1));
			Assert.True(newCache.TryAdd(assetsRemoved[assetId2], out var newDbPath2));
			Assert.True(newCache.TryAdd(assetsRemoved[assetId3], out var newDbPath3));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAddRestore_MultipleFast_SamePartition() {
			var assetId1 = Guid.NewGuid();
			var assetId2 = Guid.NewGuid();

			IDictionary<Guid, object> assetsRemoved;
			{
				var oldCache = new PartitionedTemporalGuidCache(
					DATABASE_FOLDER_PATH,
					TimeSpan.FromSeconds(1),
					partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
					partPath => { Directory.Delete(partPath, true); }, // delete partition
					(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
					partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
				);

				oldCache.TryAdd(assetId1, 2, out var dbPath);
				oldCache.TryAdd(assetId2, 2, out var dbPath1);
				File.AppendAllText(Path.Combine(dbPath, "t1"), "1234");

				assetsRemoved = oldCache.Remove(4, out var cleared);
			}

			var newCache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetIdToCopy, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			newCache.TryAdd(assetsRemoved[assetId1], out var newDbPath1);
			newCache.TryAdd(assetsRemoved[assetId2], out var newDbPath2);
			Assert.AreEqual(newDbPath1, newDbPath2);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAddRestore_MultipleSlow_DifferentPartition() {
			var assetId1 = Guid.NewGuid();
			var assetId2 = Guid.NewGuid();

			IDictionary<Guid, object> assetsRemoved;
			{
				var oldCache = new PartitionedTemporalGuidCache(
					DATABASE_FOLDER_PATH,
					TimeSpan.FromSeconds(1),
					partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
					partPath => { Directory.Delete(partPath, true); }, // delete partition
					(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
					partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
				);

				oldCache.TryAdd(assetId1, 2, out var dbPath);
				oldCache.TryAdd(assetId2, 2, out var dbPath1);
				File.AppendAllText(Path.Combine(dbPath, "t1"), "1234");

				assetsRemoved = oldCache.Remove(4, out var cleared);
			}

			var newCache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetIdToCopy, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			newCache.TryAdd(assetsRemoved[assetId1], out var newDbPath1);
			Thread.Sleep(1100);
			newCache.TryAdd(assetsRemoved[assetId2], out var newDbPath2);
			Assert.AreNotEqual(newDbPath1, newDbPath2);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryAddRestore_Duplicate_ReturnsFalse() {
			var assetId1 = Guid.NewGuid();

			IDictionary<Guid, object> assetsRemoved;
			{
				var oldCache = new PartitionedTemporalGuidCache(
					DATABASE_FOLDER_PATH,
					TimeSpan.FromSeconds(1),
					partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
					partPath => { Directory.Delete(partPath, true); }, // delete partition
					(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
					partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
				);

				oldCache.TryAdd(assetId1, 2, out var dbPath);
				File.AppendAllText(Path.Combine(dbPath, "t1"), "12");

				assetsRemoved = oldCache.Remove(4, out var cleared);
			}

			var newCache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetIdToCopy, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			newCache.TryAdd(assetsRemoved[assetId1], out var newDbPath1);
			Assert.False(newCache.TryAdd(assetsRemoved[assetId1], out var newDbPath2));
		}

		#endregion

		#region TryGetAssetPartition

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryGetAssetPartition_GoodAsset_ReturnsTrue() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			var assetId1 = Guid.NewGuid();

			cache.TryAdd(assetId1, 2, out var dbPath);

			Assert.True(cache.TryGetAssetPartition(assetId1, out var dbPath2));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryGetAssetPartition_GoodAsset_SamePartition() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			var assetId1 = Guid.NewGuid();

			cache.TryAdd(assetId1, 2, out var dbPath);

			cache.TryGetAssetPartition(assetId1, out var dbPath2);

			Assert.AreEqual(dbPath, dbPath2);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryGetAssetPartition_MissingAsset_ReturnsFalse() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			Assert.False(cache.TryGetAssetPartition(Guid.NewGuid(), out var dbPath));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryGetAssetPartition_ZeroAsset_ReturnsFalse() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			Assert.False(cache.TryGetAssetPartition(Guid.Empty, out var dbPath));
		}

		#endregion

		#region Count

		[Test]
		public static void TestPartitionedTemporalGuidCache_Count_Fresh_IsZero() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			Assert.AreEqual(0, cache.Count);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Count_Returns3AfterAdding3() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);
			Assert.AreEqual(3, cache.Count);
		}

		#endregion

		#region Clear

		[Test]
		public static void TestPartitionedTemporalGuidCache_Clear_ResultsInCountZero() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			cache.Clear();
			Assert.AreEqual(0, cache.Count);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Clear_OnePartition_CallsDeleteCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { handlerCalled = true; }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			cache.Clear();
			Assert.True(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Clear_TwoPartitions_CallsDeleteCallback() {
			var handlerCalled = 0;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { handlerCalled++; }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			Thread.Sleep(1100);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath2);

			cache.Clear();
			Assert.AreEqual(2, handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Clear_TwoPartitions_CorrectPaths() {
			var pathsCalled = new List<string>();

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { pathsCalled.Add(partPath); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			Thread.Sleep(1100);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath2);

			cache.Clear();
			Assert.That(pathsCalled, Contains.Item(dbPath));
			Assert.That(pathsCalled, Contains.Item(dbPath2));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Clear_CallsCreateCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { handlerCalled = true; }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			cache.Clear();
			Assert.True(handlerCalled);
		}

		#endregion

		#region Contains

		[Test]
		public static void TestPartitionedTemporalGuidCache_Contains_DoesntFindUnknown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			Assert.False(cache.Contains(Guid.NewGuid()));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Contains_FindsKnown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);
			Assert.True(cache.Contains(guid));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Contains_DoesntFindRemovedItem() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);
			cache.TryRemove(guid);
			Assert.False(cache.Contains(guid));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Contains_Delayed_CallsCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 2, out var dbPath2);

			Thread.Sleep(1100);

			cache.Contains(guid);

			Assert.True(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Contains_Immediate_NoCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 2, out var dbPath2);

			cache.Contains(guid);

			Assert.False(handlerCalled);
		}

		#endregion

		#region AssetSize Get

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeGet_Known_SizeCorrect() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);
			Assert.AreEqual(2, cache.AssetSize(guid));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeGet_KnownDelayed_CallsCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 2, out var dbPath2);
			Thread.Sleep(1100);
			cache.AssetSize(guid);

			Assert.True(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeGet_KnownImmediate_NoCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.AssetSize(guid);

			Assert.False(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeGet_Unknown_Null() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);
			Assert.IsNull(cache.AssetSize(guid));
		}

		#endregion

		#region AssetSize Set

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeSet_Known_SizeUpdated() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);

			cache.AssetSize(guid, 10);

			Assert.AreEqual(10, cache.AssetSize(guid));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeSet_KnownDelayed_CallsCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(guid, 2, out var dbPath2);

			Thread.Sleep(1100);

			cache.AssetSize(guid, 10);

			Assert.True(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeSet_KnownImmediate_NoCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);

			cache.AssetSize(guid, 10);

			Assert.False(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_AssetSizeSet_Unknown_NoChange() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid1 = Guid.NewGuid();
			var guid2 = Guid.NewGuid();
			var guid3 = Guid.NewGuid();
			cache.TryAdd(guid1, 1, out var dbPath1);
			cache.TryAdd(guid3, 3, out var dbPath3);

			cache.AssetSize(guid2, 10);

			Assert.AreEqual(1, cache.AssetSize(guid1));
			Assert.AreEqual(3, cache.AssetSize(guid3));
		}

		#endregion

		#region ItemsWithPrefix

		[Test]
		public static void TestPartitionedTemporalGuidCache_ItemsWithPrefix_DoesntFindUnknown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 1, out var dbPath1);
			cache.TryAdd(Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6"), 2, out var dbPath2);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 3, out var dbPath3);
			Assert.IsEmpty(cache.ItemsWithPrefix("123"));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_ItemsWithPrefix_FindsSingularKnown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid, 1, out var dbPath1);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 2, out var dbPath2);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 3, out var dbPath3);

			var result = cache.ItemsWithPrefix(guid.ToString("N").Substring(0, 3));
			Assert.AreEqual(1, result.Count());
			Assert.That(result, Contains.Item(guid));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_ItemsWithPrefix_FindsMultipleKnown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid1 = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid1, 1, out var dbPath1);
			var guid2 = Guid.Parse("fcfdbe4a-1f93-4316-8c32-ae7a168a00e4");
			cache.TryAdd(guid2, 2, out var dbPath2);
			cache.TryAdd(Guid.Parse("67bdbe4a-1f93-4316-8c32-ae7a168a00e4"), 3, out var dbPath3);
			cache.TryAdd(Guid.Parse("06fd2e96-4c5e-4e87-918a-f217064330ea"), 4, out var dbPath4);

			var result = cache.ItemsWithPrefix(guid1.ToString("N").Substring(0, 3));
			Assert.AreEqual(2, result.Count());
			Assert.That(result, Contains.Item(guid1));
			Assert.That(result, Contains.Item(guid2));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_ItemsWithPrefix_Delayed_CallsCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid, 1, out var dbPath1);

			Thread.Sleep(1100);

			cache.ItemsWithPrefix(guid.ToString("N").Substring(0, 3));

			Assert.True(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_ItemsWithPrefix_Immediate_NoCopyCallback() {
			var handlerCalled = false;

			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { handlerCalled = true; }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.Parse("fcf84364-5fbd-4866-b8a7-35b93a20dbc6");
			cache.TryAdd(guid, 1, out var dbPath1);

			cache.ItemsWithPrefix(guid.ToString("N").Substring(0, 3));

			Assert.False(handlerCalled);
		}

		#endregion

		#region TryRemove

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryRemove_DoesntRemoveUnknown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 0, out var dbPath);
			Assert.False(cache.TryRemove(Guid.NewGuid()));
			Assert.AreEqual(1, cache.Count);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_TryRemove_DoesRemoveKnown() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { }, // Open or create partition
				partPath => { }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var guid = Guid.NewGuid();
			cache.TryAdd(Guid.NewGuid(), 1, out var dbPath1);
			cache.TryAdd(guid, 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 3, out var dbPath3);
			Assert.True(cache.TryRemove(guid));
			Assert.AreEqual(2, cache.Count);
			Assert.False(cache.Contains(guid));
		}

		#endregion

		#region Remove

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_Empty_ReturnsEmptyAndZero() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				Directory.Delete, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			var removed = cache.Remove(100, out var sizeCleared);
			Assert.IsEmpty(removed);
			Assert.AreEqual(0, sizeCleared);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_RemovesItemsFromCache() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				Directory.Delete, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 4, out var dbPath4);
			cache.TryAdd(Guid.NewGuid(), 8, out var dbPath8);

			cache.Remove(5, out var sizeCleared);

			Assert.Less(cache.Count, 3);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_CallsDeleteCallback() {
			var handlerCalled = false;
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromDays(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				partPath => { handlerCalled = true; Directory.Delete(partPath); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 4, out var dbPath4);
			cache.TryAdd(Guid.NewGuid(), 8, out var dbPath8);

			cache.Remove(5, out var sizeCleared);

			Assert.True(handlerCalled);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_OnlyDeletesExpectedPartition() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				partPath => { Directory.Delete(partPath, true); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);
			cache.TryAdd(Guid.NewGuid(), 2, out var dbPath2);
			cache.TryAdd(Guid.NewGuid(), 4, out var dbPath4);
			File.AppendAllText(Path.Combine(dbPath2, "t1"), "123456");
			Thread.Sleep(1100);
			cache.TryAdd(Guid.NewGuid(), 8, out var dbPath8);
			File.AppendAllText(Path.Combine(dbPath8, "t2"), "12345678");

			cache.Remove(5, out var sizeCleared);

			DirectoryAssert.DoesNotExist(dbPath2);
			DirectoryAssert.Exists(dbPath8);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_ReportsCorrectSizeCleared() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { Directory.CreateDirectory(partPath);}, // Open or create partition
				partPath => { Directory.Delete(partPath, true); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();

			cache.TryAdd(guidRemoved1, 2, out var dbPathR1);
			cache.TryAdd(guidRemoved2, 4, out var dbPathR2);
			File.AppendAllText(Path.Combine(dbPathR1, "t1"), "123456");
			Thread.Sleep(1100);
			cache.TryAdd(guidStays1, 8, out var dbPathS1);
			File.AppendAllText(Path.Combine(dbPathS1, "t2"), "12345678");

			cache.Remove(5, out var sizeCleared);

			Assert.AreEqual(6, sizeCleared);
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_ReturnsLeastRecentlyAccessedItems() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				partPath => { Directory.Delete(partPath, true); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();
			var guidStays2 = Guid.NewGuid();
			var guidStays3 = Guid.NewGuid();

			cache.TryAdd(guidStays1, 2, out var dbPathS1);
			cache.TryAdd(guidStays2, 2, out var dbPathS2);
			cache.TryAdd(guidRemoved1, 2, out var dbPathR1);
			cache.TryAdd(guidRemoved2, 2, out var dbPathR2);
			File.AppendAllText(Path.Combine(dbPathS1, "t1"), "12345678");
			Thread.Sleep(1100);
			cache.TryAdd(guidStays3, 2, out var dbPathS3);

			// Touch the timestamps
			cache.Contains(guidStays1);
			cache.ItemsWithPrefix(guidStays2.ToString("N").Substring(0, 3));
			File.AppendAllText(Path.Combine(dbPathS3, "t2"), "123456");

			var removed = cache.Remove(3, out var sizeCleared);

			Assert.That(removed.Keys, Contains.Item(guidRemoved1));
			Assert.That(removed.Keys, Contains.Item(guidRemoved2));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_CacheDoesntContainRemovedItems() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				partPath => { Directory.Delete(partPath, true); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();
			var guidStays2 = Guid.NewGuid();
			var guidStays3 = Guid.NewGuid();

			cache.TryAdd(guidStays1, 2, out var dbPathS1);
			cache.TryAdd(guidStays2, 2, out var dbPathS2);
			cache.TryAdd(guidRemoved1, 2, out var dbPathR1);
			cache.TryAdd(guidRemoved2, 2, out var dbPathR2);
			File.AppendAllText(Path.Combine(dbPathS1, "t1"), "12345678");
			Thread.Sleep(1100);
			cache.TryAdd(guidStays3, 2, out var dbPathS3);

			// Touch the timestamps
			cache.Contains(guidStays1);
			cache.ItemsWithPrefix(guidStays2.ToString("N").Substring(0, 3));
			File.AppendAllText(Path.Combine(dbPathS3, "t2"), "123456");

			cache.Remove(3, out var sizeCleared);

			Assert.False(cache.Contains(guidRemoved1));
			Assert.False(cache.Contains(guidRemoved2));
		}

		[Test]
		public static void TestPartitionedTemporalGuidCache_Remove_LeavesMostRecentlyAccessedItems() {
			var cache = new PartitionedTemporalGuidCache(
				DATABASE_FOLDER_PATH,
				TimeSpan.FromSeconds(1),
				partPath => { Directory.CreateDirectory(partPath); }, // Open or create partition
				partPath => { Directory.Delete(partPath, true); }, // delete partition
				(assetId, partPathSource, partPathDest) => { }, // copy asset between partitions
				partPath => { return null; } // partition found. Load it and return the asset IDs and sizes contained.
			);

			var guidRemoved1 = Guid.NewGuid();
			var guidRemoved2 = Guid.NewGuid();
			var guidStays1 = Guid.NewGuid();
			var guidStays2 = Guid.NewGuid();
			var guidStays3 = Guid.NewGuid();

			cache.TryAdd(guidStays1, 2, out var dbPathS1);
			cache.TryAdd(guidStays2, 2, out var dbPathS2);
			cache.TryAdd(guidRemoved1, 2, out var dbPathR1);
			cache.TryAdd(guidRemoved2, 2, out var dbPathR2);
			File.AppendAllText(Path.Combine(dbPathS1, "t1"), "12345678");
			Thread.Sleep(1100);
			cache.TryAdd(guidStays3, 2, out var dbPathS3);

			// Touch the timestamps
			cache.Contains(guidStays1);
			cache.ItemsWithPrefix(guidStays2.ToString("N").Substring(0, 3));
			File.AppendAllText(Path.Combine(dbPathS3, "t2"), "123456");

			cache.Remove(3, out var sizeCleared);

			Assert.True(cache.Contains(guidStays1));
			Assert.True(cache.Contains(guidStays2));
			Assert.True(cache.Contains(guidStays3));
		}

		#endregion
	}
}
