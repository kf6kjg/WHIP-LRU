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

namespace WHIP_LRU.Server {
	public class WHIPServer : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public const string DEFAULT_ADDRESS = "*";
		public const string DEFAULT_PASSWORD = null;
		public const uint DEFAULT_PORT = 32700;

		// Thread signal.  
		public static ManualResetEvent AllDone = new ManualResetEvent(false);

		public delegate ServerResponseMsg RequestReceivedDelegate(ClientRequestMsg request);

		private bool _isRunning;
		private IPEndPoint _localEndPoint;
		private string _password;
		private int _port;

		private RequestReceivedDelegate _requestHandler;

		public WHIPServer(RequestReceivedDelegate requestHandler, string address = DEFAULT_ADDRESS, uint port = DEFAULT_PORT, string password = DEFAULT_PASSWORD) {
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
			// Create a TCP/IP socket.  
			var listener = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			// Bind the socket to the local endpoint and listen for incoming connections.  
			listener.Bind(_localEndPoint);
			listener.Listen(100);

			_isRunning = true;
			while (_isRunning) {
				// Set the event to nonsignaled state.  
				AllDone.Reset();

				// Start an asynchronous socket to listen for connections.  
				LOG.Debug("[WHIP_SERVER] Waiting for a connection...");
				listener.BeginAccept(AcceptCallback, listener);

				// Wait until a connection is made before continuing.  
				AllDone.WaitOne();
			}
		}

		public void Stop() {
			_isRunning = false;
		}

		#region Callbacks

		private void AcceptCallback(IAsyncResult ar) {
			// Signal the main thread to continue.  
			AllDone.Set();

			// Get the socket that handles the client request.  
			var listener = (Socket)ar.AsyncState;

			Socket handler = null;
			StateObject state = null;
			try {
				handler = listener.EndAccept(ar);

				LOG.Debug($"[WHIP_SERVER] Accepting connection from {handler.RemoteEndPoint} on {handler.LocalEndPoint}.");

				// Create the state object.  
				state = new StateObject();
				state.WorkSocket = handler;
				state.State = State.Challenged;
				state.Message = new AuthResponseMsg();
				handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
			}
			catch (Exception e) {
				LOG.Warn("[WHIP_SERVER] Exception caught while setting up to receive data from client.", e);
				return;
			}

			LOG.Debug($"[WHIP_SERVER] Sending challenge to {handler.RemoteEndPoint} on {handler.LocalEndPoint}.");

			var response = new AuthChallengeMsg();

			var challenge = response.GetChallenge();

			state.CorrectChallengeHash = AuthResponseMsg.ComputeChallengeHash(challenge, _password);

			try {
				Send(handler, response);
			}
			catch (Exception e) {
				LOG.Warn($"[WHIP_SERVER] Exception caught responding to client connection request from {handler.RemoteEndPoint} on {handler.LocalEndPoint}", e);
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
				LOG.Warn($"[WHIP_SERVER] Exception caught reading data.", e);
				return;
			}

			if (bytesRead > 0) {
				LOG.Debug($"[WHIP_SERVER] Reading {bytesRead} bytes from {handler.RemoteEndPoint} on {handler.LocalEndPoint}. Connection in state {state.State}.");

				bool complete = false;
				try {
					complete = state.Message.AddRange(state.Buffer.Take(bytesRead));
				}
				catch (Exception e) {
					LOG.Warn($"[WHIP_SERVER] Exception caught while extracting data from inbound message from {handler.RemoteEndPoint} on {handler.LocalEndPoint}.", e);
				}

				if (complete) {
					IByteArraySerializable response = null;

					switch (state.State) {
						case State.Ready: {
							// There might be more data, so store the data received so far.
							var message = state.Message as ClientRequestMsg;

							LOG.Debug($"[WHIP_SERVER] Request message from {handler.RemoteEndPoint} on {handler.LocalEndPoint} completed: {message?.GetHeaderSummary()}");

							try {
								response = _requestHandler(message);
							}
							catch (Exception e) {
								LOG.Warn($"[WHIP_SERVER] Exception caught from request handler while processing message from {handler.RemoteEndPoint} on {handler.LocalEndPoint}", e);
							}

							LOG.Debug($"[WHIP_SERVER] Replying to request message from {handler.RemoteEndPoint} on {handler.LocalEndPoint}: {((ClientRequestMsg)response).GetHeaderSummary()}");

							try {
								Send(handler, response);
								handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
							}
							catch (Exception e) {
								LOG.Warn($"[WHIP_SERVER] Exception caught responding to client from {handler.RemoteEndPoint} on {handler.LocalEndPoint}", e);
							}
						} break;
						case State.Challenged:
						default: {
							// Wants to know status, reply accordingly.
							var message = state.Message as AuthResponseMsg;

							var hashCorrect = message?.ChallengeHash == state.CorrectChallengeHash;

							LOG.Debug($"[WHIP_SERVER] Auth response from {handler.RemoteEndPoint} on {handler.LocalEndPoint} completed: " + (hashCorrect ? "Hash correct." : "Hash not correct, auth failed."));

							response = new AuthStatusMsg(hashCorrect ? AuthStatusMsg.StatusType.AS_SUCCESS : AuthStatusMsg.StatusType.AS_FAILURE);

							if (hashCorrect) {
								state.Message = new ClientRequestMsg();
								state.State = State.Ready;
							}

							try {
								if (hashCorrect) {
									Send(handler, response);
									handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
								}
								else {
									SendAndClose(handler, response);
								}
							}
							catch (Exception e) {
								LOG.Warn($"[WHIP_SERVER] Exception caught responding to client from {handler.RemoteEndPoint} on {handler.LocalEndPoint}", e);
							}
						} break;
					}
				}
				else {
					// Not all data received. Get more.  
					LOG.Debug($"[WHIP_SERVER] Message from {handler.RemoteEndPoint} on {handler.LocalEndPoint} incomplete, getting next packet.");

					handler.BeginReceive(state.Buffer, 0, StateObject.BUFFER_SIZE, 0, ReadCallback, state);
				}
			}
			else {
				LOG.Debug($"[WHIP_SERVER] Zero bytes received from {handler.RemoteEndPoint} on {handler.LocalEndPoint}. Client must have closed the connection.");
			}
		}

