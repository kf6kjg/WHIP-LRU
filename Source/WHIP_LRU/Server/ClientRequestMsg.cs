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
using System;
using System.Collections.Generic;

namespace WHIP_LRU.Server {
	public class ClientRequestMsg {
		private const int HEADER_SIZE = 37;
		private const int DATA_SIZE_MARKER_LOC = 33;

		private enum RequestType {
			RT_GET = 10,
			RT_PUT = 11,
			RT_PURGE = 12,
			RT_TEST = 13,
			RT_MAINT_PURGELOCALS = 14,
			RT_STATUS_GET = 15,
			RT_STORED_ASSET_IDS_GET = 16,
			RT_GET_DONTCACHE = 17
		};

		public ClientRequestMsg() {
		}

		private List<byte> _data = new List<byte>();
		public bool AddRange(IEnumerable<byte> data) {
			_data.AddRange(data);

			// TODO: determine if the added range completes the message or not.

			return true;
		}

	}
}
