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
using System.Reflection;
using System.Threading;

namespace SpeedTests {
	public class TestLibWhipLru : WhipServiceTest {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		protected const string SERVICE_ADDRESS = "127.0.0.1";
		protected const int SERVICE_PORT = 37111;
		protected const string SERVICE_PASSWORD = "widjadidja";

		private readonly LibWhipLru.Cache.CacheManager _libWhipLruCacheManager;
		private readonly LibWhipLru.WhipLru _libWhipLru;

		public TestLibWhipLru() : base(SERVICE_ADDRESS, SERVICE_PORT, SERVICE_PASSWORD) {
			LOG.Debug($"Initializing {nameof(TestLibWhipLru)}...");

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				System.IO.Directory.Delete("SpeedTestLibWhipLru", true);
			}
			catch (Exception) {
			}
			try {
				System.IO.File.Delete("SpeedTestLibWhipLru.wcache");
			}
			catch (Exception) {
			}
			try {
				System.IO.File.Delete("SpeedTestLibWhipLru.pid");
			}
			catch (Exception) {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			_libWhipLruCacheManager = new LibWhipLru.Cache.CacheManager("SpeedTestLibWhipLru", 1024UL * 4096 * 4096, "SpeedTestLibWhipLru.wcache", 100, TimeSpan.FromMinutes(2));

			_libWhipLruCacheManager.PutAsset(new InWorldz.Data.Assets.Stratus.StratusAsset {
				CreateTime = DateTime.UtcNow, // Close enough.
				Data = _knownAsset.Data,
				Description = _knownAsset.Description,
				Id = new Guid(_knownAsset.Uuid),
				Local = _knownAsset.Local,
				Name = _knownAsset.Name,
				Temporary = _knownAsset.Temporary,
				Type = (sbyte)_knownAsset.Type,
			});

			var pidFileManager = new LibWhipLru.Util.PIDFileManager("SpeedTestLibWhipLru.pid");

			_libWhipLru = new LibWhipLru.WhipLru(SERVICE_ADDRESS, SERVICE_PORT, SERVICE_PASSWORD, pidFileManager, _libWhipLruCacheManager);

			_libWhipLru.Start();

			Thread.Sleep(500);

			LOG.Debug($"Initialization of {nameof(TestLibWhipLru)} complete.");
		}

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		protected override void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					LOG.Debug($"Cleaning up after {nameof(TestLibWhipLru)}...");

					base.Dispose(disposing);

					_libWhipLru.Stop();

					Thread.Sleep(500);

					_libWhipLruCacheManager.Dispose();

					Thread.Sleep(500);

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
					try {
						System.IO.Directory.Delete("SpeedTestLibWhipLru", true);
					}
					catch (Exception) {
					}
					try {
						System.IO.File.Delete("SpeedTestLibWhipLru.wcache");
					}
					catch (Exception) {
					}
					try {
						System.IO.File.Delete("SpeedTestLibWhipLru.pid");
					}
					catch (Exception) {
					}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

					LOG.Debug($"Clean up after {nameof(TestLibWhipLru)} complete.");
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.

				disposedValue = true;
			}
		}

		// override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~TestLibWhipLru() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		#endregion
	}
}
