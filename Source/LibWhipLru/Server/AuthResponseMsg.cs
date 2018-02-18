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
using System.Security.Cryptography;
using System.Text;
using InWorldz.Whip.Client;

namespace LibWhipLru.Server {
	/// <summary>
	/// Message from a client to the server in response to the auth challenge.
	/// Designed from the server's perspective.
	/// </summary>
	public class AuthResponseMsg : IByteArrayAppendable {
		private const short MESSAGE_SIZE = 41;
		private const byte PACKET_IDENTIFIER = 0;
		private const byte CHALLENGE_HASH_LOC = 1;
		private const byte CHALLENGE_HASH_LENGTH = MESSAGE_SIZE - CHALLENGE_HASH_LOC;

		private readonly List<byte> _rawMessageData = new List<byte>();

		/// <summary>
		/// Gets the challenge hash the client sent.
		/// </summary>
		/// <value>The challenge hash.</value>
		public string ChallengeHash { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:LibWhipLru.Server.AuthResponseMsg"/> is ready to be read.
		/// </summary>
		/// <value><c>true</c> if is ready; otherwise, <c>false</c>.</value>
		public bool IsReady { get; private set; }

		/// <summary>
		/// Adds the incoming data to the buffer.
		/// </summary>
		/// <returns><c>true</c>, if range was added, <c>false</c> otherwise.</returns>
		/// <param name="data">Data.</param>
		public bool AddRange(byte[] data) {
			if (IsReady) { // Refuse to append more data once loaded.
				throw new InvalidOperationException("You cannot reuse messages!");
			}

			_rawMessageData.AddRange(data);

			if (_rawMessageData.Count >= MESSAGE_SIZE) {
				var packet = _rawMessageData.GetRange(0, MESSAGE_SIZE).ToArray();

				if (packet[0] != PACKET_IDENTIFIER) {
					throw new AssetProtocolError($"Wrong packet identifier for authentication response: {packet[0]}");
				}

				var encoding = new ASCIIEncoding();

				ChallengeHash = encoding.GetString(packet, CHALLENGE_HASH_LOC, CHALLENGE_HASH_LENGTH);

				IsReady = true;
			}

			return IsReady;
		}

		/// <summary>
		/// Computes the challenge hash from the challengeBytes and the password. The client has a matching function to create the same.
		/// </summary>
		/// <returns>The challenge hash.</returns>
		/// <param name="challengeBytes">Challenge bytes.</param>
		/// <param name="password">Password.</param>
		public static string ComputeChallengeHash(byte[] challengeBytes, string password) {
			// By-and-large the following is nearly verbatim from the InWorldz.Whip.Client.AuthResponse construcutor.

			//convert the password to ascii
			var encoding = new ASCIIEncoding();
			var asciiPW = encoding.GetBytes(password ?? string.Empty);

			var authString = new AppendableByteArray(asciiPW.Length + challengeBytes.Length);
			authString.Append(asciiPW);
			authString.Append(challengeBytes);

			var sha = new SHA1CryptoServiceProvider();
			var challengeHash = sha.ComputeHash(authString.data);

			return InWorldz.Whip.Client.Util.HashToHex(challengeHash);
		}
	}
}
