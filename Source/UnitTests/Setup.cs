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

using System;
using System.IO;
using System.Threading;
using Chattel;
using LibWhipLru;
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using WHIP_LRU.Server;

namespace UnitTests {
	[SetUpFixture]
	public sealed class Setup {
		private WhipLru _service;

		[OneTimeSetUp]
		public void Init() {
			// Configure Log4Net
			XmlConfigurator.Configure(new FileInfo(Constants.LOG_CONFIG_PATH));

			// Load INI stuff
			var configSource = new ArgvConfigSource(new string[] { });

			// Configure nIni aliases and locale
			Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US", true);

			configSource.Alias.AddAlias("On", true);
			configSource.Alias.AddAlias("Off", false);
			configSource.Alias.AddAlias("True", true);
			configSource.Alias.AddAlias("False", false);
			configSource.Alias.AddAlias("Yes", true);
			configSource.Alias.AddAlias("No", false);

			// Read in the ini file
			configSource.Merge(new IniConfigSource(Constants.INI_PATH));

			// Start booting server
			var pidFileManager = new WHIP_LRU.Util.PIDFileManager(Constants.PID_FILE_PATH);

			var chattelConfigRead = new ChattelConfiguration(configSource, configSource.Configs["AssetsRead"]);
			var chattelConfigWrite = new ChattelConfiguration(configSource, configSource.Configs["AssetsWrite"]);

			_service = new WhipLru(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT, Constants.PASSWORD, pidFileManager, chattelConfigRead, chattelConfigWrite);

			_service.Start();
		}

		[OneTimeTearDown]
		public void Cleanup() {
			_service.Stop();

			Thread.Sleep(500);

			// Clear the PID file if it exists. 
			File.Delete(Constants.PID_FILE_PATH);
		}
	}
}
