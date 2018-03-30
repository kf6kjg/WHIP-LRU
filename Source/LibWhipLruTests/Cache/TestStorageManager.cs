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
	[NonParallelizable]
	public static class TestStorageManager {
		public static readonly string WRITE_CACHE_FILE_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_sm.whipwcache";
		public const uint WRITE_CACHE_MAX_RECORD_COUNT = 8;

		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private static ChattelConfiguration _chattelConfigRead;
		private static ChattelConfiguration _chattelConfigWrite;
		private static AssetLocalStorageLmdbPartitionedLRU _readerLocalStorage;
		private static ChattelReader _chattelReader;
		private static ChattelWriter _chattelWriter;

		[OneTimeSetUp]
		public static void Startup() {
			// Folder has to be there or the config fails.
			TestAssetLocalStorageLmdbPartitionedLRUCtor.RebuildLocalStorageFolder(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH);
			_chattelConfigRead = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, (IAssetServer)null);
			_chattelConfigWrite = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, (IAssetServer)null);

		}

		[SetUp]
		public static void BeforeEveryTest() {
			LOG.Info($"Executing {nameof(BeforeEveryTest)}");
			TestAssetLocalStorageLmdbPartitionedLRUCtor.RebuildLocalStorageFolder(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH);

			_readerLocalStorage = new AssetLocalStorageLmdbPartitionedLRU(
				_chattelConfigRead,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_MAX_SIZE_BYTES,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_PARTITION_INTERVAL
			);
			_chattelReader = new ChattelReader(_chattelConfigRead, _readerLocalStorage);
			_chattelWriter = new ChattelWriter(_chattelConfigWrite, _readerLocalStorage);
		}

		[TearDown]
		public static void CleanupAfterEveryTest() {
			LOG.Info($"Executing {nameof(CleanupAfterEveryTest)}");
			_chattelReader = null;
			_chattelWriter = null;

			IDisposable localStorageDisposal = _readerLocalStorage;
			_readerLocalStorage = null;
			localStorageDisposal.Dispose();

			TestAssetLocalStorageLmdbPartitionedLRUCtor.CleanLocalStorageFolder(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH);
		}

		#region Ctor

		[Test]
		public static void TestStorageManager_Ctor_DoesNotThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_Ctor_DoesNotThrow)}");
			Assert.DoesNotThrow(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public static void TestStorageManager_Ctor_NullLocalStorage_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestStorageManager_Ctor_NullLocalStorage_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				null,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public static void TestStorageManager_Ctor_NullReader_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestStorageManager_Ctor_NullReader_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				null,
				_chattelWriter
			));
		}

		[Test]
		public static void TestStorageManager_Ctor_NullWriter_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestStorageManager_Ctor_NullWriter_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				null
			));
		}

		[Test]
		public static void TestStorageManager_Ctor_NegativeTime_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_Ctor_NegativeTime_DoesntThrow)}");
			Assert.DoesNotThrow(() => new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(-1),
				_chattelReader,
				_chattelWriter
			));
		}

		[Test]
		public static void TestStorageManager_Ctor_ZeroTime_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_Ctor_ZeroTime_DoesntThrow)}");
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
		public static void TestStorageManager_CheckAsset_EmptyId_ArgumentException() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_EmptyId_ArgumentException)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.CheckAsset(Guid.Empty, result => { }));
		}

		[Test]
		public static void TestStorageManager_CheckAsset_Unknown_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_Unknown_DoesntThrow)}");
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
		public static void TestStorageManager_CheckAsset_Unknown_CallsCallback() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_Unknown_CallsCallback)}");
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
		public static void TestStorageManager_CheckAsset_Unknown_IsFalse() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_Unknown_IsFalse)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			mgr.CheckAsset(Guid.NewGuid(), Assert.False);
		}

		[Test]
		public static void TestStorageManager_CheckAsset_Known_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_Known_DoesntThrow)}");
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
		public static void TestStorageManager_CheckAsset_Known_CallsCallback() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_Known_CallsCallback)}");
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
		public static void TestStorageManager_CheckAsset_Known_IsTrue() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_Known_IsTrue)}");
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
		public static void TestStorageManager_CheckAsset_SingleNoExist_CallsServerRequestAsset() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_SingleNoExist_CallsServerRequestAsset)}");
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			using (var localStorage = new AssetLocalStorageLmdbPartitionedLRU(
				config,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_MAX_SIZE_BYTES,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_PARTITION_INTERVAL
			)) {
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
		}

		[Test]
		public static void TestStorageManager_CheckAsset_DoubleNoExist_CallsServerRequestOnlyOnce() {
			LOG.Info($"Executing {nameof(TestStorageManager_CheckAsset_DoubleNoExist_CallsServerRequestOnlyOnce)}");
			// Tests the existence of a negative cache.
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			using (var localStorage = new AssetLocalStorageLmdbPartitionedLRU(
				config,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_MAX_SIZE_BYTES,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_PARTITION_INTERVAL
			)) {
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
		}

		#endregion

		#region Putting assets

		[Test]
		public static void TestStorageManager_StoreAsset_AssetNull_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_AssetNull_ArgumentNullException)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentNullException>(() => mgr.StoreAsset(null, result => { }));
		}

		[Test]
		public static void TestStorageManager_StoreAsset_EmptyId_ArgumentException() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_EmptyId_ArgumentException)}");
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
		public static void TestStorageManager_StoreAsset_DoesntThrowFirstTime() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_DoesntThrowFirstTime)}");
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
		public static void TestStorageManager_StoreAsset_DoesntThrowDuplicate() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_DoesntThrowDuplicate)}");
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
		public static void TestStorageManager_StoreAsset_DoesntThrowDuplicateParallel() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_DoesntThrowDuplicateParallel)}");
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
		public static void TestStorageManager_StoreAsset_DoesntThrowMultipleParallel() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_DoesntThrowMultipleParallel)}");
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

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_StoreAsset_AbleToFindAsset() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_AbleToFindAsset)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			var found = false;

			wait.Reset();
			mgr.CheckAsset(asset.Id, foundResult => { found = foundResult; wait.Set(); });
			wait.WaitOne();

			Assert.IsTrue(found);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_StoreAsset_AbleToFindAssetAfterFailure() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_AbleToFindAssetAfterFailure)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.CheckAsset(asset.Id, foundResult => wait.Set());
			wait.WaitOne();

			wait.Reset();
			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			var found = false;

			wait.Reset();
			mgr.CheckAsset(asset.Id, foundResult => { found = foundResult; wait.Set(); });
			wait.WaitOne();

			Assert.IsTrue(found);
		}


		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_StoreAsset_CallsServerPutAsset() {
			LOG.Info($"Executing {nameof(TestStorageManager_StoreAsset_CallsServerPutAsset)}");
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, server);
			using (var readerLocalStorage = new AssetLocalStorageLmdbPartitionedLRU(
				config,
				uint.MaxValue,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_PARTITION_INTERVAL
			)) {
				var reader = new ChattelReader(config, readerLocalStorage);
				var writer = new ChattelWriter(config, readerLocalStorage);

				var mgr = new StorageManager(
					readerLocalStorage,
					TimeSpan.FromMinutes(2),
					reader,
					writer
				);

				var asset = new StratusAsset {
					Id = Guid.NewGuid(),
				};

				var wait = new AutoResetEvent(false);

				mgr.StoreAsset(asset, result => wait.Set());
				wait.WaitOne();

				server.Received(1).StoreAssetSync(asset);
			}
		}

		#endregion

		#region Get Assets

		[Test]
		public static void TestStorageManager_GetAsset_EmptyId_ArgumentException() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_EmptyId_ArgumentException)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.GetAsset(Guid.Empty, result => { }, () => { }));
		}

		[Test]
		public static void TestStorageManager_GetAsset_Unknown_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Unknown_DoesntThrow)}");
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
		public static void TestStorageManager_GetAsset_Unknown_CallsFailureCallback() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Unknown_CallsFailureCallback)}");
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
		public static void TestStorageManager_GetAsset_Unknown_IsNull() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Unknown_IsNull)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			mgr.GetAsset(Guid.NewGuid(), Assert.IsNull, Assert.Fail);
		}

		[Test]
		public static void TestStorageManager_GetAsset_Known_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Known_DoesntThrow)}");
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
		public static void TestStorageManager_GetAsset_Known_CallsSuccessCallback() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Known_CallsSuccessCallback)}");
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
		public static void TestStorageManager_GetAsset_Known_IsNotNull() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Known_IsNotNull)}");
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
		public static void TestStorageManager_GetAsset_Known_HasSameId() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Known_HasSameId)}");
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
		public static void TestStorageManager_GetAsset_Known_IsIdentical() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_Known_IsIdentical)}");
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
		public static void TestStorageManager_GetAsset_SingleNoExist_CallsServerRequestAsset() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_SingleNoExist_CallsServerRequestAsset)}");
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			using (var localStorage = new AssetLocalStorageLmdbPartitionedLRU(
				config,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_MAX_SIZE_BYTES,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_PARTITION_INTERVAL
			)) {
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
		}

		[Test]
		public static void TestStorageManager_GetAsset_DoubleNoExist_CallsServerRequestOnlyOnce() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetAsset_DoubleNoExist_CallsServerRequestOnlyOnce)}");
			// Tests the existence of a negative cache.
			var server = Substitute.For<IAssetServer>();
			var config = new ChattelConfiguration(TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_FOLDER_PATH, WRITE_CACHE_FILE_PATH, WRITE_CACHE_MAX_RECORD_COUNT, server);
			using (var localStorage = new AssetLocalStorageLmdbPartitionedLRU(
				config,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_MAX_SIZE_BYTES,
				TestAssetLocalStorageLmdbPartitionedLRUCtor.DATABASE_PARTITION_INTERVAL
			)) {
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
		}

		#endregion

		#region Purge All Assets marked local

		[Test]
		public static void TestStorageManager_PurgeAllLocalAsset_EmptyLocalStorage_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAllLocalAsset_EmptyLocalStorage_DoesntThrow)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(mgr.PurgeAllLocalAssets);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_DoesntThrow)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			Assert.DoesNotThrow(mgr.PurgeAllLocalAssets);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_RemovedLocal() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_RemovedLocal)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = true,
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			mgr.PurgeAllLocalAssets();

			var foundAsset = true; // Put in opposite state to what is expected.

			wait.Reset();
			mgr.CheckAsset(asset.Id, found => { foundAsset = found; wait.Set(); });
			wait.WaitOne();

			Assert.False(foundAsset);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_DidntRemoveNonLocal() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_DidntRemoveNonLocal)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = false,
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			mgr.PurgeAllLocalAssets();

			var foundAsset = false; // Put in opposite state to what is expected.

			wait.Reset();
			mgr.CheckAsset(asset.Id, found => { foundAsset = found; wait.Set(); });
			wait.WaitOne();

			Assert.True(foundAsset);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_OnlyRemovesLocal() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAllLocalAsset_NonEmptyLocalStorage_OnlyRemovesLocal)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset1 = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = true,
			};
			var asset2 = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = false,
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset1, result => wait.Set());
			wait.WaitOne();

			wait.Reset();
			mgr.StoreAsset(asset2, result => wait.Set());
			wait.WaitOne();

			mgr.PurgeAllLocalAssets();

			var foundAsset1 = true; // Put in opposite state to what is expected.
			var foundAsset2 = false;

			wait.Reset();
			mgr.CheckAsset(asset1.Id, found => { foundAsset1 = found; wait.Set(); });
			wait.WaitOne();
			wait.Reset();
			mgr.CheckAsset(asset2.Id, found => { foundAsset2 = found; wait.Set(); });
			wait.WaitOne();

			Assert.False(foundAsset1);
			Assert.True(foundAsset2);
		}

		#endregion

		#region Purge Asset

		[Test]
		public static void TestStorageManager_PurgeAsset_EmptyId_ArgumentException() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_EmptyId_ArgumentException)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.Throws<ArgumentException>(() => mgr.PurgeAsset(Guid.Empty, result => { }));
		}

		[Test]
		public static void TestStorageManager_PurgeAsset_DoesntThrowFirstTime() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_DoesntThrowFirstTime)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			Assert.DoesNotThrow(() => mgr.PurgeAsset(Guid.NewGuid(), result => { }));
		}

		[Test]
		public static void TestStorageManager_PurgeAsset_DoesntThrowDuplicate() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_DoesntThrowDuplicate)}");
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
		public static void TestStorageManager_PurgeAsset_DoesntThrowMultiple() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_DoesntThrowMultiple)}");
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
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_Unknown_CallsCallback() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_Unknown_CallsCallback)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
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
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_Known_CallsCallback() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_Known_CallsCallback)}");
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

			var wait = new AutoResetEvent(false);
			var callbackCalled = false;

			mgr.PurgeAsset(asset.Id, result => { callbackCalled = true; wait.Set(); });

			wait.WaitOne();

			Assert.True(callbackCalled);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_Unknown_DoesntRemoveKnown() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_Unknown_DoesntRemoveKnown)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();
			wait.Reset();

			mgr.PurgeAsset(Guid.NewGuid(), result => wait.Set());
			wait.WaitOne();
			wait.Reset();

			Assert.True(_readerLocalStorage.AssetOnDisk(asset.Id));
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_Known_RemovesItem() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_Known_RemovesItem)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();
			wait.Reset();

			mgr.PurgeAsset(asset.Id, result => wait.Set());
			wait.WaitOne();

			Assert.False(_readerLocalStorage.AssetOnDisk(asset.Id));
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_Unknown_ReturnsNotFoundLocally() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_Unknown_ReturnsNotFoundLocally)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var assetId = Guid.NewGuid();

			var wait = new AutoResetEvent(false);
			StorageManager.PurgeResult status = StorageManager.PurgeResult.DONE;

			mgr.PurgeAsset(assetId, result => { status = result; wait.Set(); });

			wait.WaitOne();

			Assert.AreEqual(StorageManager.PurgeResult.NOT_FOUND_LOCALLY, status);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_Known_ReturnsDone() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_Known_ReturnsDone)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			StorageManager.PurgeResult status = StorageManager.PurgeResult.NOT_FOUND_LOCALLY;

			wait.Reset();
			mgr.PurgeAsset(asset.Id, result => { status = result; wait.Set(); });
			wait.WaitOne();

			Assert.AreEqual(StorageManager.PurgeResult.DONE, status);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_DoesntRemoveOtherAsset() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_DoesntRemoveOtherAsset)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			wait.Reset();
			mgr.PurgeAsset(Guid.NewGuid(), result => wait.Set());
			wait.WaitOne();

			Assert.True(_readerLocalStorage.AssetOnDisk(asset.Id));
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_PurgeAsset_RemovesAsset() {
			LOG.Info($"Executing {nameof(TestStorageManager_PurgeAsset_RemovesAsset)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());
			wait.WaitOne();

			wait.Reset();
			mgr.PurgeAsset(asset.Id, result => wait.Set());
			wait.WaitOne();

			Assert.False(_readerLocalStorage.AssetOnDisk(asset.Id));
		}

		// remote purge should not be called - however there's no API for that ATM in Chattel so it can't be called.

		#endregion

		#region GetLocallyKnownAssetIds

		[Test]
		public static void TestStorageManager_GetLocallyKnownAssetIds_None_Empty() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetLocallyKnownAssetIds_None_Empty)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var ids = mgr.GetLocallyKnownAssetIds("000");	
			Assert.IsEmpty(ids);
		}

		[Test]
		[Timeout(1000)]
		public static void TestStorageManager_GetLocallyKnownAssetIds_Single_FoundMatch() {
			LOG.Info($"Executing {nameof(TestStorageManager_GetLocallyKnownAssetIds_Single_FoundMatch)}");
			var mgr = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			var wait = new AutoResetEvent(false);

			mgr.StoreAsset(asset, result => wait.Set());

			wait.WaitOne();

			var ids = mgr.GetLocallyKnownAssetIds(asset.Id.ToString("N").Substring(0, 3));
			Assert.That(ids, Has.Member(asset.Id));
		}

		#endregion
	}
}
