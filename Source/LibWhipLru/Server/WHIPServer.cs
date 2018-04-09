// WHIPServer.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using log4net;

namespace LibWhipLru.Server {
	/// <summary>
	/// A fairly generic WHIP server implementation. Actual message handling is done in callbacks.
	/// </summary>
	public class WHIPServer : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public const string DEFAULT_ADDRESS = "*";
		public const string DEFAULT_PASSWORD = null;
		public const uint DEFAULT_PORT = 32700;
		public const uint DEFAULT_BACKLOG_LENGTH = 100;

		// Thread signal.
		private readonly ManualResetEvent _allDone = new ManualResetEvent(false);

		public delegate void RequestReceivedDelegate(ClientRequestMsg request, RequestResponseDelegate responseHandler, object context);
		public delegate void RequestResponseDelegate(ServerResponseMsg response, object context);

		private bool _isRunning;
		private readonly IPEndPoint _localEndPoint;
		private readonly string _password;
		private readonly int _port;
		private readonly int _listenBacklogLength;

		private readonly ConcurrentDictionary<string, ClientInfo> _activeConnections = new ConcurrentDictionary<string, ClientInfo>();
		internal IEnumerable<ClientInfo> ActiveConnections => _activeConnections.Values; // Automatic lock and snapshot each access.

		private readonly RequestReceivedDelegate _requestHandler;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Server.WHIPServer"/> class.
		/// </summary>
		/// <param name="requestHandler">Request handler.</param>
		public WHIPServer(
			RequestReceivedDelegate requestHandler
		) : this (requestHandler, DEFAULT_ADDRESS, DEFAULT_PORT, DEFAULT_PASSWORD, DEFAULT_BACKLOG_LENGTH) {
			// All handled elsewhere.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Server.WHIPServer"/> class.
		/// </summary>
		/// <param name="requestHandler">Request handler.</param>
		/// <param name="listenBacklogLength">Listen backlog length.</param>
		public WHIPServer(
			RequestReceivedDelegate requestHandler,
			uint listenBacklogLength
		) : this(requestHandler, DEFAULT_ADDRESS, DEFAULT_PORT, DEFAULT_PASSWORD, listenBacklogLength) {
			// All handled elsewhere.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Server.WHIPServer"/> class.
		/// </summary>
		/// <param name="requestHandler">Request handler.</param>
		/// <param name="address">Address.</param>
		/// <param name="port">Port.</param>
		/// <param name="password">Password.</param>
		public WHIPServer(
			RequestReceivedDelegate requestHandler,
			string address,
			uint port,
			string password
		) : this(requestHandler, address, port, password, DEFAULT_BACKLOG_LENGTH) {
			// All handled elsewhere.
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.Server.WHIPServer"/> class.
		/// </summary>
		/// <param name="requestHandler">Request handler.</param>
		/// <param name="address">Address.</param>
		/// <param name="port">Port.</param>
		/// <param name="password">Password.</param>
		/// <param name="listenBacklogLength">Listen backlog length.</param>
		public WHIPServer(
			RequestReceivedDelegate requestHandler,
			string address,
			uint port,
			string password,
			uint listenBacklogLength
		) {
			LOG.Debug($"{address}:{port} - Initializing server.");

			IPAddress addr;
			if (string.IsNullOrWhiteSpace(address) || address == DEFAULT_ADDRESS) {
				addr = IPAddress.Any;
			}
			else {
				try {
					addr = IPAddress.Parse(address);
				}
				catch (ArgumentNullException e) {
					throw new ArgumentNullException("Address cannot be null.", e);
				}
				catch (FormatException e) {
					throw new FormatException("Address must be a valid IPv4 or IPv6 address.", e);
				}
			}

			_password = password;
			_port = (int)port;

			_requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));

			if (listenBacklogLength <= 0) {
				throw new ArgumentOutOfRangeException(nameof(listenBacklogLength), $"Value less than minimum of 1");
			}
			else if (listenBacklogLength > int.MaxValue) {
				throw new ArgumentOutOfRangeException(nameof(listenBacklogLength), $"Value exceeded maximum of {int.MaxValue}");
			}
			_listenBacklogLength = (int)listenBacklogLength;

			try {
				_localEndPoint = new IPEndPoint(addr, _port);
			}
			catch (ArgumentOutOfRangeException e) {
				throw new ArgumentOutOfRangeException($"Port number {_port} is invalid, should be between {IPEndPoint.MinPort} and {IPEndPoint.MaxPort}", e);
			}
		}

		/// <summary>
		/// Start the server listening.  Note that all kinds of exceptions can be thrown.
		/// </summary>
		public void Start() {
			LOG.Debug($"{_localEndPoint} - Starting server.");

			// Create a TCP/IP socket.
			using (var listener = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)) {
				// Bind the socket to the local endpoint and listen for incoming connections.
				listener.Bind(_localEndPoint);
				listener.Listen(_listenBacklogLength);

				var hadConnection = true; // Lies, damnable lies!
				_isRunning = true;
				while (_isRunning) {
					// Set the event to nonsignaled state.
					_allDone.Reset();

					if (hadConnection) {
						// Start an asynchronous socket to listen for connections.
						LOG.Debug($"{_localEndPoint} - Waiting for a connection...");
						listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
					}

					// Wait until a connection is made before continuing.
					hadConnection = _allDone.WaitOne(500); // Have to have a timeout or you can run across a situation where this jsut hangs arund instead of dying after stop is called.
				}
			}
		}

