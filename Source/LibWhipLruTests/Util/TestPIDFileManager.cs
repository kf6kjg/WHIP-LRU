// TestPIDFileManager.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
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
using System.Diagnostics;
using System.IO;
using LibWhipLru.Util;
using NUnit.Framework;

#pragma warning disable RECS0026 // Possible unassigned object created by 'new'

namespace LibWhipLruTests.Util {
	[TestFixture]
	[NonParallelizable]
	public static class TestPIDFileManager {
		public static readonly string PID_FILE_PATH = Path.Combine(TestContext.CurrentContext.TestDirectory, "test.pid");
		public static readonly string PID_FILE_PATH_AUTO = $"{Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName)}.pid";

		/// <summary>
		/// Deletes the process identifier file given.
		/// </summary>
		/// <param name="path">Path to PID file.</param>
		public static void DeletePIdFile(string path) {
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				File.Delete(path);
			}
			catch {
				// Ignore.
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
		}

		/// <summary>
		/// Deletes the automatically generated process identifier file.
		/// This type can exist if the PIDFileManager class is instanciated without any parameters.
		/// </summary>
		public static void DeletePIdFileAuto() {
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				File.Delete(PID_FILE_PATH_AUTO);
			}
			catch {
				// Ignore.
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
		}

		public static byte[] GeneratePIdContents(PIDFileManager.Status status = PIDFileManager.Status.Init) {
			var pid = Process.GetCurrentProcess().Id;
			var pidInfo = $"{((int)status)} {pid}";
			return System.Text.Encoding.UTF8.GetBytes(pidInfo);
		}

		[SetUp]
		public static void BeforeEveryTest() {
			DeletePIdFile(PID_FILE_PATH);
			DeletePIdFileAuto();
		}

		#region Ctor()

		[Test]
		public static void TestPIDFileManager_Ctor0_DoesNotThrow() {
			Assert.DoesNotThrow(() => new PIDFileManager());
		}

		[Test]
		public static void TestPIDFileManager_Ctor0_CreatesPIdFile() {
			var pidMan = new PIDFileManager();

			FileAssert.Exists(PID_FILE_PATH_AUTO);
		}

		[Test]
		public static void TestPIDFileManager_Ctor0_PIdFileHasCorrectContents() {
			var pidMan = new PIDFileManager();

			var expectedContents = GeneratePIdContents(PIDFileManager.Status.Init);

			using (var pidFile = File.OpenRead(PID_FILE_PATH_AUTO)) {
				var contents = new byte[pidFile.Length];
				pidFile.Read(contents, 0, (int)pidFile.Length);

				Assert.AreEqual(expectedContents, contents);
			}
		}

		#endregion

		#region Ctor(string)

		[Test]
		public static void TestPIDFileManager_Ctor1_Null_DoesNotThrow() {
			Assert.DoesNotThrow(() => new PIDFileManager(null));
		}

		[Test]
		public static void TestPIDFileManager_Ctor1_Null_CreatesAutoPIdFile() {
			var pidMan = new PIDFileManager(null);

			FileAssert.Exists(PID_FILE_PATH_AUTO);
		}

		[Test]
		public static void TestPIDFileManager_Ctor1_Null_PIdFileHasCorrectContents() {
			var pidMan = new PIDFileManager(null);

			var expectedContents = GeneratePIdContents(PIDFileManager.Status.Init);

			using (var pidFile = File.OpenRead(PID_FILE_PATH_AUTO)) {
				var contents = new byte[pidFile.Length];
				pidFile.Read(contents, 0, (int)pidFile.Length);

				Assert.AreEqual(expectedContents, contents);
			}
		}


		[Test]
		public static void TestPIDFileManager_Ctor1_Empty_DoesNotThrow() {
			Assert.DoesNotThrow(() => new PIDFileManager(string.Empty));
		}

		[Test]
		public static void TestPIDFileManager_Ctor1_Empty_CreatesAutoPIdFile() {
			var pidMan = new PIDFileManager(string.Empty);

			FileAssert.Exists(PID_FILE_PATH_AUTO);
		}

		[Test]
		public static void TestPIDFileManager_Ctor1_Empty_PIdFileHasCorrectContents() {
			var pidMan = new PIDFileManager(string.Empty);

			var expectedContents = GeneratePIdContents(PIDFileManager.Status.Init);

			using (var pidFile = File.OpenRead(PID_FILE_PATH_AUTO)) {
				var contents = new byte[pidFile.Length];
				pidFile.Read(contents, 0, (int)pidFile.Length);

				Assert.AreEqual(expectedContents, contents);
			}
		}


		[Test]
		public static void TestPIDFileManager_Ctor1_Path_DoesNotThrow() {
			Assert.DoesNotThrow(() => new PIDFileManager(PID_FILE_PATH));
		}

		[Test]
		public static void TestPIDFileManager_Ctor1_Path_CreatesAutoPIdFile() {
			var pidMan = new PIDFileManager(PID_FILE_PATH);

			FileAssert.Exists(PID_FILE_PATH);
		}

		[Test]
		public static void TestPIDFileManager_Ctor1_Path_PIdFileHasCorrectContents() {
			var pidMan = new PIDFileManager(PID_FILE_PATH);

			var expectedContents = GeneratePIdContents(PIDFileManager.Status.Init);

			using (var pidFile = File.OpenRead(PID_FILE_PATH)) {
				var contents = new byte[pidFile.Length];
				pidFile.Read(contents, 0, (int)pidFile.Length);

				Assert.AreEqual(expectedContents, contents);
			}
		}

		#endregion

		#region SetStatus(Status)

		[Test]
		public static void TestPIDFileManager_SetStatus_DoesntThrow() {
			var pidMan = new PIDFileManager();

			Assert.DoesNotThrow(() => pidMan.SetStatus(PIDFileManager.Status.Running));
		}

		[Test]
		public static void TestPIDFileManager_SetStatus_PIdFileHasCorrectContents() {
			var pidMan = new PIDFileManager();

			var expectedContents = GeneratePIdContents(PIDFileManager.Status.Running);

			pidMan.SetStatus(PIDFileManager.Status.Running);

			using (var pidFile = File.OpenRead(PID_FILE_PATH_AUTO)) {
				var contents = new byte[pidFile.Length];
				pidFile.Read(contents, 0, (int)pidFile.Length);

				Assert.AreEqual(expectedContents, contents);
			}
		}


		#endregion
	}
}
