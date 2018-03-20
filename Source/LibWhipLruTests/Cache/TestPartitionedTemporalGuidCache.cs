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
using System.IO;
using System.Linq;
using System.Threading;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public static class TestPartitionedTemporalGuidCache {
		public static readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_ac_lmdb";

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

		#region Ctor

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
	}
}
