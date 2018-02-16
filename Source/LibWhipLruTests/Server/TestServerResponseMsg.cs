// TestServerResponseMsg.cs
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
using System.Text;
using LibWhipLru.Server;
using NUnit.Framework;

namespace LibWhipLruTests.Server {
	public static class TestServerResponseMsg {
		private const short DATA_SZ_TAG_LOC = HEADER_SIZE - 4;
		private const short HEADER_SIZE = 37;
		private const short UUID_TAG_LOC = 1;
		private const short UUID_LEN = 32;

		//48 MB max data size
		private const int MAX_DATA_SIZE = 50331648;

		#region Ctor(ResponseCode, Guid)

		[Test]
		public static void TestServerResponseMsg_Ctor2_GuidRandom_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.NewGuid()));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor2_GuidEmpty_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty));
		}

		#endregion

		#region Ctor(ResponseCode, Guid, byte[])

		[Test]
		public static void TestServerResponseMsg_Ctor3Bytes_GuidRandom_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.NewGuid(), new byte[] { 0 }));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3Bytes_GuidEmpty_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, new byte[] { 0 }));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3Bytes_DataNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, (byte[])null));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3Bytes_DataEmpty_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, new byte[] { }));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3Bytes_DataMaxSize_DoesntThrow() {
			var data = new byte[MAX_DATA_SIZE];
			Assert.Throws<InWorldz.Whip.Client.AssetProtocolError>(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, data));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3Bytes_DataOversize_AssetProtocolError() {
			var data = new byte[MAX_DATA_SIZE + 1];
			Assert.Throws<InWorldz.Whip.Client.AssetProtocolError>(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, data));
		}

		#endregion

		#region Ctor(ResponseCode, Guid, string)

		[Test]
		public static void TestServerResponseMsg_Ctor3String_GuidRandom_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.NewGuid(), "asdf"));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3String_GuidEmpty_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, "asdf"));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3String_MessageNull_ArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, (string)null));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3String_MessageEmpty_DoesNotThrow() {
			Assert.DoesNotThrow(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, string.Empty));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3String_MessageMaxSize_DoesntThrow() {
			var message = Encoding.UTF8.GetString(new byte[MAX_DATA_SIZE]);
			Assert.Throws<InWorldz.Whip.Client.AssetProtocolError>(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, message));
		}

		[Test]
		public static void TestServerResponseMsg_Ctor3String_MessageOversize_AssetProtocolError() {
			var message = Encoding.UTF8.GetString(new byte[MAX_DATA_SIZE + 1]);
			Assert.Throws<InWorldz.Whip.Client.AssetProtocolError>(() => new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, message));
		}

		#endregion

		#region GetHeaderSummary

		[Test]
		public static void TestServerResponseMsg_GetHeaderSummary_ContainsStatusOk() {
			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_OK,
				Guid.Empty,
				new byte[11]
			);

			Assert.That(msg.GetHeaderSummary(), Contains.Substring("RC_OK"));
		}

		[Test]
		public static void TestServerResponseMsg_GetHeaderSummary_ContainsStatusError() {
			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_ERROR,
				Guid.Empty,
				new byte[11]
			);

			Assert.That(msg.GetHeaderSummary(), Contains.Substring("RC_ERROR"));
		}

		[Test]
		public static void TestServerResponseMsg_GetHeaderSummary_ContainsAssetIdEmpty() {
			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_ERROR,
				Guid.Empty,
				new byte[11]
			);

			Assert.That(msg.GetHeaderSummary(), Contains.Substring(Guid.Empty.ToString("D")));
		}

		[Test]
		public static void TestServerResponseMsg_GetHeaderSummary_ContainsAssetIdRandom() {
			var id = Guid.NewGuid();
			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_ERROR,
				id,
				new byte[11]
			);

			Assert.That(msg.GetHeaderSummary(), Contains.Substring(id.ToString("D")));
		}

		[Test]
		public static void TestServerResponseMsg_GetHeaderSummary_ContainsDataSize11() {
			var id = Guid.NewGuid();
			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_ERROR,
				id,
				new byte[11]
			);

			Assert.That(msg.GetHeaderSummary(), Contains.Substring("11"));
		}

		[Test]
		public static void TestServerResponseMsg_GetHeaderSummary_ContainsDataSizeRandom() {
			var size = RandomUtil.NextUInt() % 4096;
			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_ERROR,
				Guid.Empty,
				new byte[size]
			);

			Assert.That(msg.GetHeaderSummary(), Contains.Substring(size.ToString()));
		}

		#endregion

		#region ToByteArray

		[Test]
		public static void TestServerResponseMsg_RCFOUND_Bytes_ToByteArray_DoesNotThrow() {
			var data = new byte[20];
			RandomUtil.Rnd.NextBytes(data);

			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_FOUND,
				Guid.NewGuid(),
				data
			);

			Assert.DoesNotThrow(() => msg.ToByteArray());
		}

		[Test]
		public static void TestServerResponseMsg_RCFOUND_Bytes_ToByteArray_CorrectSize() {
			var data = new byte[20];
			RandomUtil.Rnd.NextBytes(data);

			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_FOUND,
				Guid.NewGuid(),
				data
			);

			var packet = msg.ToByteArray();
			Assert.AreEqual(HEADER_SIZE + data.Length, packet.Length);
		}

		[Test]
		public static void TestServerResponseMsg_RCFOUND_Bytes_ToByteArray_NotNull() {
			var data = new byte[20];
			RandomUtil.Rnd.NextBytes(data);

			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_FOUND,
				Guid.NewGuid(),
				data
			);

			var packet = msg.ToByteArray();
			Assert.NotNull(packet);
		}

		[Test]
		public static void TestServerResponseMsg_RCFOUND_Bytes_ToByteArray_CorrectHeader() {
			var data = new byte[] {
				255, 254, 253, 252, 251
			};

			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_FOUND,
				Guid.Parse("0123456789abcdef0123456789abcdef"),
				data
			);

			/* Structure of message:
			 * (1 byte) ResponseCode
			 * (32 bytes) UUID
			 * (4 bytes) size, big endian (MSB first)
			 * data block
			 */
			var expectedHeader = new byte[] {
				10,
				48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
				48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
				/*data.Length*/0, 0, 0, 5,
			};

			var packet = msg.ToByteArray();
			var packetHeader = new byte[HEADER_SIZE];
			Buffer.BlockCopy(packet, 0, packetHeader, 0, packetHeader.Length);

			Assert.AreEqual(expectedHeader, packetHeader, $"Data received:\n{string.Join(", ", packetHeader)}");
		}

		[Test]
		public static void TestServerResponseMsg_RCFOUND_Bytes_ToByteArray_CorrectData() {
			var data = new byte[] {
				255, 254, 253, 252, 251
			};

			var msg = new ServerResponseMsg(
				ServerResponseMsg.ResponseCode.RC_FOUND,
				Guid.Parse("0123456789abcdef0123456789abcdef"),
				data
			);

			/* Structure of message:
			 * (1 byte) ResponseCode
			 * (32 bytes) UUID
			 * (4 bytes) size, big endian (MSB first)
			 * data block
			 */
			var expectedHeader = new byte[] {
				10,
				48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
				48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
				/*data.Length*/0, 0, 0, 5,
				/*data*/255, 254, 253, 252, 251
			};

			var packet = msg.ToByteArray();

			Assert.AreEqual(expectedHeader, packet, $"Data received:\n{string.Join(", ", packet)}");
		}

		#endregion
	}
}
