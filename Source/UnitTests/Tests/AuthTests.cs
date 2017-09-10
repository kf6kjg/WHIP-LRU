// AuthTests.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using InWorldz.Whip.Client;
using NUnit.Framework;

namespace UnitTests.Tests {
	[TestFixture]
	public class AuthTests : SelfHostBase {
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

		[Test]
		[Timeout(200)]
		public void TestAuthChallengeOnConnect() {
			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);

				var challengeData = GetData(socket, CHALLENGE_MESSAGE_SIZE);

				Assert.NotNull(challengeData, "Server disconnected while receiving challenge");

				Assert.AreEqual(CHALLENGE_PACKET_IDENTIFIER, challengeData[0], "Was expecting the ID for a challenge packet.");

				var challenge = challengeData.Skip(1).Take(CHALLENGE_SIZE).ToArray();

				Assert.AreEqual(CHALLENGE_SIZE, challenge.Length, "Wrong number of bytes in challenge.");
			}
		}

		[Test]
		[Timeout(200)]
		public void TestAuthStatusErrorWithInvalidPassword() {
			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);

				// Get the challenge
				var challengeData = GetData(socket, CHALLENGE_MESSAGE_SIZE);
				Assert.NotNull(challengeData, "Server disconnected while receiving challenge!");

				var challenge = challengeData.Skip(1).Take(CHALLENGE_SIZE).ToArray();

				// Send the auth response
				var responsePacket = GenerateAuthResponsePacket(challenge, "notthepassword");
				Send(socket, responsePacket);

				// Receive the auth status
				var statusData = GetData(socket, STATUS_PACKET_MESSAGE_SIZE);
				Assert.NotNull(challengeData, "Server disconnected while receiving challenge!");

				Assert.AreEqual(STATUS_PACKET_IDENTIFIER, statusData[0], "Was expecting the ID for a status packet.");

				Assert.AreEqual((byte)StatusType.AS_FAILURE, statusData[1], "Was expecting this fake password to fail.");
			}
		}

		[Test]
		[Timeout(200)]
		public void TestAuthStatusErrorWithBadPasswordUsingStaticMethod() {
			Assert.DoesNotThrow(() => {
				Connect(password: "notthepassword");
			}, "Static auth method failed.");
		}

		[Test]
		[Timeout(200)]
		public void TestAuthStatusGood() {
			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);

				// Get the challenge
				var challengeData = GetData(socket, CHALLENGE_MESSAGE_SIZE);
				Assert.NotNull(challengeData, "Server disconnected while receiving challenge!");

				var challenge = challengeData.Skip(1).Take(CHALLENGE_SIZE).ToArray();

				// Send the auth response
				var responsePacket = GenerateAuthResponsePacket(challenge, Constants.PASSWORD);
				Send(socket, responsePacket);

				// Receive the auth status
				var statusData = GetData(socket, STATUS_PACKET_MESSAGE_SIZE);
				Assert.NotNull(challengeData, "Server disconnected while receiving challenge!");

				Assert.AreEqual(STATUS_PACKET_IDENTIFIER, statusData[0], "Was expecting the ID for a status packet.");

				Assert.AreEqual((byte)StatusType.AS_SUCCESS, statusData[1], "Was expecting this password to pass.");
			}
		}

		[Test]
		[Timeout(200)]
		public void TestAuthStatusGoodUsingStaticMethod() {
			Assert.DoesNotThrow(() => {
				Connect();
			}, "Static auth method failed.");
		}

		public static byte[] GetData(Socket conn, uint messageSize) {
			var soFar = 0;
			var buffer = new byte[1024];

			var result = new List<byte>();

			while (soFar < messageSize) {
				//read the header and challenge phrase
				int rcvd = conn.Receive(buffer, (int)messageSize - soFar, SocketFlags.None);
				if (rcvd == 0) return null; // Upstream disconnected

				result.AddRange(buffer);

				soFar += rcvd;
			}

			return result.Take((int)messageSize).ToArray();
		}

		public static byte[] GenerateAuthResponsePacket(byte[] challengeBytes, string password) {
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

		public static void Send(Socket conn, byte[] rawMessageData) {
			int amtSent = 0;

			while (amtSent < rawMessageData.Length) {
				amtSent += conn.Send(rawMessageData, amtSent, rawMessageData.Length - amtSent, SocketFlags.None);
			}
		}

		public static Socket Connect(string address = Constants.SERVICE_ADDRESS, int port = Constants.SERVICE_PORT, string password = Constants.PASSWORD) {
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			socket.Connect(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT);

			// Get the challenge
			var challengeData = GetData(socket, CHALLENGE_MESSAGE_SIZE);
			if (challengeData == null) {
				throw new AuthException("Service disconnected during reception of challenge.");
			}

			var challenge = challengeData.Skip(1).Take(CHALLENGE_SIZE).ToArray();

			// Send the auth response
			var responsePacket = GenerateAuthResponsePacket(challenge, Constants.PASSWORD);
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
	}
}
