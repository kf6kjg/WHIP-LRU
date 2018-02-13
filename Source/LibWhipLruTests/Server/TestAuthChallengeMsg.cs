// TestAuthChallengeMsg.cs
//
// Author:
//       Ricky C <>
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
using LibWhipLru.Server;
using NUnit.Framework;

namespace LibWhipLruTests.Server {
	[TestFixture]
	public static class TestAuthChallengeMsg {
		private const short MESSAGE_SIZE = 8;
		private const byte PACKET_IDENTIFIER = 0;
		private const short PHRASE_SIZE = 7;
		private const short PHRASE_LOCATION = 1;

		#region Ctor()

		[Test]
		public static void TestAuthChallengeMsg_Ctor0_DoesNotThrow() {
			Assert.DoesNotThrow(() => new AuthChallengeMsg());
		}

		#endregion

		#region GetChallenge

		[Test]
		public static void TestAuthChallengeMsg_GetChallenge_DoesNotThrow() {
			var challengeMsg = new AuthChallengeMsg();
			Assert.DoesNotThrow(() => challengeMsg.GetChallenge());
		}

		[Test]
		public static void TestAuthChallengeMsg_GetChallenge_CorrectSize() {
			var challengeMsg = new AuthChallengeMsg();
			var challenge = challengeMsg.GetChallenge();
			Assert.AreEqual(PHRASE_SIZE, challenge.Length);
		}

		[Test]
		public static void TestAuthChallengeMsg_GetChallenge_NotNull() {
			var challengeMsg = new AuthChallengeMsg();
			var challenge = challengeMsg.GetChallenge();
			Assert.NotNull(challenge);
		}

		[Test]
		public static void TestAuthChallengeMsg_GetChallenge_NotZeros() {
			var challengeMsg = new AuthChallengeMsg();
			var challenge = challengeMsg.GetChallenge();
			Assert.AreNotEqual(new byte[PHRASE_SIZE], challenge);
		}

		// Can't mock the RNG AFAIK without changing the code, nor do I want the effort of testing its randomness. I'm going to trust the implementation wasn't stupid.

		#endregion

		#region ToByteArray

		[Test]
		public static void TestAuthChallengeMsg_ToByteArray_DoesNotThrow() {
			var challengeMsg = new AuthChallengeMsg();
			Assert.DoesNotThrow(() => challengeMsg.ToByteArray());
		}

		[Test]
		public static void TestAuthChallengeMsg_ToByteArray_CorrectSize() {
			var challengeMsg = new AuthChallengeMsg();
			var packet = challengeMsg.ToByteArray();
			Assert.AreEqual(MESSAGE_SIZE, packet.Length);
		}

		[Test]
		public static void TestAuthChallengeMsg_ToByteArray_NotNull() {
			var challengeMsg = new AuthChallengeMsg();
			var packet = challengeMsg.ToByteArray();
			Assert.NotNull(packet);
		}

		[Test]
		public static void TestAuthChallengeMsg_ToByteArray_CorrectHeader() {
			var challengeMsg = new AuthChallengeMsg();
			var packet = challengeMsg.ToByteArray();
			Assert.AreEqual(PACKET_IDENTIFIER, packet[0]);
		}

		[Test]
		public static void TestAuthChallengeMsg_ToByteArray_ContainsChallenge() {
			var challengeMsg = new AuthChallengeMsg();
			var challenge = challengeMsg.GetChallenge();
			var packet = challengeMsg.ToByteArray();
			var packetChallenge = new byte[PHRASE_SIZE];
			Buffer.BlockCopy(packet, PHRASE_LOCATION, packetChallenge, 0, PHRASE_SIZE);

			Assert.AreEqual(challenge, packetChallenge);
		}

		#endregion
	}
}
