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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;

namespace WHIP_LRU.Server {
	public class WHIPServer : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public const string DEFAULT_ADDRESS = "*";
		public const string DEFAULT_PASSWORD = null;
		public const uint DEFAULT_PORT = 32700;

		// Thread signal.  
		private ManualResetEvent _allDone = new ManualResetEvent(false);

		public delegate void RequestReceivedDelegate(ClientRequestMsg request, RequestResponseDelegate responseHandler, object context);
		public delegate void RequestResponseDelegate(ServerResponseMsg response, object context);

		private bool _isRunning;
		private IPEndPoint _localEndPoint;
		private string _password;
		private int _port;

		private RequestReceivedDelegate _requestHandler;

		public WHIPServer(RequestReceivedDelegate requestHandler, string address = DEFAULT_ADDRESS, uint port = DEFAULT_PORT, string password = DEFAULT_PASSWORD) {
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

			_requestHandler = requestHandler;

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
			var listener = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			// Bind the socket to the local endpoint and listen for incoming connections.  
			listener.Bind(_localEndPoint);
			listener.Listen(100);

			_isRunning = true;
			while (_isRunning) {
				// Set the event to nonsignaled state.  
				_allDone.Reset();

				// Start an asynchronous socket to listen for connections.  
				LOG.Debug($"{_localEndPoint} - Waiting for a connection...");
				listener.BeginAccept(AcceptCallback, listener);

				// Wait until a connection is made before continuing.  
				_allDone.WaitOne();
			}
		}

		public void Stop() {
			LOG.Debug($"{_localEndPoint} - Stopping server.");

			_isRunning = false;
			_allDone.Set();
		}

		#region Callbacks

		private void AcceptCallback(IAsyncResult ar) {
			// Signal the main thread to continue.  
			_allDone.Set();

			// Get the socket that handles the client request.  
			var listener = (Socket)ar.AsyncState;

			Socket handler = null;
			StateObject state = null;
			string client = "unknown";
			try {
				handler = listener.EndAccept(ar);

				try {
					client = handler.RemoteEndPoint.ToString();
				}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
				catch (Exception) {
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				}

				LOG.Debug($"{_localEndPoint}, {client}, Acceptance - Accepting connection.");

				// Create the state object.  
				state = new StateObject();
				state.WorkSocket = handler;
				state.ClientData = client;
				state.State = State.Challenged;
				state.Message = new AuthResponseMsg();
				handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {client}, {state.State} - Exception caught while setting up to receive data from client.", e);
				return;
			}

			LOG.Debug($"{_localEndPoint}, {client}, {state.State} - Sending challenge.");

			var response = new AuthChallengeMsg();

			var challenge = response.GetChallenge();

			state.CorrectChallengeHash = AuthResponseMsg.ComputeChallengeHash(challenge, _password);

			try {
				Send(state, response);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {client}, {state.State} - Exception caught responding to client connection request.", e);
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
				LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught reading data.", e);
				Send(state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, UUID.Zero));
				handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
				return;
			}

			if (bytesRead > 0) {
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Reading {bytesRead} bytes.");

				var complete = false;
				try {
					complete = state.Message.AddRange(state.Buffer.Take(bytesRead));
				}
				catch (Exception e) {
					LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught while extracting data from inbound message.", e);
					Send(state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, UUID.Zero));
					handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
					return;
				}

