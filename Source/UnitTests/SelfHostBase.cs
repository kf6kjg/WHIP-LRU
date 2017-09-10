// Setup.cs
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

using System.IO;
using System.Threading;
using NUnit.Framework;

namespace UnitTests {
	public class SelfHostBase {
		private System.Diagnostics.Process service;

		[OneTimeSetUp]
		public void Init() {
			// Boot the service
			service = new System.Diagnostics.Process();
			service.EnableRaisingEvents = false;
			service.StartInfo.FileName = Path.Combine(Constants.EXECUTABLE_DIRECTORY, "WHIP_LRU.exe");
			service.StartInfo.WorkingDirectory = Constants.EXECUTABLE_DIRECTORY;
			service.StartInfo.Arguments = $"--inifile='{Constants.INI_PATH}' --logconfig='{Constants.LOG_CONFIG_PATH}' --pidfile='{Constants.PID_FILE_PATH}'";
			service.StartInfo.RedirectStandardInput = false;
			service.StartInfo.RedirectStandardOutput = false;
			service.StartInfo.RedirectStandardError = false;
			service.StartInfo.UseShellExecute = false;

			var result = service.Start();
			if (!result)
				Assert.Fail("Could not start process, maybe an existing process has been reused?");

			Thread.Sleep(500);
			Assert.IsFalse(service.HasExited, "Service closed during startup, check UnitTests.WHIP_LRU.log!");
		}

		[OneTimeTearDown]
		public void Cleanup() {
			service.Close(); // Ask it nicely to shut itself.

			// Clear the PID file if it exists.
			File.Delete(Constants.PID_FILE_PATH);
		}
	}
}
