// TestAssetCacheLmdb.cs
//
// Author:
//       Ricky C <>
//
// Copyright (c) 2018 
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
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public class TestAssetCacheLmdb {
		public static readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_ac_lmdb";
		public const ulong DATABASE_MAX_SIZE_BYTES = uint.MaxValue/*Min value to get tests to run*/;

		private ChattelConfiguration _chattelConfigRead;
		private AssetCacheLmdb _cacheLmdb;
		private IChattelCache _cache;

		[OneTimeSetUp]
		public void Startup() {
			// Folder has to be there or the config fails.
			TestAssetCacheLmdbCtor.RebuildCacheFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
			_chattelConfigRead = new ChattelConfiguration(DATABASE_FOLDER_PATH);
		}

		[SetUp]
		public void BeforeEveryTest() {
			TestAssetCacheLmdbCtor.RebuildCacheFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);

			_cache = _cacheLmdb = new AssetCacheLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			);
		}

		[TearDown]
		public void CleanupAfterEveryTest() {
			_cache = null;
			IDisposable cacheDisposal = _cacheLmdb;
			_cacheLmdb = null;
			cacheDisposal.Dispose();

			TestAssetCacheLmdbCtor.CleanCacheFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		#region Contains and AssetOnDisk

		// These two methods are, or are nearly, simple pass-throughs for the OrderedGuidCache.  Thus they are tested in that class's tests.

		#endregion

		#region Cache Asset

		[Test]
		public void TestAssetCacheLmdb_CacheAsset_AssetOnDiskImmediately() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_cache.CacheAsset(asset);
			Assert.True(_cacheLmdb.AssetOnDisk(asset.Id));
		}

		[Test]
		public void TestAssetCacheLmdb_CacheAsset_ContainsImmediately() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_cache.CacheAsset(asset);
			Assert.True(_cacheLmdb.Contains(asset.Id));
		}

		[Test]
		public void TestAssetCacheLmdb_CacheAsset_DoesntThrow() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			Assert.DoesNotThrow(() => _cache.CacheAsset(asset));
		}

		[Test]
		public void TestAssetCacheLmdb_CacheAsset_Null_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => _cache.CacheAsset(null));
		}

		[Test]
		public void TestAssetCacheLmdb_CacheAsset_EmpyId_ArgumentException() {
			var asset = new StratusAsset {
				Id = Guid.Empty,
			};

			Assert.Throws<ArgumentException>(() => _cache.CacheAsset(asset));
		}

		#endregion
	}
}
