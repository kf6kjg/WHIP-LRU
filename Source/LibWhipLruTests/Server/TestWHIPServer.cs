// TestWHIPServer.cs
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
using LibWhipLru.Server;
using NUnit.Framework;

namespace LibWhipLruTests.Server {
	[TestFixture]
	public static class TestWHIPServer {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly string DEFAULT_ADDRESS = "*";
		private static readonly string DEFAULT_PASSWORD = null;
		private static readonly uint DEFAULT_PORT = 32700;
		private static readonly uint DEFAULT_BACKLOG_LENGTH = 100;


		#region Ctor1

		[Test]
		public static void TestWHIPServer_Ctor1_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor1_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer((req, respHandler, context) => { })) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor1_HandlerNull_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor1_HandlerNull_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => {
				using (new WHIPServer(null)) {
					// Nothing to do here.
				}
			});
		}

		#endregion

		#region Ctor2

		[Test]
		public static void TestWHIPServer_Ctor2_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor2_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor2_HandlerNull_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor2_HandlerNull_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => {
				using (new WHIPServer(
					null,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor2_BacklogZero_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor2_BacklogZero_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					0
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor2_BacklogExactly2MiB_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor2_BacklogExactly2MiB_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					int.MaxValue
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor2_BacklogOver2MiB_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor2_BacklogOver2MiB_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					int.MaxValue + 1U
				)) {
					// Nothing to do here.
				}
			});
		}

		#endregion

		#region Ctor4

		[Test]
		public static void TestWHIPServer_Ctor4_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_HandlerNull_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_HandlerNull_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => {
				using (new WHIPServer(
					null,
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_AddressNull_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_AddressNull_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					null,
					DEFAULT_PORT,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_AddressLocalhostIp_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_AddressLocalhostIp_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					"127.0.0.1",
					DEFAULT_PORT,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_AddressAsterisk_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_AddressAsterisk_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					"*",
					DEFAULT_PORT,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_PortZero_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_PortZero_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					0,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_Port65535_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_Port65535_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					65535,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_Port65536_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_Port65536_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					65536,
					DEFAULT_PASSWORD
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_PasswordNull_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_PasswordNull_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					null
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_PasswordEmpty_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_PasswordEmpty_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					string.Empty
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor4_PasswordRandom32_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor4_PasswordRandom32_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					RandomUtil.StringUTF8(32)
				)) {
					// Nothing to do here.
				}
			});
		}

		#endregion

		#region Ctor5

		[Test]
		public static void TestWHIPServer_Ctor5_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_HandlerNull_ArgumentNullException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_HandlerNull_ArgumentNullException)}");
			Assert.Throws<ArgumentNullException>(() => {
				using (new WHIPServer(
					null,
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_BacklogZero_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_BacklogZero_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					0
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_BacklogExactly2MiB_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_BacklogExactly2MiB_ArgumentOutOfRangeException)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					int.MaxValue
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_BacklogOver2MiB_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_BacklogOver2MiB_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					int.MaxValue + 1U
				)) {
					// Nothing to do here.
				}
			});
		}


		[Test]
		public static void TestWHIPServer_Ctor5_AddressNull_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_AddressNull_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					null,
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_AddressLocalhostIp_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_AddressLocalhostIp_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					"127.0.0.1",
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_AddressAsterisk_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_AddressAsterisk_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					"*",
					DEFAULT_PORT,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_PortZero_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_PortZero_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					0,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_Port65535_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_Port65535_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					65535,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_Port65536_ArgumentOutOfRangeException() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_Port65536_ArgumentOutOfRangeException)}");
			Assert.Throws<ArgumentOutOfRangeException>(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					65536,
					DEFAULT_PASSWORD,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_PasswordNull_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_PasswordNull_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					null,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_PasswordEmpty_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_PasswordEmpty_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					string.Empty,
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		[Test]
		public static void TestWHIPServer_Ctor5_PasswordRandom32_DoesntThrow() {
			LOG.Info($"Executing {nameof(TestWHIPServer_Ctor5_PasswordRandom32_DoesntThrow)}");
			Assert.DoesNotThrow(() => {
				using (new WHIPServer(
					(req, respHandler, context) => { },
					DEFAULT_ADDRESS,
					DEFAULT_PORT,
					RandomUtil.StringUTF8(32),
					DEFAULT_BACKLOG_LENGTH
				)) {
					// Nothing to do here.
				}
			});
		}

		#endregion

	}
}