				if (complete) {
					IByteArraySerializable response = null;

					switch (state.State) {
						case State.Ready: {
							// There might be more data, so store the data received so far.
							var message = state.Message as ClientRequestMsg;

							LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Request message completed: {message?.GetHeaderSummary()}");

							try {
								_requestHandler(message, RequestResponseCallback, state);
							}
							catch (Exception e) {
								LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught from request handler while processing message.", e);
								Send(state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, message.AssetId));
								handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
								return;
							}
						} break;
						case State.Challenged:
						default: {
							// Wants to know status, reply accordingly.
							var message = state.Message as AuthResponseMsg;

							var hashCorrect = message?.ChallengeHash == state.CorrectChallengeHash;

							LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Auth response completed: " + (hashCorrect ? "Hash correct." : "Hash not correct, auth failed."));

							response = new AuthStatusMsg(hashCorrect ? AuthStatusMsg.StatusType.AS_SUCCESS : AuthStatusMsg.StatusType.AS_FAILURE);

							if (hashCorrect) {
								state.Message = new ClientRequestMsg();
								state.State = State.Ready;
							}

							try {
								if (hashCorrect) {
									Send(state, response);
									handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
								}
								else {
									SendAndClose(state, response);
								}
							}
							catch (Exception e) {
								LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught responding to client.", e);
								Send(state, new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, UUID.Zero));
								handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
								return;
							}
						} break;
					}
				}
				else {
					// Not all data received. Get more.  
					LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Message incomplete, getting next packet.");

					handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, 0, ReadCallback, state);
				}
			}
			else {
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Zero bytes received. Client must have closed the connection.");
			}
		}

		private void RequestResponseCallback(ServerResponseMsg response, object context) {
			var state = (StateObject)context;
			var handler = state.WorkSocket;

			LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Replying to request message: {response.GetHeaderSummary()}.");

			try {
				Send(state, response);
				handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught responding to client.", e);
			}
		}

		private void Send(StateObject state, IByteArraySerializable response) {
			var handler = state.WorkSocket;

			if (response != null) {
				// Convert the string data to byte data using ASCII encoding.  
				var byteData = response.ToByteArray();

				// Begin sending the data to the remote device.  
				if (handler.Connected) {
					LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Sending {byteData.Length} bytes.");
					handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, SendCallback, state);
				}
				else {
					LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Could not send {byteData.Length} bytes because no longer connected.");
				}
			}
			else if (handler.Connected) {
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Sending nothing.");
				handler.BeginSend(new byte[] { }, 0, 0, SocketFlags.None, SendCallback, state);
			}
			else {
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Client disconnected before response could be sent.");
			}
		}

		private void SendCallback(IAsyncResult ar) {
			// Retrieve the socket from the state object.  
			var state = (StateObject)ar.AsyncState;
			var handler = state.WorkSocket;

			try {
				// Complete sending the data to the remote device.  
				var bytesSent = handler.EndSend(ar);
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Sent {bytesSent} bytes.");
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught responding.", e);
			}
		}

		private void SendAndClose(StateObject state, IByteArraySerializable response) {
			var handler = state.WorkSocket;

			if (response != null) {
				// Convert the string data to byte data using ASCII encoding.  
				var byteData = response.ToByteArray();

				// Begin sending the data to the remote device.  
				if (handler.Connected) {
					LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Sending {byteData.Length} bytes and then closing connection.");
					handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, SendAndCloseCallback, state);
				}
				else {
					LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Could not send {byteData.Length} bytes and then close connection because no longer connected.");
				}
			}
			else if (handler.Connected) {
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Sending nothing and then closing connection.");
				handler.BeginSend(new byte[] { }, 0, 0, SocketFlags.None, SendAndCloseCallback, state);
			}
			else {
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Client disconnected before response could be sent and the connection closed.");
			}
		}

		private void SendAndCloseCallback(IAsyncResult ar) {
			// Retrieve the socket from the state object.  
			var state = (StateObject)ar.AsyncState;
			var handler = state.WorkSocket;

			try {
				// Complete sending the data to the remote device.  
				var bytesSent = handler.EndSend(ar);
				LOG.Debug($"{_localEndPoint}, {state.ClientData}, {state.State} - Sent {bytesSent} bytes, and am closing the connection.");

				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}
			catch (Exception e) {
				LOG.Warn($"{_localEndPoint}, {state.ClientData}, {state.State} - Exception caught responding or closing the connection.", e);
			}
		}

		// State object for reading client data asynchronously  
		private class StateObject {
			// Size of receive buffer.  
			public const int BUFFER_SIZE = 1024;

			// Client  socket.  
			public Socket WorkSocket;

			// Client info
			public string ClientData;

			// Receive buffer.  
			public byte[] Buffer = new byte[BUFFER_SIZE];

			// Received data.  
			public IByteArrayAppendable Message;

			// Current communications state
			public State State;

			// The expected response to the challenge
			public string CorrectChallengeHash;
		}

		private enum State {
			Challenged,
			Ready,
		}

		#endregion

		#region IDisposable Support

		private bool disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// Dispose managed state (managed objects).
					Stop();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~WHIPServer() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
