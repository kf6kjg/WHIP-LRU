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
		/// Will only contain data when the client has finished sending all the expected bytes.
		/// </summary>
		public byte[] Data { get => _dataWritePointer >= _data.Length ? _data : new byte[] { }; }
		private byte[] _data;
		private int _dataWritePointer;
		private readonly byte[] _dataHeader = new byte[HEADER_SIZE];
		private int _dataHeaderWritePointer;

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:LibWhipLru.Server.ClientRequestMsg"/> is has completed receiving all expected packet data.
		/// </summary>
		/// <value><c>true</c> if is ready; otherwise, <c>false</c>.</value>
		public bool IsReady { get; private set; }

		/// <summary>
		/// Gets a header summary useful in logging.
		/// </summary>
		/// <returns>The header summary.</returns>
		public string GetHeaderSummary() {
			return $"Type: {Type}, AssetID: {AssetId}, Size: {_data?.Length}";
		}

		/// <summary>
		/// Adds the incoming raw data to the message.
		/// </summary>
		/// <returns><c>true</c>, if all expected data is received, <c>false</c> otherwise.</returns>
		/// <param name="data">Data.</param>
		public bool AddRange(byte[] data) {
			data = data ?? throw new ArgumentNullException(nameof(data));

			if (IsReady) { // Refuse to append more data once loaded.
				throw new InvalidOperationException("You cannot reuse messages!");
			}

			var headerBytesInData = 0;

			if (_dataHeaderWritePointer < HEADER_SIZE) {
				// Incomplete header, so bring in what we've received, but nothing more than the header.
				headerBytesInData = Math.Min(_dataHeader.Length - _dataHeaderWritePointer, data.Length);
				Buffer.BlockCopy(data, 0, _dataHeader, _dataHeaderWritePointer, headerBytesInData);
				_dataHeaderWritePointer += headerBytesInData;
			}

			if (!_typeIsReady && _dataHeaderWritePointer >= REQUEST_TYPE_LOC + 1) {
				var type = _dataHeader[REQUEST_TYPE_LOC];
				if (typeof(RequestType).IsEnumDefined((int)type)) {
					Type = (RequestType)type;
					_typeIsReady = true;
				}
				else {
					throw new AssetProtocolError($"Invalid result type in client message. Type value sent: {type}");
				}
			}

			if (!_assetIdIsReady && _dataHeaderWritePointer >= UUID_TAG_LOCATION + UUID_LEN) {
				var idString = Encoding.ASCII.GetString(_dataHeader, UUID_TAG_LOCATION, UUID_LEN);
				if (Guid.TryParse(idString, out var id)) {
					AssetId = id;
					_assetIdIsReady = true;
				}
				else {
					throw new AssetProtocolError($"Invalid UUID in client message. Header summary: Type = {Type}, AssetId = {idString}");
				}
			}

			if (_data == null && _dataHeaderWritePointer >= DATA_SIZE_MARKER_LOC + 4) {
				var dataSize = Math.Max(0, InWorldz.Whip.Client.Util.NTOHL(_dataHeader, DATA_SIZE_MARKER_LOC)); // No, you don't get to send me negative numbers.
				_data = new byte[dataSize];

				var bytesToRead = Math.Min(dataSize, data.Length - headerBytesInData); // No trying to send me more than you said you were sending!

				if (dataSize > 0) {
					// We are processing a packet of data that has the header.
					Buffer.BlockCopy(data, headerBytesInData, _data, 0, bytesToRead);
				}

				_dataWritePointer = bytesToRead;
			}
			else if (_data != null) {
				// Processing a pure data packet.
				var bytesToRead = Math.Min(data.Length, _data.Length - _dataWritePointer); // No trying to send me more than you said you were sending!

				Buffer.BlockCopy(data, 0, _data, _dataWritePointer, bytesToRead);

				_dataWritePointer += bytesToRead;
			}

			IsReady = _dataWritePointer >= _data?.Length;

			return IsReady;
		}
	}
}
