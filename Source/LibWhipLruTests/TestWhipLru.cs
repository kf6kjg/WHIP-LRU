// TestWhipLru.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2018 Richard Curtice
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
using LibWhipLru;
using LibWhipLru.Cache;
using LibWhipLruTests.Cache;
using NUnit.Framework;

namespace LibWhipLruTests {
	[TestFixture]
	[NonParallelizable]
	public static class TestWhipLru {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string ADDRESS = "127.0.0.1";
		private static readonly uint PORT = 13375;
		private static readonly string PASSWORD = "password";
		private static readonly LibWhipLru.Util.PIDFileManager PID_FILE_MANAGER = new LibWhipLru.Util.PIDFileManager();

		private static AssetLocalStorageLmdb _readerLocalStorage;
		private static ChattelReader _chattelReader;
		private static ChattelWriter _chattelWriter;
		private static StorageManager _storageManager;

		[SetUp]
		public static void BeforeEveryTest() {
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				Directory.Delete(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, true);
			}
			catch {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			Directory.CreateDirectory(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH);
			var chattelConfigRead = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH);
			var chattelConfigWrite = new ChattelConfiguration(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH);

			_readerLocalStorage = new AssetLocalStorageLmdb(chattelConfigRead, TestAssetLocalStorageLmdb.DATABASE_MAX_SIZE_BYTES);
			_chattelReader = new ChattelReader(chattelConfigRead, _readerLocalStorage);
			_chattelWriter = new ChattelWriter(chattelConfigWrite, _readerLocalStorage);
			_storageManager = new StorageManager(
				_readerLocalStorage,
				TimeSpan.FromMinutes(2),
				_chattelReader,
				_chattelWriter
			);
		}

		[TearDown]
		public static void CleanupAfterEveryTest() {
			IDisposable readerDispose = _readerLocalStorage;
			_readerLocalStorage = null;
			readerDispose.Dispose();

			Directory.Delete(TestAssetLocalStorageLmdb.DATABASE_FOLDER_PATH, true);
		}

		#region Ctor

		[Test]
		public static void TestWhipLru_Ctor5_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_DoesntThrow)}");
			Assert.DoesNotThrow(() => new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_NullAddress_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_NullAddress_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => new WhipLru(
				null,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_EmptyAddress_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_EmptyAddress_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => new WhipLru(
				string.Empty,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_0Port_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_0Port_DoesntThrow)}");
			Assert.DoesNotThrow(() => new WhipLru(
				ADDRESS,
				0,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_65535Port_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_65535Port_DoesntThrow)}");
			Assert.DoesNotThrow(() => new WhipLru(
				ADDRESS,
				65535,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_65536Port_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_65536Port_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => new WhipLru(
				ADDRESS,
				65536,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_NullPidFileManager_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_NullPidFileManager_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				null,
				_storageManager
			));
		}

		[Test]
		public static void TestWhipLru_Ctor5_NullStorageManager_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor5_NullStorageManager_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				null
			));
		}

		[Test]
		public static void TestWhipLru_Ctor6_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor6_DoesntThrow)}");
			Assert.DoesNotThrow(() => new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager,
				10
			));
		}

		[Test]
		public static void TestWhipLru_Ctor6_BacklogLength0_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Ctor6_BacklogLength0_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager,
				0
			));
		}

		#endregion

		#region Start

		[Test]
		public static void TestWhipLru_Start_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Start_DoesntThrow)}");
			var whiplru = new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			);

			Assert.DoesNotThrow(whiplru.Start);

			whiplru.Stop();
		}

		[Test]
		public static void TestWhipLru_Start_Twice_InvalidOperationException() {
			LOG.Info($"Executing {nameof(TestWhipLru_Start_Twice_InvalidOperationException)}");
			var whiplru = new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			);

			whiplru.Start();

			Assert.Throws<InvalidOperationException>(whiplru.Start);

			whiplru.Stop();
		}

		[Test]
		public static void TestWhipLru_Start_Stop_Start_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Start_Stop_Start_DoesntThrow)}");
			var whiplru = new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			);

			whiplru.Start();

			whiplru.Stop();

			Assert.DoesNotThrow(whiplru.Start);

			whiplru.Stop();
		}

		// Service listening tests are handled in the API integration tests.

		#endregion

		#region Stop

		[Test]
		public static void TestWhipLru_Stop_AfterStarted_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Stop_AfterStarted_DoesntThrow)}");
			var whiplru = new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			);

			whiplru.Start();

			Assert.DoesNotThrow(whiplru.Stop);
		}

		[Test]
		public static void TestWhipLru_Stop_Fresh_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Stop_Fresh_DoesntThrow)}");
			var whiplru = new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			);

			Assert.DoesNotThrow(whiplru.Stop);
		}

		[Test]
		public static void TestWhipLru_Stop_TwiceAfterStarted_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWhipLru_Stop_TwiceAfterStarted_DoesntThrow)}");
			var whiplru = new WhipLru(
				ADDRESS,
				PORT,
				PASSWORD,
				PID_FILE_MANAGER,
				_storageManager
			);

			whiplru.Start();

			whiplru.Stop();

			Assert.DoesNotThrow(whiplru.Stop);
		}

		#endregion
	}
}
