// TestLibWhipLru.cs
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
	public class TestLibWhipLruCache :IDisposable {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private const uint ITERATION_MAX = 1000;
		private static readonly TimeSpan TEST_MAX_TIME = TimeSpan.FromSeconds(60);

		private readonly LibWhipLru.Cache.CacheManager _libWhipLruCacheManager;
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

		public TestLibWhipLruCache() {
			LOG.Debug($"Initializing {nameof(TestLibWhipLruCache)}...");

			_timer = new System.Timers.Timer();
			_timer.Elapsed += TimerExpired;

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				System.IO.Directory.Delete("TestLibWhipLruCache", true);
			}
			catch (Exception) {
			}
			try {
				System.IO.File.Delete("TestLibWhipLruCache.wcache");
			}
			catch (Exception) {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			_libWhipLruCacheManager = new LibWhipLru.Cache.CacheManager("TestLibWhipLruCache", 1024UL * 4096 * 4096, "TestLibWhipLruCache.wcache", 100);

			_libWhipLruCacheManager.PutAsset(_knownAsset);

			LOG.Debug($"Initialization of {nameof(TestLibWhipLruCache)} complete.");
		}

		public bool RunTests() {
			var status = true;

			var methodInfos = typeof(TestLibWhipLruCache).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
			var stopWatch = new Stopwatch();
			var testParams = new object[] { };
			foreach (var methodInfo in methodInfos) {
				if (!methodInfo.Name.StartsWith("Test", StringComparison.InvariantCulture)) continue;

				var counter = 0U;

				try {
					LOG.Debug($"Starting test {nameof(TestLibWhipLruCache)}.{methodInfo.Name}...");
					stopWatch.Restart();

					_cancelTest = false;
					_timer.Interval = TEST_MAX_TIME.TotalMilliseconds;
					_timer.Start();
					for (; counter < ITERATION_MAX; ++counter) {
						methodInfo.Invoke(this, testParams);
						if (_cancelTest) break;
					}
					_timer.Stop();

					stopWatch.Stop();

					LOG.Info($"Test {nameof(TestLibWhipLruCache)}.{methodInfo.Name} took {stopWatch.ElapsedMilliseconds}ms over {counter} iterations.");
				}
				catch (Exception e) {
					LOG.Warn($"Test {nameof(TestLibWhipLruCache)}.{methodInfo.Name} threw an exception after {stopWatch.ElapsedMilliseconds}ms over {counter} iterations.", e);
				}
			}

			return status;
		}

		private void TimerExpired(object sender, System.Timers.ElapsedEventArgs e) {
			_cancelTest = true;
		}

		private void TestGetUnknown() {
			_libWhipLruCacheManager.GetAsset(Guid.NewGuid());
		}

		private void TestGetKnown() {
			_libWhipLruCacheManager.GetAsset(_knownAsset.Id);
		}

		private void TestPutKnown() {
			_libWhipLruCacheManager.PutAsset(_knownAsset);
		}

		private void TestPutNewBlank() {
			_libWhipLruCacheManager.PutAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				Id = Guid.NewGuid(),
			});
		}

		private void TestPutNewComplete() {
			_libWhipLruCacheManager.PutAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				Id = Guid.NewGuid(),
				CreateTime = DateTime.UtcNow,
				Data = System.Text.Encoding.UTF8.GetBytes("Just some data."),
				Description = "Test of a new asset",
				Local = false,
				Name = "New Asset",
				Temporary = false,
				Type = 7,
			});
		}

		private void TestPutNewComplete2MB() {
			var data = new byte[2097152];

			RandomUtil.Rnd.NextBytes(data);

			_libWhipLruCacheManager.PutAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				Id = Guid.NewGuid(),
				CreateTime = DateTime.UtcNow,
				Data = data,
				Description = "Test of a new asset",
				Local = false,
				Name = "New Asset",
				Temporary = false,
				Type = 7,
			});
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					LOG.Debug($"Cleaning up after {nameof(TestLibWhipLruCache)}...");

					_libWhipLruCacheManager.Dispose();

					System.IO.Directory.Delete("TestLibWhipLruCache", true);
					System.IO.File.Delete("TestLibWhipLruCache.wcache");

					LOG.Debug($"Clean up after {nameof(TestLibWhipLruCache)} complete.");
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.

				// set large fields to null.

				disposedValue = true;
			}
		}

		// override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~TestLibWhipLruCache() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
