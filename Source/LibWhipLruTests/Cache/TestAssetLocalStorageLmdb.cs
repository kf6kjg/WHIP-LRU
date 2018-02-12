// TestAssetLocalStorageLmdb.cs
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
	public static class TestAssetLocalStorageLmdb {
		public static readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_ac_lmdb";
		public const ulong DATABASE_MAX_SIZE_BYTES = uint.MaxValue/*Min value to get tests to run*/;

		private static ChattelConfiguration _chattelConfigRead;
		private static AssetLocalStorageLmdb _localStorageLmdb;
		private static IChattelLocalStorage _localStorage;

		[OneTimeSetUp]
		public static void Startup() {
			// Folder has to be there or the config fails.
			TestAssetLocalStorageLmdbCtor.RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
			_chattelConfigRead = new ChattelConfiguration(DATABASE_FOLDER_PATH, assetServer: null);
		}

		[SetUp]
		public static void BeforeEveryTest() {
			TestAssetLocalStorageLmdbCtor.RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);

			_localStorage = _localStorageLmdb = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			);
		}

		[TearDown]
		public static void CleanupAfterEveryTest() {
			_localStorage = null;
			IDisposable localStorageDisposal = _localStorageLmdb;
			_localStorageLmdb = null;
			localStorageDisposal.Dispose();

			TestAssetLocalStorageLmdbCtor.CleanLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		#region Contains and AssetOnDisk

		// These two methods are, or are nearly, simple pass-throughs for the OrderedGuidCache.  Thus they are tested in that class's tests.

		#endregion

		#region Store Asset

		[Test]
		public static void TestAssetLocalStorageLmdb_StoreAsset_AssetOnDiskImmediately() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(asset);
			Assert.True(_localStorageLmdb.AssetOnDisk(asset.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_StoreAsset_ContainsImmediately() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(asset);
			Assert.True(_localStorageLmdb.Contains(asset.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_StoreAsset_DoesntThrow() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			Assert.DoesNotThrow(() => _localStorage.StoreAsset(asset));
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_StoreAsset_Null_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => _localStorage.StoreAsset(null));
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_StoreAsset_EmpyId_ArgumentException() {
			var asset = new StratusAsset {
				Id = Guid.Empty,
			};

			Assert.Throws<ArgumentException>(() => _localStorage.StoreAsset(asset));
		}

		#endregion
	}
}
