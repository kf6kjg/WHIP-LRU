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
	public class ClientRequestMsg {
		private const short DATA_SIZE_MARKER_LOC = 33;
		private const short HEADER_SIZE = 37;
		private const short UUID_TAG_LOCATION = 1;
		private const short UUID_LEN = 32;
		private readonly List<byte> _data = new List<byte>();

		public byte[] Data => _data?.Skip(HEADER_SIZE).ToArray();
		public UUID AssetId { get; private set; }
		public bool IsReady { get; private set; }
		public RequestType Type { get; private set; }

		public string GetHeaderSummary() {
			return $"Type: {Type}, AssetID: {AssetId}, Size: {Data?.Length}";
		}

		public bool AddRange(IEnumerable<byte> data) {
			if (!IsReady) { // Refuse to append more data once loaded.
				_data.AddRange(data);

				if (_data.Count >= HEADER_SIZE) {
					var header = _data.Take(HEADER_SIZE).ToArray();

					// We've enough of the header to determine size.
					var dataSize = InWorldz.Whip.Client.Util.NTOHL(header, DATA_SIZE_MARKER_LOC);
					var packetSize = HEADER_SIZE + dataSize;

					if (packetSize > 0 && _data.Count >= packetSize) {
						// Load the class up with the data.
						var type = header[0];
						if (typeof(RequestType).IsEnumDefined(type)) {
							Type = (RequestType)type;
						}
						else {
							throw new AssetProtocolError("Invalid result type in server response: " + GetHeaderSummary());
						}

						var idBytes = header.Skip(UUID_TAG_LOCATION).Take(UUID_LEN);
						var encoding = new ASCIIEncoding();
						var idString = encoding.GetString(idBytes.ToArray());
						UUID id;
						if (UUID.TryParse(idString, out id)) {
							AssetId = id;
						}
						else {
							throw new AssetProtocolError("Invalid UUID in server response: " + GetHeaderSummary());
						}

						IsReady = true;

						// If all the expected data has arrived, return true, else false.
					}
				}
			}

			return IsReady;
		}

		public enum RequestType {
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