		/// <summary>
		/// Stop this the listening server, letting it finish out whatever it was working on in the background.
		/// </summary>
		public void Stop() {
			LOG.Debug($"{_localEndPoint} - Stopping server.");

			_isRunning = false;
			_allDone.Set();
		}

		#region Callbacks

		private void AcceptCallback(IAsyncResult ar) {
			if (!_isRunning) {
				return;
			}

			// Signal the main thread to continue.
			_allDone.Set();

			LOG.Debug($"{_localEndPoint}, unknown, Acceptance - Accepting connection.");

			// Get the socket that handles the client request.
			var listener = (Socket)ar.AsyncState;

			// Create the state object.
			var state = new StateObject {
				Buffer = new byte[StateObject.BUFFER_SIZE],
				Client = new ClientInfo {
					State = State.Acceptance,
					RequestInfo = "(connecting)",
					RemoteEndpoint = string.Empty,
					Started = DateTimeOffset.UtcNow,
				},
				WorkSocket = null,
			};

			try {
				state.WorkSocket = listener.EndAccept(ar);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, unknown, {state.Client.State} - Exception caught while making connection with client.", e);
				return;
			}

#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			try {
				state.Client.RemoteEndpoint = state.WorkSocket.RemoteEndPoint.ToString();
			}
			catch {
				// Ignore
			}
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body

			var response = new AuthChallengeMsg();
			var challenge = response.GetChallenge();
			state.CorrectChallengeHash = AuthResponseMsg.ComputeChallengeHash(challenge, _password);

			state.Client.State = State.Challenged;
			state.Client.RequestInfo = null;
			StartReceive(ref state, new AuthResponseMsg(), ReadCallback);

			LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sending challenge.");

			try {
				Send(ref state, response);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught responding to client connection request.", e);
				return;
			}

