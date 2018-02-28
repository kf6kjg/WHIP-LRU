// TestAssetLocalStorageLmdb_Ctor3NoLruPurge.cs
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
using System.Collections.Generic;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public static class TestAssetLocalStorageLmdb_Ctor3NoLruPurge {
		public static readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_ac_lmdb";
		public const ulong DATABASE_MAX_SIZE_BYTES = 4UL * 1024 * 1024/*Min value to get tests to run*/;

		private static ChattelConfiguration _chattelConfigRead;
		private static AssetLocalStorageLmdb _localStorageLmdb;
		private static IChattelLocalStorage _localStorage;

		[OneTimeSetUp]
		public static void Startup() {
			// Folder has to be there or the config fails.
			TestAssetLocalStorageLmdbCtor.RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
			_chattelConfigRead = new ChattelConfiguration(DATABASE_FOLDER_PATH);
		}

		[SetUp]
		public static void BeforeEveryTest() {
			TestAssetLocalStorageLmdbCtor.RebuildLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);

			_localStorage = _localStorageLmdb = new AssetLocalStorageLmdb(
				_chattelConfigRead,
				DATABASE_MAX_SIZE_BYTES,
				false
			);
		}

		[TearDown]
		public static void CleanupAfterEveryTest() {
			_localStorage = null;
			IDisposable localStorageDisposal = _localStorageLmdb;
			_localStorageLmdb = null;
			localStorageDisposal?.Dispose();

			TestAssetLocalStorageLmdbCtor.CleanLocalStorageFolder(DATABASE_FOLDER_PATH, TestStorageManager.WRITE_CACHE_FILE_PATH);
		}

		#region Contains and AssetOnDisk

		// These two methods are, or are nearly, simple pass-throughs for the OrderedGuidCache.  Thus they are tested in that class's tests.

		#endregion

		#region Store Asset

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_AssetOnDiskImmediately() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(asset);
			Assert.True(_localStorageLmdb.AssetOnDisk(asset.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_ContainsImmediately() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(asset);
			Assert.True(_localStorageLmdb.Contains(asset.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_DoesntThrow() {
			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			Assert.DoesNotThrow(() => _localStorage.StoreAsset(asset));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_Null_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => _localStorage.StoreAsset(null));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_EmpyId_ArgumentException() {
			var asset = new StratusAsset {
				Id = Guid.Empty,
			};

			Assert.Throws<ArgumentException>(() => _localStorage.StoreAsset(asset));
		}

		[Test]
		[Timeout(2000)]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_Full_FailsLast_Asset() {
			var assetId1 = Guid.NewGuid();
			var assetId2 = Guid.NewGuid();

			_localStorage.StoreAsset(new StratusAsset {
				Id = assetId1,
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 4],
			});

			_localStorage.StoreAsset(new StratusAsset {
				Id = Guid.NewGuid(),
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 4],
			});

			_localStorage.StoreAsset(new StratusAsset {
				Id = Guid.NewGuid(),
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 4],
			});

			Assert.Throws<WriteCacheFullException>(() => _localStorage.StoreAsset(new StratusAsset {
				Id = assetId2,
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 3],
			}));
		}

		[Test]
		[Timeout(2000)]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_Full_AllExpectedAssetsExist() {
			var assetId1 = Guid.NewGuid();
			var assetId2 = Guid.NewGuid();
			var assetId3 = Guid.NewGuid();
			var assetId4 = Guid.NewGuid();

			_localStorage.StoreAsset(new StratusAsset {
				Id = assetId1,
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 4],
			});

			_localStorage.StoreAsset(new StratusAsset {
				Id = assetId2,
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 3],
			});

			_localStorage.StoreAsset(new StratusAsset {
				Id = assetId3,
				Data = new byte[DATABASE_MAX_SIZE_BYTES / 3],
			});

			try {
				_localStorage.StoreAsset(new StratusAsset {
					Id = assetId4,
					Data = new byte[DATABASE_MAX_SIZE_BYTES / 8],
				});
			}
			catch (WriteCacheFullException) {
				// Skip it if it happens, which it very much should in this case.
			}

			var errors = new List<string>();

			if (_localStorageLmdb.AssetOnDisk(assetId1)) {
				errors.Add("Missing Asset 1");
			}

			if (_localStorageLmdb.AssetOnDisk(assetId2)) {
				errors.Add("Missing Asset 2");
			}

			if (_localStorageLmdb.AssetOnDisk(assetId3)) {
				errors.Add("Missing Asset 3");
			}

			if (!_localStorageLmdb.AssetOnDisk(assetId4)) {
				errors.Add("Asset 4 should have been deleted");
			}

			if (errors.Count > 0) {
				Assert.Fail(string.Join(", ", errors));
			}
		}

		[Test]
		[Timeout(40000)]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_StoreAsset_Purge_Cycle() {
			Assert.DoesNotThrow(() => {
				for (var i = 0; i < 1000; ++i) {
					var assetId = Guid.NewGuid();

					_localStorage.StoreAsset(new StratusAsset {
						Id = assetId,
						Data = new byte[RandomUtil.NextUInt() % (DATABASE_MAX_SIZE_BYTES / 8 - 4096) + 4096],
					});

					_localStorage.Purge(assetId);
				}
			});
		}

		#endregion

		#region PurgeAll

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_Null_EmptyLocalStorage_DoesntThrow() {
			Assert.DoesNotThrow(() => _localStorage.PurgeAll(null));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_Null_NonEmptyLocalStorage_DoesntThrow() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			_localStorage.StoreAsset(assetTest);

			Assert.DoesNotThrow(() => _localStorage.PurgeAll(null));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_Null_NonEmptyLocalStorage_RemovesDiskEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			_localStorage.StoreAsset(assetTest);
			_localStorage.PurgeAll(null);

			Assert.False(_localStorageLmdb.AssetOnDisk(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_Null_NonEmptyLocalStorage_RemovesMemoryEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			_localStorage.StoreAsset(assetTest);
			_localStorage.PurgeAll(null);

			Assert.False(_localStorageLmdb.Contains(assetTest.Id));
		}


		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_EmptyList_EmptyLocalStorage_DoesntThrow() {
			Assert.DoesNotThrow(() => _localStorage.PurgeAll(new List<AssetFilter>{ }));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_EmptyList_NonEmptyLocalStorage_DoesntThrow() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			_localStorage.StoreAsset(assetTest);

			Assert.DoesNotThrow(() => _localStorage.PurgeAll(new List<AssetFilter> { }));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_EmptyList_NonEmptyLocalStorage_RemovesDiskEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			_localStorage.StoreAsset(assetTest);
			_localStorage.PurgeAll(new List<AssetFilter> { });

			Assert.False(_localStorageLmdb.AssetOnDisk(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_EmptyList_NonEmptyLocalStorage_RemovesMemoryEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			_localStorage.StoreAsset(assetTest);
			_localStorage.PurgeAll(new List<AssetFilter> { });

			Assert.False(_localStorageLmdb.Contains(assetTest.Id));
		}


		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_EmptyLocalStorage_DoesntThrow() {
			Assert.DoesNotThrow(() => _localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			}));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_Match_NonEmptyLocalStorage_DoesntThrow() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = true,
			};
			_localStorage.StoreAsset(assetTest);

			Assert.DoesNotThrow(() => _localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			}));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_Match_NonEmptyLocalStorage_RemovesDiskEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = true,
			};
			_localStorage.StoreAsset(assetTest);

			_localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			});

			Assert.False(_localStorageLmdb.AssetOnDisk(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_Match_NonEmptyLocalStorage_RemovesMemoryEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = true,
			};
			_localStorage.StoreAsset(assetTest);

			_localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			});

			Assert.False(_localStorageLmdb.Contains(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_Nonmatch_NonEmptyLocalStorage_DoesntThrow() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = false,
			};
			_localStorage.StoreAsset(assetTest);

			Assert.DoesNotThrow(() => _localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			}));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_Nonmatch_NonEmptyLocalStorage_DoesntRemoveDiskEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = false,
			};
			_localStorage.StoreAsset(assetTest);

			_localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			});

			Assert.True(_localStorageLmdb.AssetOnDisk(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_Nonmatch_NonEmptyLocalStorage_DoesntRemoveMemoryEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = false,
			};
			_localStorage.StoreAsset(assetTest);

			_localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			});

			Assert.True(_localStorageLmdb.Contains(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_PurgeAll_SingleFilter_OneMatch_OneNonmatch_Correct() {
			var assetTest1 = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = true,
			};
			var assetTest2 = new StratusAsset {
				Id = Guid.NewGuid(),
				Local = false,
			};
			_localStorage.StoreAsset(assetTest1);
			_localStorage.StoreAsset(assetTest2);

			_localStorage.PurgeAll(new List<AssetFilter> {
				new AssetFilter {
					LocalFilter = true,
				}
			});

			Assert.False(_localStorageLmdb.AssetOnDisk(assetTest1.Id));
			Assert.True(_localStorageLmdb.AssetOnDisk(assetTest2.Id));
		}

		#endregion

		#region Purge

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_EmpyId_ArgumentException() {
			Assert.Throws<ArgumentException>(() => _localStorage.Purge(Guid.Empty));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_Unknown_AssetNotFoundException() {
			Assert.Throws<AssetNotFoundException>(() => _localStorage.Purge(Guid.NewGuid()));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_Known_DoesntThrow() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest);

			Assert.DoesNotThrow(() => _localStorage.Purge(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_NonEmptyLocalStorage_RemovesDiskEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest);

			_localStorage.Purge(assetTest.Id);

			Assert.False(_localStorageLmdb.AssetOnDisk(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_NonEmptyLocalStorage_RemovesMemoryEntry() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest);

			_localStorage.Purge(assetTest.Id);

			Assert.False(_localStorageLmdb.Contains(assetTest.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_NonEmptyLocalStorage_LeavesOtherDiskEntry() {
			var assetTest1 = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			var assetTest2 = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest1);
			_localStorage.StoreAsset(assetTest2);

			_localStorage.Purge(assetTest1.Id);

			Assert.True(_localStorageLmdb.AssetOnDisk(assetTest2.Id));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_Purge_NonEmptyLocalStorage_LeavesOtherMemoryEntry() {
			var assetTest1 = new StratusAsset {
				Id = Guid.NewGuid(),
			};
			var assetTest2 = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest1);
			_localStorage.StoreAsset(assetTest2);

			_localStorage.Purge(assetTest1.Id);

			Assert.True(_localStorageLmdb.Contains(assetTest2.Id));
		}

		#endregion

		#region TryGetAsset

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_TryGetAsset_EmpyId_ArgumentException() {
			Assert.Throws<ArgumentException>(() => _localStorage.TryGetAsset(Guid.Empty, out var assetResult));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_TryGetAsset_Unknown_False() {
			Assert.False(_localStorage.TryGetAsset(Guid.NewGuid(), out var assetResult));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_TryGetAsset_Unknown_OutNull() {
			_localStorage.TryGetAsset(Guid.NewGuid(), out var assetResult);
			Assert.Null(assetResult);
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_TryGetAsset_Known_True() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest);

			Assert.True(_localStorage.TryGetAsset(assetTest.Id, out var assetResult));
		}

		[Test]
		public static void TestAssetLocalStorageLmdbCtor3NoLruPurge_TryGetAsset_Known_OutEqual() {
			var assetTest = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			_localStorage.StoreAsset(assetTest);

			_localStorage.TryGetAsset(assetTest.Id, out var assetResult);

			Assert.AreEqual(assetTest, assetResult);
		}

		#endregion

		#region Dispose

		// Not testable since we've got an active instance already on it.
		// Actually, it's being tested repeatedly via the disposal/reactivation on every test.

		#endregion
	}
}
