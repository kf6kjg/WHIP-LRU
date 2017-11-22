// TestWhip.cs
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
using System.Threading;

namespace SpeedTests {
	public class TestWhip : WhipServiceTest {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private const string SERVICE_ADDRESS = "127.0.0.1";
		private const int SERVICE_PORT = 37100;
		private const string SERVICE_PASSWORD = "whipspeed";

		private Process _whipService;

		public TestWhip() : base(SERVICE_ADDRESS, SERVICE_PORT, SERVICE_PASSWORD) {
			LOG.Debug($"Initializing {nameof(TestWhip)}...");

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				System.IO.Directory.Delete("whipassets", true);
			}
			catch (Exception) {
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			System.IO.Directory.CreateDirectory("whipassets");

			var psi = new ProcessStartInfo("whip"); // Yes, Linux only ATM...
			psi.UseShellExecute = false;
			psi.RedirectStandardError = true;
			psi.RedirectStandardInput = true;
			psi.RedirectStandardOutput = true;

			_whipService = Process.Start(psi);

			Thread.Sleep(500);

			LOG.Debug($"Initialization of {nameof(TestWhip)} complete.");
		}

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		protected override void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					LOG.Debug($"Cleaning up after {nameof(TestWhip)}...");

					Dispose();

					_whipService.Kill();

					Thread.Sleep(500);

					_whipService.Dispose();

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
					try {
						System.IO.Directory.Delete("whipassets", true);
					}
					catch (Exception) {
					}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

					LOG.Debug($"Clean up after {nameof(TestWhip)} complete.");
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.

				// set large fields to null.

				disposedValue = true;
			}
		}

		// override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~TestWhip() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		#endregion
	}
}
