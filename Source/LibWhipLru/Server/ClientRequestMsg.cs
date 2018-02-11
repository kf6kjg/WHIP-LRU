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
using System.Text;
using InWorldz.Whip.Client;
using static InWorldz.Whip.Client.ClientRequestMsg;

namespace LibWhipLru.Server {
	/// <summary>
	/// Message from an authorized client to the server to request an asset.
	/// Designed from the server's perspective.
	/// </summary>
	public class ClientRequestMsg : IByteArrayAppendable {
		private const short REQUEST_TYPE_LOC = 0;
		private const short DATA_SIZE_MARKER_LOC = 33;
		private const short HEADER_SIZE = 37;
		private const short UUID_TAG_LOCATION = 1;
		private const short UUID_LEN = 32;

		private long _bytesRead;

		/// <summary>
		/// Gets the request type.
		/// </summary>
		/// <value>The type.</value>
		public RequestType Type { get; private set; }
		private bool _typeIsReady;

		/// <summary>
		/// Gets the asset identifier requested.
		/// </summary>
		/// <value>The asset identifier.</value>
		public Guid AssetId { get; private set; }
		private bool _assetIdIsReady;

		/// <summary>
		/// Access to the data byte array that was sent from the client as payload.
		/// </summary>
		public byte[] Data;
		private int _dataWritePointer = 0;

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:LibWhipLru.Server.ClientRequestMsg"/> is ready.
		/// </summary>
		/// <value><c>true</c> if is ready; otherwise, <c>false</c>.</value>
		public bool IsReady { get; private set; }

		/// <summary>
		/// Gets a header summary useful in logging.
		/// </summary>
		/// <returns>The header summary.</returns>
		public string GetHeaderSummary() {
			return $"Type: {Type}, AssetID: {AssetId}, Size: {Data?.Length}";
		}

		/// <summary>
		/// Adds the incoming raw data to the message.
		/// </summary>
		/// <returns><c>true</c>, if all expected data is received, <c>false</c> otherwise.</returns>
		/// <param name="data">Data.</param>
		public bool AddRange(byte[] data) {
			if (data.Length < HEADER_SIZE) { // Only allow packets big enough to contain the whole header.
				throw new ArgumentOutOfRangeException(nameof(data), $"Data buffer MUST be at least {HEADER_SIZE} bytes long.");
			}

			if (IsReady) { // Refuse to append more data once loaded.
				throw new InvalidOperationException("You cannot reuse messages!");
			}

			_bytesRead += data.Length;

			if (!_typeIsReady && _bytesRead >= REQUEST_TYPE_LOC + 1) {
				var type = data[REQUEST_TYPE_LOC];
				if (typeof(RequestType).IsEnumDefined((int)type)) {
					Type = (RequestType)type;
					_typeIsReady = true;
				}
				else {
					throw new AssetProtocolError($"Invalid result type in server response. Type value sent: {type}");
				}
			}

			if (!_assetIdIsReady && _bytesRead >= UUID_TAG_LOCATION + UUID_LEN) {
				var idString = Encoding.ASCII.GetString(data, UUID_TAG_LOCATION, UUID_LEN);
				Guid id;
				if (Guid.TryParse(idString, out id)) {
					AssetId = id;
					_assetIdIsReady = true;
				}
				else {
					throw new AssetProtocolError($"Invalid UUID in server response. Header summary: Type = {Type}, AssetId = {idString}");
				}
			}

			if (Data == null && _bytesRead >= DATA_SIZE_MARKER_LOC + 4) {
				var dataSize = Math.Max(0, InWorldz.Whip.Client.Util.NTOHL(data, DATA_SIZE_MARKER_LOC)); // No, you don't get to send me negative numbers.
				Data = new byte[dataSize];

				var bytesToRead = Math.Min(dataSize, data.Length - HEADER_SIZE);

				if (dataSize > 0) {
					// We are processing a packet of data that has the header.
					Buffer.BlockCopy(data, HEADER_SIZE, Data, 0, bytesToRead);
				}

				_dataWritePointer = bytesToRead;
			}
			else {
				// Processing a pure data packet.
				var bytesToRead = Math.Min(data.Length, Data.Length - _dataWritePointer);

				Buffer.BlockCopy(data, 0, Data, _dataWritePointer, bytesToRead);

				_dataWritePointer += bytesToRead;
			}

			IsReady = _dataWritePointer >= Data.Length;

			return IsReady;
		}
	}
}
