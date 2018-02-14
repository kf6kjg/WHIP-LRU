// TestAuthStatusMsg.cs
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
	public static class TestAuthStatusMsg {
		private const short MESSAGE_SIZE = 2;
		private const byte PACKET_IDENTIFIER = 1;

		#region Ctor()

		[Test]
		public static void TestAuthStatusMsg_Ctor1_ASFail_DoesNotThrow() {
			Assert.DoesNotThrow(() => new AuthStatusMsg(AuthStatusMsg.StatusType.AS_FAILURE));
		}

		[Test]
		public static void TestAuthStatusMsg_Ctor1_ASSuccess_DoesNotThrow() {
			Assert.DoesNotThrow(() => new AuthStatusMsg(AuthStatusMsg.StatusType.AS_SUCCESS));
		}

		#endregion

		#region ToByteArray

		[Test]
		public static void TestAuthStatusMsg_ASFail_ToByteArray_DoesNotThrow() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_FAILURE);
			Assert.DoesNotThrow(() => statusMsg.ToByteArray());
		}

		[Test]
		public static void TestAuthStatusMsg_ASFail_ToByteArray_CorrectSize() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_FAILURE);
			var packet = statusMsg.ToByteArray();
			Assert.AreEqual(MESSAGE_SIZE, packet.Length);
		}

		[Test]
		public static void TestAuthStatusMsg_ASFail_ToByteArray_NotNull() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_FAILURE);
			var packet = statusMsg.ToByteArray();
			Assert.NotNull(packet);
		}

		[Test]
		public static void TestAuthStatusMsg_ASFail_ToByteArray_CorrectHeader() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_FAILURE);
			var packet = statusMsg.ToByteArray();
			Assert.AreEqual(PACKET_IDENTIFIER, packet[0]);
		}

		[Test]
		public static void TestAuthStatusMsg_ASFail_ToByteArray_CorrectStatus() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_FAILURE);
			var packet = statusMsg.ToByteArray();

			Assert.AreEqual(1, packet[1]);
		}


		[Test]
		public static void TestAuthStatusMsg_ASSuccess_ToByteArray_DoesNotThrow() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_SUCCESS);
			Assert.DoesNotThrow(() => statusMsg.ToByteArray());
		}

		[Test]
		public static void TestAuthStatusMsg_ASSuccess_ToByteArray_CorrectSize() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_SUCCESS);
			var packet = statusMsg.ToByteArray();
			Assert.AreEqual(MESSAGE_SIZE, packet.Length);
		}

		[Test]
		public static void TestAuthStatusMsg_ASSuccess_ToByteArray_NotNull() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_SUCCESS);
			var packet = statusMsg.ToByteArray();
			Assert.NotNull(packet);
		}

		[Test]
		public static void TestAuthStatusMsg_ASSuccess_ToByteArray_CorrectHeader() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_SUCCESS);
			var packet = statusMsg.ToByteArray();
			Assert.AreEqual(PACKET_IDENTIFIER, packet[0]);
		}

		[Test]
		public static void TestAuthStatusMsg_ASSuccess_ToByteArray_CorrectStatus() {
			var statusMsg = new AuthStatusMsg(AuthStatusMsg.StatusType.AS_SUCCESS);
			var packet = statusMsg.ToByteArray();

			Assert.AreEqual(0, packet[1]);
		}

		#endregion
	}
}
