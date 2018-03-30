// TestLibWhipLruLocalStorage.cs
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
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace SpeedTests {
	public class TestLibWhipLruLocalStorage : IDisposable {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private const uint ITERATION_MAX = 1000;
		private static readonly TimeSpan TEST_MAX_TIME = TimeSpan.FromSeconds(60);

		private readonly LibWhipLru.Cache.StorageManager _libWhipLruStorageManager;
		private readonly LibWhipLru.Cache.AssetLocalStorageLmdbPartitionedLRU _libWhipLruLocalStorage;
		private readonly InWorldz.Data.Assets.Stratus.StratusAsset _knownAsset = new InWorldz.Data.Assets.Stratus.StratusAsset {
			Id = Guid.NewGuid(),
			CreateTime = DateTime.UtcNow,
			Data = new byte[]{},
			Description = "Test of a known asset",
			Local = false,
			Name = "Known Asset",
			Temporary = false,
			Type = 7,
		};

		private readonly System.Timers.Timer _timer;
		private bool _cancelTest;

		public TestLibWhipLruLocalStorage() {
			LOG.Debug($"Initializing {nameof(TestLibWhipLruLocalStorage)}...");

			_timer = new System.Timers.Timer();
			_timer.Elapsed += TimerExpired;

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				System.IO.Directory.Delete("TestLibWhipLruCache", true);
			}
			catch {
				// Don't care.
			}
			try {
				System.IO.File.Delete("TestLibWhipLruCache.wcache");
			}
			catch {
				// Don't care.
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			var config = new Chattel.ChattelConfiguration(
				"TestLibWhipLruCache",
				"TestLibWhipLruCache.wcache",
				100,
				(Chattel.IAssetServer) null
			);

			_libWhipLruLocalStorage = new LibWhipLru.Cache.AssetLocalStorageLmdbPartitionedLRU(
				config,
				uint.MaxValue
			);

			_libWhipLruStorageManager = new LibWhipLru.Cache.StorageManager(
				_libWhipLruLocalStorage,
				TimeSpan.FromMinutes(2),
				null,
				null
			);

			_libWhipLruStorageManager.StoreAsset(_knownAsset, result => {});

			LOG.Debug($"Initialization of {nameof(TestLibWhipLruLocalStorage)} complete.");
		}

		public bool RunTests() {
			var status = true;

			var methodInfos = typeof(TestLibWhipLruLocalStorage).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
			var stopWatch = new Stopwatch();
			var testParams = new object[] { };
			foreach (var methodInfo in methodInfos) {
				if (!methodInfo.Name.StartsWith("Test", StringComparison.InvariantCulture)) {
					continue;
				}

				var counter = 0U;

				try {
					LOG.Debug($"Starting test {nameof(TestLibWhipLruLocalStorage)}.{methodInfo.Name}...");
					stopWatch.Restart();

					_cancelTest = false;
					_timer.Interval = TEST_MAX_TIME.TotalMilliseconds;
					_timer.Start();
					for (; counter < ITERATION_MAX; ++counter) {
						methodInfo.Invoke(this, testParams);
						if (_cancelTest) {
							break;
						}
					}
					_timer.Stop();

					stopWatch.Stop();

					LOG.Info($"Test {nameof(TestLibWhipLruLocalStorage)}.{methodInfo.Name} took {stopWatch.ElapsedMilliseconds}ms over {counter} iterations." + (_cancelTest ? " And was cancelled during the last iteration." : string.Empty));
				}
				catch (Exception e) {
					LOG.Warn($"Test {nameof(TestLibWhipLruLocalStorage)}.{methodInfo.Name} threw an exception after {stopWatch.ElapsedMilliseconds}ms over {counter} iterations.", e);
				}
			}

			return status;
		}

		private void TimerExpired(object sender, System.Timers.ElapsedEventArgs e) {
			_cancelTest = true;
		}

		#region Get Tests

		private void TestGetUnknown() {
			_libWhipLruStorageManager.GetAsset(Guid.NewGuid(), asset => { }, () => {});
		}

		private void TestGetKnown() {
			_libWhipLruStorageManager.GetAsset(_knownAsset.Id, asset => { }, () => { });
		}

		#endregion

		#region Get Dont Cache Tests

		private void TestGetDontCacheUnknown() {
			_libWhipLruStorageManager.GetAsset(Guid.NewGuid(), asset => { }, () => { }, false);
		}

		private void TestGetDontCacheKnown() {
			_libWhipLruStorageManager.GetAsset(_knownAsset.Id, asset => { }, () => { }, false);
		}

		#endregion

		#region Put Tests

		private void TestPutKnown() {
			_libWhipLruStorageManager.StoreAsset(_knownAsset, result => { });
		}

		private void TestPutNewBlank() {
			_libWhipLruStorageManager.StoreAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				Id = Guid.NewGuid(),
			}, result => { });
		}

		private void TestPutNewComplete() {
			_libWhipLruStorageManager.StoreAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				Id = Guid.NewGuid(),
				CreateTime = DateTime.UtcNow,
				Data = System.Text.Encoding.UTF8.GetBytes("Just some data."),
				Description = "Test of a new asset",
				Local = false,
				Name = "New Asset",
				Temporary = false,
				Type = 7,
			}, result => { });
		}

		private void TestPutNewComplete2MB() {
			var data = new byte[2097152];

			RandomUtil.Rnd.NextBytes(data);

			_libWhipLruStorageManager.StoreAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				Id = Guid.NewGuid(),
				CreateTime = DateTime.UtcNow,
				Data = data,
				Description = "Test of a new asset",
				Local = false,
				Name = "New Asset",
				Temporary = false,
				Type = 7,
			}, result => { });
		}

		#endregion

		#region Stored Asset Ids Get Tests

		private void TestStoredAssetIdsGet000() {
			_libWhipLruStorageManager.GetLocallyKnownAssetIds("000");
		}

		#endregion

		#region Test Tests

		private void TestTestUnknown() {
			_libWhipLruStorageManager.CheckAsset(Guid.NewGuid(), found => {});
		}

		private void TestTestKnown() {
			_libWhipLruStorageManager.CheckAsset(_knownAsset.Id, found => { });
		}

		#endregion

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					LOG.Debug($"Cleaning up after {nameof(TestLibWhipLruLocalStorage)}...");

					IDisposable disposableCache = _libWhipLruLocalStorage;
					disposableCache.Dispose();

					System.IO.Directory.Delete("TestLibWhipLruCache", true);
					System.IO.File.Delete("TestLibWhipLruCache.wcache");

					LOG.Debug($"Clean up after {nameof(TestLibWhipLruLocalStorage)} complete.");
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.

				// set large fields to null.

				disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion
	}
}
