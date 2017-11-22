// WhipServiceTest.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using InWorldz.Whip.Client;

namespace SpeedTests {
	public class WhipServiceTest : IDisposable {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private const uint ITERATION_MAX = 1000;
		private static readonly TimeSpan TEST_MAX_TIME = TimeSpan.FromSeconds(10);

		protected readonly Asset _knownAsset = new Asset(
			Guid.NewGuid().ToString(),
			7, // Notecard
			false,
			false,
			(int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds,
			"Known Asset",
			"Test of a known asset",
			new byte[] { 0x31, 0x33, 0x33, 0x37 }
		);

		private Socket _socket;

		private readonly string _serviceAddress;
		private readonly int _servicePort;
		private readonly string _servicePassword;

		public WhipServiceTest(string serviceAddress, int servicePort, string servicePassword) {
			LOG.Debug($"Initializing {nameof(WhipServiceTest)}...");

			_serviceAddress = serviceAddress;
			_servicePort = servicePort;
			_servicePassword = servicePassword;

			LOG.Debug($"Initialization of {nameof(WhipServiceTest)} complete.");
		}

		public bool RunTests() {
			if (_socket == null) {
				_socket = Connect(_serviceAddress, _servicePort, _servicePassword);
			}

			var status = true;

			var methodInfos = typeof(WhipServiceTest).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
			var stopWatch = new Stopwatch();
			var testParams = new object[] { };
			foreach (var methodInfo in methodInfos) {
				if (!methodInfo.Name.StartsWith("Test", StringComparison.InvariantCulture)) continue;

				var counter = 0U;
				var completed = false;

				try {
					LOG.Debug($"Starting test {nameof(WhipServiceTest)}.{methodInfo.Name}...");
					stopWatch.Restart();

					ExecuteWithTimeLimit(() => {
						for (; counter < ITERATION_MAX; ++counter) {
							methodInfo.Invoke(this, testParams);
						}
					}, TEST_MAX_TIME, out completed);

					stopWatch.Stop();
					LOG.Info($"Test {nameof(WhipServiceTest)}.{methodInfo.Name} took {stopWatch.ElapsedMilliseconds}ms over {counter} iterations.");
				}
				catch (Exception e) {
					LOG.Warn($"Test {nameof(WhipServiceTest)}.{methodInfo.Name} threw an exception after {stopWatch.ElapsedMilliseconds}ms over {counter} iterations.", e);
				}
			}

			return status;
		}

		private static void ExecuteWithTimeLimit(Action func, TimeSpan timeout, out bool completed) {
			var iar = func.BeginInvoke(null, new object());
			completed = iar.AsyncWaitHandle.WaitOne(timeout);
			func.EndInvoke(iar); //not calling EndInvoke will result in a memory leak
		}

		private void TestGetUnknown() {
			var request = new ClientRequestMsg(ClientRequestMsg.RequestType.GET, Guid.NewGuid().ToString());
			request.Send(_socket);
			// Wait until response comes back.
			while (_socket.Available <= 0) {
			}
#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new ServerResponseMsg(_socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
		}

		private void TestGetKnown() {
			var request = new ClientRequestMsg(ClientRequestMsg.RequestType.GET, _knownAsset.Uuid);
			request.Send(_socket);
			// Wait until response comes back.
			while (_socket.Available <= 0) {
			}
#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new ServerResponseMsg(_socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
		}

		private void TestPutKnown() {
			var request = new ClientRequestMsg(ClientRequestMsg.RequestType.PUT, _knownAsset.Uuid, _knownAsset.Serialize().data);
			request.Send(_socket);
			// Wait until response comes back.
			while (_socket.Available <= 0) {
			}
#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new ServerResponseMsg(_socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
		}

		private void TestPutNewBlank() {
			var asset = new Asset(
				Guid.NewGuid().ToString(),
				7, // Notecard
				false,
				false,
				(int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds,
				"Blank Asset",
				"",
				new byte[] {}
			);

			var request = new ClientRequestMsg(ClientRequestMsg.RequestType.PUT, asset.Uuid, asset.Serialize().data);
			request.Send(_socket);
			// Wait until response comes back.
			while (_socket.Available <= 0) {
			}
#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new ServerResponseMsg(_socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
		}

		private void TestPutNewComplete() {
			var asset = new Asset(
				Guid.NewGuid().ToString(),
				7, // Notecard
				false,
				false,
				(int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds,
				"New Asset",
				"Test of a new asset",
				System.Text.Encoding.UTF8.GetBytes("Just some data.")
			);

			var request = new ClientRequestMsg(ClientRequestMsg.RequestType.PUT, asset.Uuid, asset.Serialize().data);
			request.Send(_socket);
			// Wait until response comes back.
			while (_socket.Available <= 0) {
			}
#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new ServerResponseMsg(_socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
		}

		private void TestPutNewComplete2MB() {
			var data = new byte[2097152];

			RandomUtil.Rnd.NextBytes(data);

			var asset = new Asset(
				Guid.NewGuid().ToString(),
				7, // Notecard
				false,
				false,
				(int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds,
				"2MB Asset",
				"Test of a larger asset",
				data
			);

			var request = new ClientRequestMsg(ClientRequestMsg.RequestType.PUT, asset.Uuid, asset.Serialize().data);
			request.Send(_socket);
			// Wait until response comes back.
			while (_socket.Available <= 0) {
			}
#pragma warning disable RECS0026 // Possible unassigned object created by 'new'
			new ServerResponseMsg(_socket);
#pragma warning restore RECS0026 // Possible unassigned object created by 'new'
		}

		#region Connection Utilities

		public const byte CHALLENGE_PACKET_IDENTIFIER = 0;
		public const ushort CHALLENGE_MESSAGE_SIZE = 8;
		public const ushort CHALLENGE_SIZE = 7;
		public const byte RESPONSE_PACKET_IDENTIFIER = 0;
		public const ushort RESPONSE_PACKET_MESSAGE_SIZE = 41;
		public const byte STATUS_PACKET_IDENTIFIER = 1;
		public const byte STATUS_PACKET_MESSAGE_SIZE = 2;

		public enum StatusType : byte {
			AS_SUCCESS = 0,
			AS_FAILURE = 1
		}

		private static byte[] GetData(Socket conn, uint messageSize) {
			var soFar = 0;
			var buffer = new byte[1024];

			var result = new List<byte>();

			while (soFar < messageSize) {
				//read the header and challenge phrase
				var rcvd = conn.Receive(buffer, 1024, SocketFlags.None);
				if (rcvd == 0) return null; // Upstream disconnected

				result.AddRange(buffer);

				soFar += rcvd;
			}

			return result.Take((int)messageSize).ToArray();
		}

		private static byte[] GenerateAuthResponsePacket(byte[] challengeBytes, string password) {
			//convert the password to ascii
			var encoding = new System.Text.ASCIIEncoding();
			var asciiPW = encoding.GetBytes(password);

			//add the two ranges together and compute the hash
			var authString = new AppendableByteArray(asciiPW.Length + challengeBytes.Length);
			authString.Append(asciiPW);
			authString.Append(challengeBytes);

			var sha = new SHA1CryptoServiceProvider();
			byte[] challengeHash = sha.ComputeHash(authString.data);

			//copy the results to the raw packet data
			var rawMessageData = new AppendableByteArray(RESPONSE_PACKET_MESSAGE_SIZE);
			rawMessageData.Append(RESPONSE_PACKET_IDENTIFIER);
			rawMessageData.Append(encoding.GetBytes(Util.HashToHex(challengeHash)));

			return rawMessageData.data;
		}

		private static void Send(Socket conn, byte[] rawMessageData) {
			int amtSent = 0;

			while (amtSent < rawMessageData.Length) {
				amtSent += conn.Send(rawMessageData, amtSent, rawMessageData.Length - amtSent, SocketFlags.None);
			}
		}

		private static Socket Connect(string address, int port, string password) {
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			socket.Connect(address, port);

			// Get the challenge
			var challengeData = GetData(socket, CHALLENGE_MESSAGE_SIZE);
			if (challengeData == null) {
				throw new AuthException("Service disconnected during reception of challenge.");
			}

			var challenge = challengeData.Skip(1).Take(CHALLENGE_SIZE).ToArray();

			// Send the auth response
			var responsePacket = GenerateAuthResponsePacket(challenge, password);
			Send(socket, responsePacket);

			// Receive the auth status
			var statusData = GetData(socket, STATUS_PACKET_MESSAGE_SIZE);
			if (statusData == null) {
				throw new AuthException("Service disconnected during reception of status.");
			}

			if (statusData[1] != (byte)StatusType.AS_SUCCESS) {
				throw new AuthException("Service refused password.");
			}

			return socket;
		}

		#endregion

		#region IDisposable Support
		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					LOG.Debug($"Cleaning up after {nameof(WhipServiceTest)}...");

					_socket.Dispose();

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
					try {
						System.IO.Directory.Delete("SpeedWhipServiceTest", true);
					}
					catch (Exception) {
					}
					try {
						System.IO.File.Delete("SpeedWhipServiceTest.wcache");
					}
					catch (Exception) {
					}
					try {
						System.IO.File.Delete("SpeedWhipServiceTest.pid");
					}
					catch (Exception) {
					}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

					LOG.Debug($"Clean up after {nameof(WhipServiceTest)} complete.");
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.

				// set large fields to null.
				_socket = null;

				disposedValue = true;
			}
		}

		// override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~WhipServiceTest() {
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
