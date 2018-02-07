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
using System.IO;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public class TestAssetCacheLmdbCtor {
		public static readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_ac_lmdb";
		public const ulong DATABASE_MAX_SIZE_BYTES = uint.MaxValue/*Min value to get tests to run*/;

		private ChattelConfiguration _chattelConfigRead;

		public static void CleanCacheFolder(string dbFolderPath, string writeCacheFilePath) {
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

		public static void RebuildCacheFolder(string dbFolderPath, string writeCacheFilePath) {
			CleanCacheFolder(dbFolderPath, writeCacheFilePath);
			Directory.CreateDirectory(dbFolderPath);
		}

		[OneTimeSetUp]
		public void Startup() {
			// Folder has to be there or the config fails.
			RebuildCacheFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
			_chattelConfigRead = new ChattelConfiguration(DATABASE_FOLDER_PATH, assetServer: null);
		}

		[SetUp]
		public void BeforeEveryTest() {
			RebuildCacheFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		[TearDown]
		public void CleanupAfterEveryTest() {
			CleanCacheFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DoesntThrow() {
			Assert.DoesNotThrow(() => new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBPathBlank_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration("", assetServer: null);
			Assert.DoesNotThrow(() => new AssetLocalStorageLmdb(
				chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBPathNull_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration(null, assetServer: null);
			Assert.DoesNotThrow(() => new AssetLocalStorageLmdb(
				chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBSize0_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new AssetLocalStorageLmdb(
				_chattelConfigRead,
				0
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBSizeJustTooSmall_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new AssetLocalStorageLmdb(
				_chattelConfigRead,
				uint.MaxValue - 1
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBSizeMinimum_DoesntThrow() {
			Assert.DoesNotThrow(() => new AssetLocalStorageLmdb(
				_chattelConfigRead,
				uint.MaxValue
			));
		}

		// Maxmimum value test invalid: no computer has enough memory to run it.

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBSizeJustOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new AssetLocalStorageLmdb(
				_chattelConfigRead,
				long.MaxValue + 1UL
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_DBSizeOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new AssetLocalStorageLmdb(
				_chattelConfigRead,
				ulong.MaxValue
			));
		}

		[Test]
		public void TestAssetCacheLmdb_Ctor_RestoresIndex() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			using (var cache = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			)) {
				IChattelLocalStorage cacheViaInterface = cache;
				cacheViaInterface.StoreAsset(asset);
			}

			using (var cache = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			)) {
				Assert.True(cache.Contains(asset.Id));
			}
		}
	}
}
