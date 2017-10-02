// ClientRequestMsg.cs
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
using OpenMetaverse;
using InWorldz.Whip.Client;
using System.Text;

namespace WHIP_LRU.Server {
	public class ClientRequestMsg : IByteArrayAppendable {
		private const short REQUEST_TYPE_LOC = 0;
		private const short DATA_SIZE_MARKER_LOC = 33;
		private const short HEADER_SIZE = 37;
		private const short UUID_TAG_LOCATION = 1;
		private const short UUID_LEN = 32;

		private readonly List<byte> _rawMessageData = new List<byte>();

		public byte[] Data => _rawMessageData?.Skip(HEADER_SIZE).ToArray();
		public UUID AssetId { get; private set; }
		public bool IsReady { get; private set; }
		public RequestType Type { get; private set; }

		public string GetHeaderSummary() {
			return $"Type: {Type}, AssetID: {AssetId}, Size: {Data?.Length}";
		}

		public bool AddRange(IEnumerable<byte> data) {
			if (!IsReady) { // Refuse to append more data once loaded.
				_rawMessageData.AddRange(data);

				if (_rawMessageData.Count >= HEADER_SIZE) {
					var header = _rawMessageData.Take(HEADER_SIZE).ToArray();

					// We've enough of the header to determine size.
					var dataSize = InWorldz.Whip.Client.Util.NTOHL(header, DATA_SIZE_MARKER_LOC);
					var packetSize = HEADER_SIZE + dataSize;

					if (packetSize > REQUEST_TYPE_LOC && _rawMessageData.Count >= packetSize) {
						// Load the class up with the data.
						var type = header[REQUEST_TYPE_LOC];
						if (typeof(RequestType).IsEnumDefined(type)) {
							Type = (RequestType)type;
						}
						else {
							throw new AssetProtocolError($"Invalid result type in server response. Header summary: {GetHeaderSummary(header)}");
						}

						var idString = Encoding.ASCII.GetString(header, UUID_TAG_LOCATION, UUID_LEN);
						UUID id;
						if (UUID.TryParse(idString, out id)) {
							AssetId = id;
						}
						else {
							throw new AssetProtocolError($"Invalid UUID in server response. Header summary: {GetHeaderSummary(header)}");
						}

						IsReady = true;

						// If all the expected data has arrived, return true, else false.
					}
				}
			}

			return IsReady;
		}

		private static string GetHeaderSummary(byte[] header) {
			return $"Type: {header[REQUEST_TYPE_LOC]}, AssetID: {Encoding.ASCII.GetString(header, UUID_TAG_LOCATION, UUID_LEN)}, Size: {InWorldz.Whip.Client.Util.NTOHL(header, DATA_SIZE_MARKER_LOC)}";
		}

		public enum RequestType : byte {
			RT_GET = 10,
			RT_PUT = 11,
			RT_PURGE = 12,
			RT_TEST = 13,
			RT_MAINT_PURGELOCALS = 14,
			RT_STATUS_GET = 15,
			RT_STORED_ASSET_IDS_GET = 16,
			RT_GET_DONTCACHE = 17,
		};
	}
}
