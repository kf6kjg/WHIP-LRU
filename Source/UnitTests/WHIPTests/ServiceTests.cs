// ServiceTests.cs
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
using System.Threading;
using InWorldz.Whip.Client;
using NUnit.Framework;

namespace UnitTests.WHIPTests {
	[TestFixture]
	public static class ServiceTests {
		[Test]
		public static void TestServiceListening() {
			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				Assert.DoesNotThrow(() => {
					socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);
				}, "Service not listening on expected port.", null);
			}
		}

		[Test]
		public static void TestPIDFileExists() {
			FileAssert.Exists(Constants.PID_FILE_PATH, "Missing expected pidfile.");
		}

		[Test]
		[Timeout(5000)]
		public static void TestServiceMultipleBadRequests() {
			using (var socket = AuthTests.Connect()) {
				var assetId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";

				for (var index = 0; index < 10; ++index) {
					var request = new ClientRequestMsg(ClientRequestMsg.RequestType.STATUS_GET, assetId);
					request.Send(socket);

					while (socket.Available <= 0) {
					}

#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
					new ServerResponseMsg(socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
					// Don't care what the reponse was.
				}
			}
		}

		[Test]
		[Timeout(5000)]
		public static void TestServiceMultipleGoodRequests() {
			using (var socket = AuthTests.Connect()) {
				for (var index = 0; index < 10; ++index) {
					var request = new ClientRequestMsg(ClientRequestMsg.RequestType.STATUS_GET, Guid.Empty.ToString());
					request.Send(socket);

					while (socket.Available <= 0) {
					}

#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
					new ServerResponseMsg(socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
					// Don't care what the reponse was.
				}
			}
		}
	}
}