		private void Send(Socket handler, IByteArraySerializable response) {
			if (response != null) {
				// Convert the string data to byte data using ASCII encoding.  
				var byteData = response.ToByteArray();

				// Begin sending the data to the remote device.  
				handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, SendCallback, handler);
			}
			else {
				handler.BeginSend(new byte[] { }, 0, 0, SocketFlags.None, SendCallback, handler);
			}
		}

		private void SendCallback(IAsyncResult ar) {
			try {
				// Retrieve the socket from the state object.  
				var handler = (Socket)ar.AsyncState;

				// Complete sending the data to the remote device.  
				var bytesSent = handler.EndSend(ar);
				LOG.Debug($"[WHIP_SERVER] Sent {bytesSent} bytes to {handler.RemoteEndPoint} on {handler.LocalEndPoint}.");
			}
			catch (Exception e) {
				LOG.Warn($"[WHIP_SERVER] Problem responding to client.", e);
			}
		}

		private void SendAndClose(Socket handler, IByteArraySerializable response) {
			if (response != null) {
				// Convert the string data to byte data using ASCII encoding.  
				var byteData = response.ToByteArray();

				// Begin sending the data to the remote device.  
				handler.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, SendAndCloseCallback, handler);
			}
			else {
				handler.BeginSend(new byte[] { }, 0, 0, SocketFlags.None, SendAndCloseCallback, handler);
			}
		}

		private void SendAndCloseCallback(IAsyncResult ar) {
			try {
				// Retrieve the socket from the state object.  
				var handler = (Socket)ar.AsyncState;

				// Complete sending the data to the remote device.  
				var bytesSent = handler.EndSend(ar);
				LOG.Debug($"[WHIP_SERVER] Sent {bytesSent} bytes to {handler.RemoteEndPoint} on {handler.LocalEndPoint}, and closing the connection.");

				handler.Shutdown(SocketShutdown.Both);
				handler.Close();
			}
			catch (Exception e) {
				LOG.Warn($"[WHIP_SERVER] Problem responding to client.", e);
			}
		}

		// State object for reading client data asynchronously  
		private class StateObject {
			// Size of receive buffer.  
			public const int BUFFER_SIZE = 1024;

			// Client  socket.  
			public Socket WorkSocket;

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
