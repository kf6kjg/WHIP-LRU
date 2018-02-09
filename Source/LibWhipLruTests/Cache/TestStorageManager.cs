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
using System.Threading;
using System.Threading.Tasks;
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

		private AssetLocalStorageLmdb _readerLocalStorage;
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
				Directory.Delete(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, true);
			}
			catch {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			Directory.CreateDirectory(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH);
			var chattelConfigRead = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, (IAssetServer)null);
			var chattelConfigWrite = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, (IAssetServer)null);

			_readerLocalStorage = new AssetLocalStorageLmdb(chattelConfigRead, TestAssetLocalStorageLmdb.DATABASE_MAX_SIZE_BYTES);
			_chattelReader = new ChattelReader(chattelConfigRead, _readerLocalStorage);
			_chattelWriter = new ChattelWriter(chattelConfigWrite, _readerLocalStorage);
		}

		[TearDown]
		public void CleanupAfterEveryTest() {
			IDisposable readerDispose = _readerLocalStorage;
			_readerLocalStorage = null;
			readerDispose.Dispose();

			File.Delete(WRITE_CACHE_FILE_PATH);
			Directory.Delete(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, true);
		}

		#region Ctor

		[Test]
		public void TestStorageManager_Ctor_DoesNotThrow() {
			Assert.DoesNotThrow(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NullLocalStorage_ArgumentNullException() {
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
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				null,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NullWriter_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				null
			));
		}

		[Test]
		public void TestStorageManager_Ctor_NegativeTime_DoesntThrow() {
			Assert.DoesNotThrow(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(-1),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public void TestStorageManager_Ctor_ZeroTime_DoesntThrow() {
			Assert.DoesNotThrow(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.Zero,
				_chattelReader,
				_chattelWriter
			));
		}

		#endregion

		#region Check asset

		[Test]
		public void TestStorageManager_CheckAsset_EmptyId_ArgumentException() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.CheckAsset(Guid.Empty, result => { }));
		}

		[Test]
		public void TestStorageManager_CheckAsset_Unknown_DoesntThrow() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.CheckAsset(Guid.NewGuid(), result => { }));
		}

		[Test]
		[Timeout(1000)]
		public void TestStorageManager_CheckAsset_Unknown_CallsCallback() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var callbackWasCalled = false;
			var stopWaitHandle = new AutoResetEvent(false);

			mgr.CheckAsset(Guid.NewGuid(), result => { callbackWasCalled = true; stopWaitHandle.Set(); });

			stopWaitHandle.WaitOne();

			Assert.True(callbackWasCalled);
		}

		[Test]
		public void TestStorageManager_CheckAsset_Unknown_IsFalse() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			mgr.CheckAsset(Guid.NewGuid(), Assert.False);
		}

		[Test]
		public void TestStorageManager_CheckAsset_Known_DoesntThrow() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			Assert.DoesNotThrow(() => mgr.CheckAsset(id, result => { }));
		}

		[Test]
		[Timeout(1000)]
		public void TestStorageManager_CheckAsset_Known_CallsCallback() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			var callbackWasCalled = false;
			var stopWaitHandle = new AutoResetEvent(false);

			mgr.CheckAsset(id, result => { callbackWasCalled = true; stopWaitHandle.Set(); });

			stopWaitHandle.WaitOne();

			Assert.True(callbackWasCalled);
		}

		[Test]
		public void TestStorageManager_CheckAsset_Known_IsTrue() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			mgr.CheckAsset(id, Assert.IsTrue);
		}

		[Test]
		public void TestStorageManager_CheckAsset_SingleNoExist_CallsServerRequestAsset() {
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			var localStorage = new AssetLocalStorageLmdb(config, TestAssetLocalStorageLmdb.DATABASE_MAX_SIZE_BYTES);
			var reader = new ChattelReader(config, localStorage, false);
			var writer = new ChattelWriter(config, localStorage, false);

			var assetId = Guid.NewGuid();

			var mgr = new StorageManager(
				localStorage,
				TimeSpan.FromMinutes(2),
				reader,
				writer
			);
			mgr.CheckAsset(assetId, result => { });

			server.Received(1).RequestAssetSync(assetId);
		}

		[Test]
		public void TestStorageManager_CheckAsset_DoubleNoExist_CallsServerRequestOnlyOnce() {
			// Tests the existence of a negative cache.
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			var localStorage = new AssetLocalStorageLmdb(config, TestAssetLocalStorageLmdb.DATABASE_MAX_SIZE_BYTES);
			var reader = new ChattelReader(config, localStorage, false);
			var writer = new ChattelWriter(config, localStorage, false);

			var assetId = Guid.NewGuid();

			var mgr = new StorageManager(
				localStorage,
				TimeSpan.FromMinutes(2),
				reader,
				writer
			);
			mgr.CheckAsset(assetId, result => { });
			mgr.CheckAsset(assetId, result => { });

			server.Received(1).RequestAssetSync(assetId);
		}

		#endregion

		#region Putting assets

		[Test]
		public void TestStorageManager_StoreAsset_AssetNull_ArgumentNullException() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentNullException>(() => mgr.StoreAsset(null, result => { }));
		}

		[Test]
		public void TestStorageManager_StoreAsset_EmptyId_ArgumentException() {
			var mgr = new StorageManager(
				_readerLocalStorage,
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
				_readerLocalStorage,
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
				_readerLocalStorage,
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
		public void TestStorageManager_StoreAsset_DoesntThrowDuplicateParallel() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			Parallel.Invoke(
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(asset, result => { }))
			);
		}

		[Test]
		public void TestStorageManager_StoreAsset_DoesntThrowMultipleParallel() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Parallel.Invoke(
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
					Id = Guid.NewGuid(),
				}, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
					Id = Guid.NewGuid(),
				}, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
					Id = Guid.NewGuid(),
				}, result => { })),
				() => Assert.DoesNotThrow(() => mgr.StoreAsset(new StratusAsset {
					Id = Guid.NewGuid(),
				}, result => { }))
			);
		}

		// TODO: checks for send to server

		#endregion

		#region Get Assets

		[Test]
		public void TestStorageManager_GetAsset_EmptyId_ArgumentException() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.GetAsset(Guid.Empty, result => { }, () => { }));
		}

		[Test]
		public void TestStorageManager_GetAsset_Unknown_DoesntThrow() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.GetAsset(Guid.NewGuid(), result => { }, () => { }));
		}

		[Test]
		[Timeout(1000)]
		public void TestStorageManager_GetAsset_Unknown_CallsFailureCallback() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var callbackWasCalled = false;
			var stopWaitHandle = new AutoResetEvent(false);

			mgr.GetAsset(Guid.NewGuid(), result => { }, () => { callbackWasCalled = true; stopWaitHandle.Set(); });

			stopWaitHandle.WaitOne();

			Assert.True(callbackWasCalled);
		}

		[Test]
		public void TestStorageManager_GetAsset_Unknown_IsNull() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			mgr.GetAsset(Guid.NewGuid(), Assert.IsNull, Assert.Fail);
		}

		[Test]
		public void TestStorageManager_GetAsset_Known_DoesntThrow() {
			var mgr = new StorageManager(
				_readerLocalStorage,
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
		[Timeout(1000)]
		public void TestStorageManager_GetAsset_Known_CallsSuccessCallback() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });


			var callbackWasCalled = false;
			var stopWaitHandle = new AutoResetEvent(false);

			mgr.GetAsset(id, result => { callbackWasCalled = true; stopWaitHandle.Set(); }, Assert.Fail);

			stopWaitHandle.WaitOne();

			Assert.True(callbackWasCalled);
		}

		[Test]
		public void TestStorageManager_GetAsset_Known_IsNotNull() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var id = Guid.NewGuid();

			mgr.StoreAsset(new StratusAsset {
				Id = id,
			}, result => { });

			mgr.GetAsset(id, Assert.IsNotNull, Assert.Fail);
		}

		[Test]
		public void TestStorageManager_GetAsset_Known_HasSameId() {
			var mgr = new StorageManager(
				_readerLocalStorage,
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
				_readerLocalStorage,
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
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			var localStorage = new AssetLocalStorageLmdb(config, TestAssetLocalStorageLmdb.DATABASE_MAX_SIZE_BYTES);
			var reader = new ChattelReader(config, localStorage, false);
			var writer = new ChattelWriter(config, localStorage, false);

			var assetId = Guid.NewGuid();

			var mgr = new StorageManager(
				localStorage,
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
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			var localStorage = new AssetLocalStorageLmdb(config, TestAssetLocalStorageLmdb.DATABASE_MAX_SIZE_BYTES);
			var reader = new ChattelReader(config, localStorage, false);
			var writer = new ChattelWriter(config, localStorage, false);

			var assetId = Guid.NewGuid();

			var mgr = new StorageManager(
				localStorage,
				TimeSpan.FromMinutes(2),
				reader,
				writer
			);
			mgr.GetAsset(assetId, result => { }, () => { });
			mgr.GetAsset(assetId, result => { }, () => { });

			server.Received(1).RequestAssetSync(assetId);
		}

		#endregion

		#region Purge All Assets marked local

		// TODO: Purge should only remove assets that are marked as local

		#endregion

		#region Purge Asset

		[Test]
		public void TestStorageManager_PurgeAsset_EmptyId_ArgumentException() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.PurgeAsset(Guid.Empty, result => { }));
		}

		[Test]
		public void TestStorageManager_PurgeAsset_DoesntThrowFirstTime() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.PurgeAsset(Guid.NewGuid(), result => { }));
		}

		[Test]
		public void TestStorageManager_PurgeAsset_DoesntThrowDuplicate() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var assetId = Guid.NewGuid();

			mgr.PurgeAsset(assetId, result => { });
			Assert.DoesNotThrow(() => mgr.PurgeAsset(assetId, result => { }));
		}

		[Test]
		public void TestStorageManager_PurgeAsset_DoesntThrowMultiple() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.PurgeAsset(Guid.NewGuid(), result => { }));
			Assert.DoesNotThrow(() => mgr.PurgeAsset(Guid.NewGuid(), result => { }));
			Assert.DoesNotThrow(() => mgr.PurgeAsset(Guid.NewGuid(), result => { }));
			Assert.DoesNotThrow(() => mgr.PurgeAsset(Guid.NewGuid(), result => { }));
		}

		[Test]
		public void TestStorageManager_PurgeAsset_Unknown_CallsCallback() {
			var localStorageLmdb = Substitute.For<AssetLocalStorageLmdb>();
			IChattelLocalStorage chattelLocalStorage = localStorageLmdb;

			var mgr = new StorageManager(
				localStorageLmdb,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var wait = new AutoResetEvent(false);
			var callbackCalled = false;

			mgr.PurgeAsset(Guid.NewGuid(), result => { callbackCalled = true; wait.Set(); });

			wait.WaitOne();

			Assert.True(callbackCalled);
		}

		[Test]
		public void TestStorageManager_PurgeAsset_Known_CallsCallback() {
			var localStorageLmdb = Substitute.For<AssetLocalStorageLmdb>();
			IChattelLocalStorage chattelLocalStorage = localStorageLmdb;

			var mgr = new StorageManager(
				localStorageLmdb,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			mgr.StoreAsset(asset, result => { });

			var wait = new AutoResetEvent(false);
			var callbackCalled = false;

			mgr.PurgeAsset(asset.Id, result => { callbackCalled = true; wait.Set(); });

			wait.WaitOne();

			Assert.True(callbackCalled);
		}

		[Test]
		public void TestStorageManager_PurgeAsset_Unknown_CallsLocalStoragePurge() {
			var localStorageLmdb = Substitute.For<AssetLocalStorageLmdb>();
			IChattelLocalStorage chattelLocalStorage = localStorageLmdb;

			var mgr = new StorageManager(
				localStorageLmdb,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var assetId = Guid.NewGuid();

			mgr.PurgeAsset(assetId, result => { });

			chattelLocalStorage.Received(1).Purge(assetId);
		}

		[Test]
		public void TestStorageManager_PurgeAsset_Known_CallsLocalStoragePurge() {
			var localStorageLmdb = Substitute.For<AssetLocalStorageLmdb>();
			IChattelLocalStorage chattelLocalStorage = localStorageLmdb;

			var mgr = new StorageManager(
				localStorageLmdb,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			mgr.StoreAsset(asset, result => { });

			mgr.PurgeAsset(asset.Id, result => { });

			chattelLocalStorage.Received(1).Purge(asset.Id);
		}

		[Test]
		[Timeout(1000)]
		public void TestStorageManager_PurgeAsset_Unknown_ReturnsNotFoundLocally() {
			var localStorageLmdb = Substitute.For<AssetLocalStorageLmdb>();
			IChattelLocalStorage chattelLocalStorage = localStorageLmdb;

			var mgr = new StorageManager(
				localStorageLmdb,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var assetId = Guid.NewGuid();

			var wait = new AutoResetEvent(false);
			var status = false;

			mgr.PurgeAsset(assetId, result => { status = result == StorageManager.PurgeResult.NOT_FOUND_LOCALLY; wait.Set(); });

			wait.WaitOne();

			Assert.True(status);
		}

		[Test]
		[Timeout(1000)]
		public void TestStorageManager_PurgeAsset_Known_ReturnsDone() {
			var localStorageLmdb = Substitute.For<AssetLocalStorageLmdb>();
			IChattelLocalStorage chattelLocalStorage = localStorageLmdb;

			var mgr = new StorageManager(
				localStorageLmdb,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var assetId = Guid.NewGuid();

			var wait = new AutoResetEvent(false);
			var status = false;

			mgr.PurgeAsset(assetId, result => { status = result == StorageManager.PurgeResult.DONE; wait.Set(); });

			wait.WaitOne();

			Assert.True(status);
		}

		[Test]
		public void TestStorageManager_PurgeAsset_DoesntRemoveOtherAsset() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			mgr.StoreAsset(asset, result => { });

			mgr.PurgeAsset(Guid.NewGuid(), result => { });

			Assert.True(_readerLocalStorage.AssetOnDisk(asset.Id));
		}

		[Test]
		public void TestStorageManager_PurgeAsset_RemovesAsset() {
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			mgr.StoreAsset(asset, result => { });

			mgr.PurgeAsset(asset.Id, result => { });

			Assert.False(_readerLocalStorage.AssetOnDisk(asset.Id));
		}

		// remote purge should not be called - however there's no API for that ATM in Chattel so it can't be called.

		#endregion
	}
}
