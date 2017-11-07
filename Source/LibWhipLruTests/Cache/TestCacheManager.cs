// TestCacheManager.cs
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
using System.Linq;
using System.Threading;
using LibWhipLru.Cache;
using NUnit.Framework;

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public class TestCacheManager {
		private readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test";
		private const ulong DATABASE_MAX_SIZE_BYTES = 1024 * 1024 * 1024;
		private readonly string WRITE_CACHE_FILE_PATH = $"{TestContext.CurrentContext.TestDirectory}/test.whipwcache";
		private const uint WRITE_CACHE_MAX_RECORD_COUNT = 8;

		[OneTimeTearDown]
		public void CleanupAfter() {
			File.Delete(WRITE_CACHE_FILE_PATH);
			Directory.Delete(DATABASE_FOLDER_PATH, true);
		}

		[Test]
		public void TestCtorDoesNotThrow() {
			Assert.DoesNotThrow(() => new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				null,
				null
			));
		}

		[Test]
		public void TestCtorBlankDBPathArgNullException() {
			Assert.Throws<ArgumentNullException>(() => new CacheManager(
				"",
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				null,
				null
			));
		}

		[Test]
		public void TestCtorNullDBPathArgNullException() {
			Assert.Throws<ArgumentNullException>(() => new CacheManager(
				null,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				null,
				null
			));
		}
	}
}
