// TestClientRequestMsg.cs
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
using System.Text;
using LibWhipLru.Server;
using NUnit.Framework;
using static InWorldz.Whip.Client.ClientRequestMsg;

namespace LibWhipLruTests.Server {
	[TestFixture]
	public static class TestClientRequestMsg {
		private const short REQUEST_TYPE_LOC = 0;
		private const short UUID_TAG_LOCATION = 1;
		private const short UUID_LEN = 32;
		private const short DATA_SIZE_MARKER_LOC = 33; // In big-endian format.
		private const short HEADER_SIZE = 37;

		#region Ctor (default as there's none defined)

		[Test]
		public static void TestClientRequestMsg_Ctor_DoesntThrow() {
			Assert.DoesNotThrow(() => new ClientRequestMsg());
		}

		#endregion

		#region AddRange

		[Test]
		public static void TestClientRequestMsg_AddRange_Null_ArgumentNullException() {
			var msg = new ClientRequestMsg();

			Assert.Throws<ArgumentNullException>(() => msg.AddRange(null));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_Empty_DoesntThrow() {
			var msg = new ClientRequestMsg();

			Assert.DoesNotThrow(() => msg.AddRange(new byte[] { }));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_LessThanHeader_DoesntThrow() {
			var msg = new ClientRequestMsg();

			var data = new byte[HEADER_SIZE - 1];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.DoesNotThrow(() => msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_AfterHeaderOneByte_DoesntThrow() {
			var msg = new ClientRequestMsg();

			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);
			data[DATA_SIZE_MARKER_LOC + 3] = 1;
			msg.AddRange(data);

			Assert.DoesNotThrow(() => msg.AddRange(new byte[] { 0 }));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_AfterHeader1OneByte_True() {
			var msg = new ClientRequestMsg();

			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);
			data[DATA_SIZE_MARKER_LOC + 3] = 1;
			msg.AddRange(data);

			Assert.IsTrue(msg.AddRange(new byte[] { 0 }));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_AfterHeader2OneByte_False() {
			var msg = new ClientRequestMsg();

			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);
			data[DATA_SIZE_MARKER_LOC + 3] = 2;
			msg.AddRange(data);

			Assert.IsFalse(msg.AddRange(new byte[] { 0 }));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_CompleteHeaderNoData_True() {
			var msg = new ClientRequestMsg();

			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.IsTrue(msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_WrongHeaderId_AssetProtocolError() {
			var msg = new ClientRequestMsg();
			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = 0; // Anything not in range of RequestType.
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.Throws<InWorldz.Whip.Client.AssetProtocolError>(() => msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_PartialHeader_DoesntThrow() {
			var msg = new ClientRequestMsg();
			var data = new byte[HEADER_SIZE - 1];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.DoesNotThrow(() => msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_PartialHeader_False() {
			var msg = new ClientRequestMsg();
			var data = new byte[HEADER_SIZE - 1];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.IsFalse(msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_PartialHeaderCompleted_DoesntThrow() {
			var msg = new ClientRequestMsg();
			var data1 = new byte[HEADER_SIZE - 1];
			data1[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data1, UUID_TAG_LOCATION, UUID_LEN);
			msg.AddRange(data1);

			var data2 = new byte[1];
			data2[0] = 0;
			Assert.DoesNotThrow(() => msg.AddRange(data2));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_PartialHeaderCompleted_True() {
			var msg = new ClientRequestMsg();
			var data1 = new byte[HEADER_SIZE - 1];
			data1[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data1, UUID_TAG_LOCATION, UUID_LEN);
			msg.AddRange(data1);

			var data2 = new byte[1];
			data2[0] = 0;
			Assert.IsTrue(msg.AddRange(data2));
		}

		[Test]
		public static void TestClientRequestMsg_AddRange_CompleteHeaderNoData_DoesntThrow() {
			var msg = new ClientRequestMsg();
			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.DoesNotThrow(() => msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRangeCompleteHeaderNoData_True() {
			var msg = new ClientRequestMsg();
			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);

			Assert.IsTrue(msg.AddRange(data));
		}

		[Test]
		public static void TestClientRequestMsg_AddRangeCompleteHeaderNegativeDate_DoesntThrow() {
			var msg = new ClientRequestMsg();
			var data = new byte[HEADER_SIZE];
			data[REQUEST_TYPE_LOC] = (byte)RequestType.TEST;
			Buffer.BlockCopy(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")), 0, data, UUID_TAG_LOCATION, UUID_LEN);
			data[DATA_SIZE_MARKER_LOC] = 0xFF;
			data[DATA_SIZE_MARKER_LOC + 1] = 0xFF;
			data[DATA_SIZE_MARKER_LOC + 2] = 0xFF;
			data[DATA_SIZE_MARKER_LOC + 3] = 0xFF;

			Assert.DoesNotThrow(() => msg.AddRange(data));
		}

		#endregion


	}
}
