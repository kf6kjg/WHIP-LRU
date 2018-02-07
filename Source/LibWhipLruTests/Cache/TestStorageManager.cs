// TestStorageManager.cs
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
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using NSubstitute;
using NUnit.Framework;

#pragma warning disable RECS0026 // Possible unassigned object created by 'new'

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public class TestStorageManager {
		public static readonly string WRITE_CACHE_FILE_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_sm.whipwcache";
		public const uint WRITE_CACHE_MAX_RECORD_COUNT = 8;

		private AssetLocalStorageLmdb _readerCache;
		private ChattelReader _chattelReader;
		private ChattelWriter _chattelWriter;


		[SetUp]
		public void BeforeEveryTest() {
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				File.Delete(WRITE_CACHE_FILE_PATH);
			}
			catch {
			}
			try {
				Directory.Delete(TestAssetCacheLmdb.DATABASE_FOLDER_PATH, true);
			}
			catch {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			Directory.CreateDirectory(TestAssetCacheLmdb.DATABASE_FOLDER_PATH);
			var chattelConfigRead = new ChattelConfiguration(TestAssetCacheLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, (IAssetServer)null);
			var chattelConfigWrite = new ChattelConfiguration(TestAssetCacheLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, (IAssetServer)null);

			_readerCache = new AssetLocalStorageLmdb(chattelConfigRead, TestAssetCacheLmdb.DATABASE_MAX_SIZE_BYTES);
			_chattelReader = new ChattelReader(chattelConfigRead, _readerCache);
			_chattelWriter = new ChattelWriter(chattelConfigWrite, _readerCache);
		}

		[TearDown]
		public void CleanupAfterEveryTest() {
			IDisposable readerDispose = _readerCache;
			_readerCache = null;
			readerDispose.Dispose();

			File.Delete(WRITE_CACHE_FILE_PATH);
			Directory.Delete(TestAssetCacheLmdb.DATABASE_FOLDER_PATH, true);
		}

		#region Ctor

		[Test]
		public void TestStorageManager_Ctor_DoesNotThrow() {
			Assert.DoesNotThrow(() => new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NullCache_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				null,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NullReader_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				null,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NullWriter_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				null
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NegativeTime_DoesntThrow() {
			Assert.DoesNotThrow(() => new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(-1),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_ZeroTime_DoesntThrow() {
			Assert.DoesNotThrow(() => new StorageManager(
				_readerCache,
				TimeSpan.Zero,
				_chattelReader,
				_chattelWriter
			));
		}

		#endregion

		// TODO: Check Asset

		#region Putting assets

		[Test]
		public void TestStorageManager_StoreAsset_AssetNull_ArgumentNullException() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentNullException>(() => mgr.StoreAsset(null, result => { }));
		}

		[Test]
		public void TestStorageManager_StoreAsset_EmptyId_ArgumentException() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.Empty,
			};

			Assert.Throws<ArgumentException>(() => mgr.StoreAsset(asset, result => { }));
		}

		[Test]
		public void TestStorageManager_StoreAsset_DoesntThrowFirstTime() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { }));
		}

		[Test]
		public void TestStorageManager_StoreAsset_DoesntThrowDuplicate() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			mgr.StoreAsset(asset, result => { });
			Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { }));
		}

		[Test]
		public void TestStorageManager_StoreAsset_DoesntThrowMultiple() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}, result => { }));
			Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}, result => { }));
			Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}, result => { }));
			Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}, result => { }));
		}

		#endregion

		#region Get Assets

		[Test]
		public void TestStorageManager_GetAsset_EmptyId_ArgumentException() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.GetAsset(Guid.Empty, result => { }, () => { }));
		}

		[Test]
		public void TestStorageManager_GetAsset_Unknown_DoesntThrow() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.GetAsset(Guid.NewGuid(), result => { }, () => { }));
		}

		[Test]
		public void TestStorageManager_GetAsset_Unknown_IsNull() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			mgr.GetAsset(Guid.NewGuid(), Assert.IsNull, Assert.Fail);
		}

		[Test]
		public void TestStorageManager_GetAsset_Known_DoesntThrow() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			Assert.DoesNotThrow(() => mgr.GetAsset(id, result => { }, () => { }));
		}

		[Test]
		public void TestStorageManager_GetAsset_Known_IsNotNull() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			mgr.GetAsset(id, result => { }, () => { });

			mgr.GetAsset(id, Assert.IsNotNull, Assert.Fail);
		}

		[Test]
		public void TestStorageManager_GetAsset_Known_HasSameId() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			mgr.GetAsset(id, result => { Assert.AreEqual(id, result.Id); }, Assert.Fail);


		}

		[Test]
		public void TestStorageManager_GetAsset_Known_IsIdentical() {
			var mgr = new StorageManager(
				_readerCache,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var baseAsset = new StratusAsset {
				CreateTime = DateTime.UtcNow,
				Data = new byte[] { 128, 42 },
				Description = RandomUtil.StringUTF8(128),
				Id = Guid.NewGuid(),
				Local = false,
				Name = RandomUtil.StringUTF8(32),
				StorageFlags = RandomUtil.NextUInt(),
				Temporary = false,
				Type = RandomUtil.NextSByte(),
			};

			mgr.StoreAsset(baseAsset, result => { });

			mgr.GetAsset(baseAsset.Id, result => {
				Assert.AreEqual(baseAsset.CreateTime, result.CreateTime);
				Assert.AreEqual(baseAsset.Description, result.Description);
				Assert.AreEqual(baseAsset.Data, result.Data);
				Assert.AreEqual(baseAsset.Id, result.Id);
				Assert.AreEqual(baseAsset.Local, result.Local);
				Assert.AreEqual(baseAsset.Name, result.Name);
				Assert.AreEqual(baseAsset.StorageFlags, result.StorageFlags);
				Assert.AreEqual(baseAsset.Temporary, result.Temporary);
				Assert.AreEqual(baseAsset.Type, result.Type);
			}, Assert.Fail);
		}

		[Test]
		public void TestStorageManager_GetAsset_SingleNoExist_CallsServerRequestAsset() {
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetCacheLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			var cache = new AssetLocalStorageLmdb(config, TestAssetCacheLmdb.DATABASE_MAX_SIZE_BYTES);
			var reader = new ChattelReader(config, cache, false);
			var writer = new ChattelWriter(config, cache, false);

			var assetId = Guid.NewGuid();

			var mgr = new StorageManager(
				cache,
				TimeSpan.FromMinutes(2),
				reader,
				writer
			);
			mgr.GetAsset(assetId, result => { }, () => { });

			server.Received(1).RequestAssetSync(assetId);
		}

		[Test]
		public void TestStorageManager_GetAsset_DoubleNoExist_CallsServerRequestOnlyOnce() {
			// Tests the existence of a negative cache.
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetCacheLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			var cache = new AssetLocalStorageLmdb(config, TestAssetCacheLmdb.DATABASE_MAX_SIZE_BYTES);
			var reader = new ChattelReader(config, cache, false);
			var writer = new ChattelWriter(config, cache, false);

			var assetId = Guid.NewGuid();

			var mgr = new StorageManager(
				cache,
				TimeSpan.FromMinutes(2),
				reader,
				writer
			);
			mgr.GetAsset(assetId, result => { }, () => { });
			mgr.GetAsset(assetId, result => { }, () => { });

			server.Received(1).RequestAssetSync(assetId);
		}

		#endregion
	}
}
