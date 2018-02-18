// TestAuthResponseMsg.cs
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
using InWorldz.Whip.Client;
using LibWhipLru.Server;
using NUnit.Framework;

namespace LibWhipLruTests.Server {
	[TestFixture]
	public static class TestAuthResponseMsg {
		private const short MESSAGE_SIZE = 41;
		private const byte CHALLENGE_HASH_LOC = 1;

		private static readonly byte[] CHALLENGE_BYTES = {
			1, 2, 3, 4, 5, 6, 7 // PHRASE_SIZE = 7
		};
		private static readonly string PASSWORD = "password";
		private static readonly string HASH_RESULT = "987441d16f837d52c24dadecbb38a0e86fde327b"; // Precalculated using code directly from InWorldz.Whip.Client C&P'd in rextester: http://rextester.com/FNCCQ76247

		#region Ctor (default as there's none defined)

		[Test]
		public static void TestAuthResponseMsg_Ctor_DoesntThrow() {
			Assert.DoesNotThrow(() => new AuthResponseMsg());
		}

		#endregion

		#region AddRange

		[Test]
		public static void TestAuthResponseMsg_AddRange_Null_ArgumentNullException() {
			var msg = new AuthResponseMsg();
			Assert.Throws<ArgumentNullException>(() => msg.AddRange(null));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_Empty_DoesntThrow() {
			var msg = new AuthResponseMsg();
			Assert.DoesNotThrow(() => msg.AddRange(new byte[] { }));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_Empty_ReturnsFalse() {
			var msg = new AuthResponseMsg();
			Assert.IsFalse(msg.AddRange(new byte[] { }));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_WrongPacketId_AssetProtocolError() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 1; // Anything but 0.
			Assert.Throws<AssetProtocolError>(() => msg.AddRange(data));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_PartialPacket_ReturnsFalse() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE - 1];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			Assert.IsFalse(msg.AddRange(data));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_PartialPacketCompleted_ReturnsTrue() {
			var msg = new AuthResponseMsg();
			var data1 = new byte[MESSAGE_SIZE - 1];
			data1[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data1, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			msg.AddRange(data1);

			var data2 = new byte[1];
			data2[0] = System.Text.Encoding.ASCII.GetBytes(HASH_RESULT)[HASH_RESULT.Length - 1];
			Assert.IsTrue(msg.AddRange(data2));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_FullPacket_DoesntThrow() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length);
			Assert.DoesNotThrow(() => msg.AddRange(data));
		}

		[Test]
		public static void TestAuthResponseMsg_AddRange_FullPacket_ReturnsTrue() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length);
			Assert.IsTrue(msg.AddRange(data));
		}

		#endregion

		#region ChallengeHash

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_Fresh_IsNull() {
			var msg = new AuthResponseMsg();

			Assert.IsNull(msg.ChallengeHash);
		}

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_PartialPacket_IsNull() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE - 1];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			msg.AddRange(data);

			Assert.IsNull(msg.ChallengeHash);
		}

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_PartialPacketCompleted_IsNotNull() {
			var msg = new AuthResponseMsg();
			var data1 = new byte[MESSAGE_SIZE - 1];
			data1[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data1, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			msg.AddRange(data1);

			var data2 = new byte[1];
			data2[0] = System.Text.Encoding.ASCII.GetBytes(HASH_RESULT)[HASH_RESULT.Length - 1];
			msg.AddRange(data2);

			Assert.IsNotNull(msg.ChallengeHash);
		}

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_PartialPacketCompleted_HasCorrectHash() {
			var msg = new AuthResponseMsg();
			var data1 = new byte[MESSAGE_SIZE - 1];
			data1[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data1, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			msg.AddRange(data1);

			var data2 = new byte[1];
			data2[0] = System.Text.Encoding.ASCII.GetBytes(HASH_RESULT)[HASH_RESULT.Length - 1];
			msg.AddRange(data2);

			Assert.AreEqual(HASH_RESULT, msg.ChallengeHash);
		}

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_FullPacket_IsNotNull() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length);
			msg.AddRange(data);

			Assert.IsNotNull(msg.ChallengeHash);
		}

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_FullPacket_IsNotEmpty() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length);
			msg.AddRange(data);

			Assert.IsNotEmpty(msg.ChallengeHash);
		}

		[Test]
		public static void TestAuthResponseMsg_ChallengeHash_FullPacket_HasCorrectHash() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length);
			msg.AddRange(data);

			Assert.AreEqual(HASH_RESULT, msg.ChallengeHash);
		}

		#endregion

		#region IsReady

		[Test]
		public static void TestAuthResponseMsg_IsReady_Fresh_IsFalse() {
			var msg = new AuthResponseMsg();

			Assert.IsFalse(msg.IsReady);
		}

		[Test]
		public static void TestAuthResponseMsg_IsReady_PartialPacket_IsFalse() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE - 1];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			msg.AddRange(data);

			Assert.IsFalse(msg.IsReady);
		}

		[Test]
		public static void TestAuthResponseMsg_IsReady_PartialPacketCompleted_IsTrue() {
			var msg = new AuthResponseMsg();
			var data1 = new byte[MESSAGE_SIZE - 1];
			data1[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data1, CHALLENGE_HASH_LOC, HASH_RESULT.Length - 1);
			msg.AddRange(data1);

			var data2 = new byte[1];
			data2[0] = System.Text.Encoding.ASCII.GetBytes(HASH_RESULT)[HASH_RESULT.Length - 1];
			msg.AddRange(data2);

			Assert.IsTrue(msg.IsReady);
		}

		[Test]
		public static void TestAuthResponseMsg_IsReady_FullPacket_IsTrue() {
			var msg = new AuthResponseMsg();
			var data = new byte[MESSAGE_SIZE];
			data[0] = 0;
			Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(HASH_RESULT), 0, data, CHALLENGE_HASH_LOC, HASH_RESULT.Length);
			msg.AddRange(data);

			Assert.IsTrue(msg.IsReady);
		}

		#endregion

		#region ComputeChallengeHash

		[Test]
		public static void TestAuthResponseMsg_ComputeChallengeHash_DoesntThrow() {
			Assert.DoesNotThrow(() => AuthResponseMsg.ComputeChallengeHash(CHALLENGE_BYTES, PASSWORD));
		}

		[Test]
		public static void TestAuthResponseMsg_ComputeChallengeHash_ComputesCorrectResult() {
			Assert.AreEqual(HASH_RESULT, AuthResponseMsg.ComputeChallengeHash(CHALLENGE_BYTES, PASSWORD));
		}

		#endregion
	}
}
