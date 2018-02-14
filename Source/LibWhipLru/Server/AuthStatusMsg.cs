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

namespace LibWhipLru.Server {
	/// <summary>
	/// Message from the server to a client that has just either completed or failed a connection.
	/// Designed from the server's perspective.
	/// </summary>
	public class AuthStatusMsg : IByteArraySerializable {
		private const short MESSAGE_SIZE = 2;
		private const byte PACKET_IDENTIFIER = 1;

		private StatusType _status;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Server.AuthStatusMsg"/> class.
		/// </summary>
		/// <param name="status">Status.</param>
		public AuthStatusMsg(StatusType status) {
			_status = status;
		}

		/// <summary>
		/// Converts to a byte array for sending across the wire.
		/// </summary>
		/// <returns>The byte array.</returns>
		public byte[] ToByteArray() {
			var output = new byte[MESSAGE_SIZE];
			/* Structure of message:
			 * (1 byte) Packet ID
			 * (1 byte) Status
			 */
			output[0] = PACKET_IDENTIFIER;
			output[1] = (byte)_status;

			return output;
		}

		/// <summary>
		/// Contains the auth status.
		/// </summary>
		public enum StatusType : byte {
			AS_SUCCESS = 0,
			AS_FAILURE = 1,
		}
	}
}
