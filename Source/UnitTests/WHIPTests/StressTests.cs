// StressTests.cs
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
using System.Net.Sockets;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnitTests.WHIPTests {
	[TestFixture]
	public class StressTests {
		private Socket _socket;

		[OneTimeSetUp]
		public void Setup() {
			_socket = AuthTests.Connect();
		}

		[OneTimeTearDown]
		public void Teardown() {
			_socket.Dispose();
			_socket = null;
		}

		#region Connection

		[Test]
		[Timeout(60000)]
		public void TestStressConnectCycling1000() {
			for (var i = 0; i < 1000; ++i) {
				using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
					socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);
				}
			}
		}

		[Test]
		[Timeout(60000)]
		public void TestStressConnectCyclingParallel1000() {
			Parallel.For(0, 1000, (index) => {
				using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
					socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);
				}
			});
		}

		#endregion

		#region PUT

		[Test]
		[Timeout(60000)]
		public void TestStressPUTRandomAsset1000() {
			for (var i = 0; i < 1000; ++i) {
				var asset = FullProtocolTests.CreateAndPutAsset(_socket, RandomBytes());
				Assert.NotNull(asset, "Stress test failed.");
			}
		}

		[Test]
		[Timeout(60000)]
		public void TestStressPUTRandomAssetParallel1000() {
			Parallel.For(0, 1000, (index) => {
				using (var socket = AuthTests.Connect()) {
					var asset = FullProtocolTests.CreateAndPutAsset(socket, RandomBytes());
					Assert.NotNull(asset, "Stress test failed.");
				}
			});
		}

		#endregion

		public static byte[] RandomBytes(int min = 2000, int max = 2000000) {
			var random = new Random();
			var randomAsset = new System.Collections.Generic.List<byte>();
			int numBytes = random.Next(min, max);

			for (int i = 0; i < numBytes; i++) {
				randomAsset.Add((byte)Math.Floor(26 * random.NextDouble() + 65));
			}

			return randomAsset.ToArray();
		}
	}
}
