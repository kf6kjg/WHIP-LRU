// ServerResponseMsg.cs
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
using System.Net;
using System.Text;
using InWorldz.Whip.Client;
using OpenMetaverse;

namespace WHIP_LRU.Server {
	public class ServerResponseMsg {
		private const short DATA_SZ_TAG_LOC = HEADER_SIZE - 4;
		private const short HEADER_SIZE = 37;
		private const short UUID_TAG_LOC = 1;
		private const short UUID_LEN = 32;

		//48 MB max data size
		private const int MAX_DATA_SIZE = 50331648;

		private UUID _assetId;
		private ResponseCode _code;
		private byte[] _data;

		private ServerResponseMsg() {
		}

		public ServerResponseMsg(ResponseCode code, UUID assetId) {
			_assetId = assetId;
			_code = code;
		}

		public ServerResponseMsg(ResponseCode code, UUID assetId, byte[] data) : this(code, assetId) {
			if (data.Length + HEADER_SIZE <= MAX_DATA_SIZE) {
				_data = new byte[data.Length];
				Buffer.BlockCopy(data, 0, _data, 0, data.Length * sizeof(byte)); // Yes, sizeof(byte) is redundant, but it's also good documentation.
			}
			else {
				throw new AssetProtocolError("Exceeded protocol size limit.");
			}
		}

		public ServerResponseMsg(ResponseCode code, UUID assetId, string message) : this(code, assetId) {
			var encoding = new UTF8Encoding();

			if (encoding.GetByteCount(message) + HEADER_SIZE <= MAX_DATA_SIZE) {
				_data = encoding.GetBytes(message);
			}
			else {
				throw new AssetProtocolError("Exceeded protocol size limit.");
			}
		}

		public string GetHeaderSummary() {
			return $"Code: {_code}, AssetID: {_assetId}, Size: {_data?.Length}";
		}

		public byte[] ToByteArray() {
			var output = new byte[HEADER_SIZE + _data.Length];
			/* Structure of message:
			 * (1 byte) ResponseCode
			 * (32 bytes) UUID
			 * (4 bytes) size
			 * data block
			 */
			output[0] = (byte)_code;

			_assetId.ToBytes(output, UUID_TAG_LOC);

			Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(_data.Length)), 0, output, DATA_SZ_TAG_LOC, 4);

			Buffer.BlockCopy(_data, 0, output, HEADER_SIZE, _data.Length * sizeof(byte)); // Yes, sizeof(byte) is redundant, but it's also good documentation.

			return output;
		}

		public enum ResponseCode {
			RC_FOUND = 10,
			RC_NOTFOUND = 11,
			RC_ERROR = 12,
			RC_OK = 13,
		};
	}
}