			// If all that worked, add the client to the bag, they are in progress.
			_activeConnections.AddOrUpdate(state.Client.RemoteEndpoint, state.Client, (key, client) => state.Client);
		}

		private void StartReceive(ref StateObject state, IByteArrayAppendable message, AsyncCallback callback) {
			Contract.Requires(callback != null);
			Contract.Requires(message != null);

			state.Client.RequestInfo = null;
			state.Message = message;
			try {
				state.WorkSocket.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), state);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught while attempting to start getting data.", e);
				Send(ref state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty));
				return;
			}
		}

		private void ContinueReceive(ref StateObject state, AsyncCallback callback) {
			Contract.Requires(callback != null);

			try {
				state.WorkSocket.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), state);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught while attempting to continue getting data.", e);
				Send(ref state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty));
				StartReceive(ref state, state.Client.State == State.Challenged ? (IByteArrayAppendable)new AuthResponseMsg() : new ClientRequestMsg(), ReadCallback);
				return;
			}
		}

		private void ReadCallback(IAsyncResult ar) {
			// Retrieve the state object and the handler socket
			// from the asynchronous state object.
			var state = (StateObject)ar.AsyncState;
			var handler = state.WorkSocket;

			// Read data from the client socket.
			int bytesRead = 0;
			try {
				bytesRead = handler.EndReceive(ar);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught reading data.", e);
				Send(ref state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty));
				StartReceive(ref state, state.Client.State == State.Challenged ? (IByteArrayAppendable)new AuthResponseMsg() : new ClientRequestMsg(), ReadCallback);
				return;
			}

			if (bytesRead > 0) {
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Reading {bytesRead} bytes.");

				var complete = false;
				try {
					complete = state.Message.AddRange(state.Buffer);
				}
				catch (Exception e) {
					LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught while extracting data from inbound message.", e);
					Send(ref state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty));
					StartReceive(ref state, state.Client.State == State.Challenged ? (IByteArrayAppendable)new AuthResponseMsg() : new ClientRequestMsg(), ReadCallback);
					return;
				}

				if (complete) {
					IByteArraySerializable response = null;

					if (state.Client.State == State.Ready) {
						// There might be more data, so store the data received so far.
						var message = state.Message as ClientRequestMsg;

						state.Client.RequestInfo = $"{message.Type.ToString().Substring(3)} {message.AssetId}"; // Substring removes the "RT_"
						LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Request message completed: {message?.GetHeaderSummary()}");

						try {
							_requestHandler(message, RequestResponseCallback, state);
						}
						catch (Exception e) {
							LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught from request handler while processing message.", e);
							Send(ref state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, message.AssetId));
							StartReceive(ref state, new ClientRequestMsg(), ReadCallback);
							return;
						}
					}
					else { // State.Challenged, or not recognized.
						// Wants to know status, reply accordingly.
						var message = state.Message as AuthResponseMsg;

						var hashCorrect = message?.ChallengeHash == state.CorrectChallengeHash;

						LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Auth response completed: " + (hashCorrect ? "Hash correct." : "Hash not correct, auth failed."));

						response = new AuthStatusMsg(hashCorrect ? AuthStatusMsg.StatusType.AS_SUCCESS : AuthStatusMsg.StatusType.AS_FAILURE);

						if (hashCorrect) {
							state.Message = new ClientRequestMsg();
							state.Client.State = State.Ready;
						}

						try {
							if (hashCorrect) {
								Send(ref state, response);
								StartReceive(ref state, new ClientRequestMsg(), ReadCallback);
							}
							else {
								SendAndClose(ref state, response);
							}
						}
						catch (Exception e) {
							LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught responding to client.", e);
							Send(ref state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty));
							StartReceive(ref state, new ClientRequestMsg(), ReadCallback);
							return;
						}
					}
				}
				else {
					// Not all data received. Get more.
					LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Message incomplete, getting next packet.");

					ContinueReceive(ref state, ReadCallback);
				}
			}
			else {
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Zero bytes received. Client must have closed the connection.");
				state.Client.State = State.Disconnected;
				_activeConnections.TryRemove(state.Client.RemoteEndpoint, out var junk);
			}
		}

		private void RequestResponseCallback(ServerResponseMsg response, object context) {
			var state = (StateObject)context;

			LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Replying to request message: {response.GetHeaderSummary()}.");

			try {
				Send(ref state, response);
				StartReceive(ref state, new ClientRequestMsg(), ReadCallback);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught responding to client.", e);
			}
		}

		private void Send(ref StateObject state, IByteArraySerializable response) {
			var handler = state.WorkSocket;

			if (response != null) {
				// Convert the string data to byte data using ASCII encoding.
				var byteData = response.ToByteArray();

				// Begin sending the data to the remote device.
				if (handler.Connected) {
					LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sending {byteData.Length} bytes.");
					handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(SendCallback), state);
				}
				else {
					LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Could not send {byteData.Length} bytes because no longer connected.");
				}
			}
			else if (handler.Connected) {
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sending nothing.");
				handler.BeginSend(new byte[] { }, 0, 0, SocketFlags.None, new AsyncCallback(SendCallback), state);
			}
			else {
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Client disconnected before response could be sent.");
				state.Client.State = State.Disconnected;
				_activeConnections.TryRemove(state.Client.RemoteEndpoint, out var junk);
				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}
		}

		private void SendCallback(IAsyncResult ar) {
			// Retrieve the socket from the state object.
			var state = (StateObject)ar.AsyncState;
			var handler = state.WorkSocket;

			try {
				// Complete sending the data to the remote device.
				var bytesSent = handler.EndSend(ar);
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sent {bytesSent} bytes.");
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught responding.", e);
			}
		}

		private void SendAndClose(ref StateObject state, IByteArraySerializable response) {
			var handler = state.WorkSocket;

			if (response != null) {
				// Convert the string data to byte data using ASCII encoding.
				var byteData = response.ToByteArray();

				// Begin sending the data to the remote device.
				if (handler.Connected) {
					LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sending {byteData.Length} bytes and then closing connection.");
					handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(SendAndCloseCallback), state);
				}
				else {
					LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Could not send {byteData.Length} bytes and then close connection because no longer connected.");
				}
			}
			else if (handler.Connected) {
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sending nothing and then closing connection.");
				handler.BeginSend(new byte[] { }, 0, 0, SocketFlags.None, new AsyncCallback(SendAndCloseCallback), state);
			}
			else {
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Client disconnected before response could be sent and the connection closed.");
				state.Client.State = State.Disconnected;
				_activeConnections.TryRemove(state.Client.RemoteEndpoint, out var junk);
				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}
		}

		private void SendAndCloseCallback(IAsyncResult ar) {
			// Retrieve the socket from the state object.
			var state = (StateObject)ar.AsyncState;
			var handler = state.WorkSocket;

			try {
				// Complete sending the data to the remote device.
				var bytesSent = handler.EndSend(ar);
				LOG.Debug($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Sent {bytesSent} bytes, and am closing the connection.");

				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.Client.RemoteEndpoint}, {state.Client.State} - Exception caught responding or closing the connection.", e);
			}

			state.Client.State = State.Disconnected;
			_activeConnections.TryRemove(state.Client.RemoteEndpoint, out var junk);
		}

		// State object for reading client data asynchronously
		private struct StateObject {
			// Size of receive buffer.
			public const int BUFFER_SIZE = 4098;

			// Client  socket.
			public Socket WorkSocket;

			// Receive buffer.
			public byte[] Buffer;

			// Received data.
			public IByteArrayAppendable Message;

			// Current client state. Shared with the ActiveConnections bag.
			public ClientInfo Client;

			// The expected response to the challenge
			public string CorrectChallengeHash;
		}

		#endregion

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		/// <summary>
		/// Part of the IDisposable pattern.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// Dispose managed state (managed objects).
					_isRunning = false;
					_allDone.Set();
					_allDone.Dispose();
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.
				// set large fields to null.

				disposedValue = true;
			}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="T:LibWhipLru.Server.WHIPServer"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="T:LibWhipLru.Server.WHIPServer"/>. The
		/// <see cref="Dispose()"/> method leaves the <see cref="T:LibWhipLru.Server.WHIPServer"/> in an unusable state. After
		/// calling <see cref="Dispose()"/>, you must release all references to the <see cref="T:LibWhipLru.Server.WHIPServer"/>
		/// so the garbage collector can reclaim the memory that the <see cref="T:LibWhipLru.Server.WHIPServer"/> was occupying.</remarks>
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion
	}
}
