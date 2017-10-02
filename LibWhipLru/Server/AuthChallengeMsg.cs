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
using System.Security.Cryptography;

namespace WHIP_LRU.Server {
	public class AuthChallengeMsg : IByteArraySerializable {
		private const short MESSAGE_SIZE = 8;
		private const byte PACKET_IDENTIFIER = 0;
		private const short PHRASE_SIZE = 7;
		private const short PHRASE_LOCATION = 1;

		private byte[] _challenge;

		public AuthChallengeMsg() {
			_challenge = new byte[PHRASE_SIZE];
			using (var random = RandomNumberGenerator.Create()) {
				random.GetBytes(_challenge);
			}
		}

		public byte[] GetChallenge() {
			var output = new byte[PHRASE_SIZE];
			Buffer.BlockCopy(_challenge, 0, output, 0, _challenge.Length * sizeof(byte)); // Yes, sizeof(byte) is redundant, but it's also good documentation.
			return output;
		}

		public byte[] ToByteArray() {
			var output = new byte[MESSAGE_SIZE];
			/* Structure of message:
			 * (1 byte) Packet ID
			 * (7 bytes) Token phrase
			 */
			output[0] = PACKET_IDENTIFIER;

			Buffer.BlockCopy(_challenge, 0, output, PHRASE_LOCATION, _challenge.Length * sizeof(byte)); // Yes, sizeof(byte) is redundant, but it's also good documentation.

			return output;
		}

		public enum ResponseCode : byte {
			RC_FOUND = 10,
			RC_NOTFOUND = 11,
			RC_ERROR = 12,
			RC_OK = 13,
		};
	}
}
