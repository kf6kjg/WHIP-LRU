﻿// TestAssetLocalStorageLmdb.cs
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
	public static class TestAssetLocalStorageLmdbCtor {
		public static readonly string DATABASE_FOLDER_PATH = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_ac_lmdb");
		public const ulong DATABASE_MAX_SIZE_BYTES = uint.MaxValue/*Min value to get tests to run*/;

		private static ChattelConfiguration _chattelConfigRead;

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
			_chattelConfigRead = new ChattelConfiguration(DATABASE_FOLDER_PATH);
		}

		[SetUp]
		public static void BeforeEveryTest() {
			RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		[TearDown]
		public static void CleanupAfterEveryTest() {
			CleanLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		#region Ctor2

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DoesntThrow() {
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBPathBlank_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration("", assetServer: null);
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBPathNull_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration(null, assetServer: null);
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBSize0_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					0
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBSizeJustTooSmall_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					uint.MaxValue - 1
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBSizeMinimum_DoesntThrow() {
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					uint.MaxValue
				)) { }
			});
		}

		// Maxmimum value test invalid: no computer has enough memory to run it.

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBSizeJustOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					long.MaxValue + 1UL
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_DBSizeOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					ulong.MaxValue
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor2_RestoresIndex() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			using (var localStorage = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			)) {
				IChattelLocalStorage localStorageViaInterface = localStorage;
				localStorageViaInterface.StoreAsset(asset);
			}

			using (var localStorage = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES
			)) {
				Assert.True(localStorage.Contains(asset.Id));
			}
		}

		#endregion

		#region Ctor3, allow asset removal

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DoesntThrow() {
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBPathBlank_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration("", assetServer: null);
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBPathNull_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration(null, assetServer: null);
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBSize0_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					0,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBSizeJustTooSmall_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					uint.MaxValue - 1,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBSizeMinimum_DoesntThrow() {
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					uint.MaxValue,
					true
				)) { }
			});
		}

		// Maxmimum value test invalid: no computer has enough memory to run it.

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBSizeJustOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					long.MaxValue + 1UL,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_DBSizeOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					ulong.MaxValue,
					true
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3AllowLruPurge_RestoresIndex() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			using (var localStorage = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES,
					true
			)) {
				IChattelLocalStorage localStorageViaInterface = localStorage;
				localStorageViaInterface.StoreAsset(asset);
			}

			using (var localStorage = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES,
					true
			)) {
				Assert.True(localStorage.Contains(asset.Id));
			}
		}

		#endregion

		#region Ctor3, no asset removal

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DoesntThrow() {
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBPathBlank_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration("", assetServer: null);
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBPathNull_DoesntThrow() {
			var chattelConfigRead = new ChattelConfiguration(null, assetServer: null);
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					DATABASE_MAX_SIZE_BYTES,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBSize0_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					0,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBSizeJustTooSmall_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					uint.MaxValue - 1,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBSizeMinimum_DoesntThrow() {
			Assert.DoesNotThrow(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					uint.MaxValue,
					false
				)) { }
			});
		}

		// Maxmimum value test invalid: no computer has enough memory to run it.

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBSizeJustOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					long.MaxValue + 1UL,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_DBSizeOversize_ArgumentOutOfRangeException() {
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new AssetLocalStorageLmdb(
					_chattelConfigRead,
					ulong.MaxValue,
					false
				)) { }
			});
		}

		[Test]
		public static void TestAssetLocalStorageLmdb_Ctor3NoLruPurge_RestoresIndex() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			using (var localStorage = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES,
				false
			)) {
				IChattelLocalStorage localStorageViaInterface = localStorage;
				localStorageViaInterface.StoreAsset(asset);
			}

			using (var localStorage = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES,
				false
			)) {
				Assert.True(localStorage.Contains(asset.Id));
			}
		}

		#endregion
	}
}
