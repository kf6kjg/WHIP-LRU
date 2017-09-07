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
		public const uint DEFAULT_PORT = 32700;

		// Thread signal.  
		public static ManualResetEvent AllDone = new ManualResetEvent(false);

		public delegate ServerResponseMsg RequestReceivedDelegate(ClientRequestMsg request);

		private IPEndPoint _localEndPoint;
		private int _port;

		private bool _isRunning;

		private RequestReceivedDelegate _requestHandler;

		public WHIPServer(RequestReceivedDelegate requestHandler, string address = DEFAULT_ADDRESS, uint port = DEFAULT_PORT) {
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

			try {
				var handler = listener.EndAccept(ar);

				LOG.Debug($"[WHIP_SERVER] Accepting connection from {handler.RemoteEndPoint} on {handler.LocalEndPoint}.");

				// Create the state object.  
				var state = new StateObject();
				state.workSocket = handler;
				handler.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, ReadCallback, state);
			}
			catch (Exception e) {
				LOG.Warn("[WHIP_SERVER] Exception caught while setting up to receive data from client.", e);
			}
		}

		private void ReadCallback(IAsyncResult ar) {
			// Retrieve the state object and the handler socket  
			// from the asynchronous state object.  
			var state = (StateObject)ar.AsyncState;
			var handler = state.workSocket;

			// Read data from the client socket.   
			int bytesRead = 0;
			try {
				bytesRead = handler.EndReceive(ar);
			}
			catch (Exception e) {
				LOG.Warn($"[WHIP_SERVER] Exception caught reading data from {handler.RemoteEndPoint} on {handler.LocalEndPoint}.", e);
				return;
			}

			if (bytesRead > 0) {
				LOG.Debug($"[WHIP_SERVER] Reading {bytesRead} from {handler.RemoteEndPoint} on {handler.LocalEndPoint}.");

				// There might be more data, so store the data received so far.
				bool complete = false;
				try {
					complete = state.message.AddRange(state.buffer.Take(bytesRead));
				}
				catch (Exception e) {
					LOG.Warn($"[WHIP_SERVER] Exception caught while extracting data from inbound message from {handler.RemoteEndPoint} on {handler.LocalEndPoint}.", e);
				}

				if (complete) {
					ServerResponseMsg response = null;

					LOG.Debug($"[WHIP_SERVER] Message from {handler.RemoteEndPoint} on {handler.LocalEndPoint} completed: {state.message.GetHeaderSummary()}");

					try {
						response = _requestHandler(state.message);
					}
					catch (Exception e) {
						LOG.Warn($"[WHIP_SERVER] Exception caught from request handler while processing message from {handler.RemoteEndPoint} on {handler.LocalEndPoint}", e);
					}

					LOG.Debug($"[WHIP_SERVER] Replying to  {handler.RemoteEndPoint} on {handler.LocalEndPoint}: {response.GetHeaderSummary()}");

					try {
						Send(handler, response);
					}
					catch (Exception e) {
						LOG.Warn($"[WHIP_SERVER] Exception caught responding to client from {handler.RemoteEndPoint} on {handler.LocalEndPoint}", e);
					}
				}
				else {
					// Not all data received. Get more.  
					LOG.Debug($"[WHIP_SERVER] Message from {handler.RemoteEndPoint} on {handler.LocalEndPoint} incomplete, getting next packet.");

					handler.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0, ReadCallback, state);
				}
			}
			else {
				LOG.Debug($"[WHIP_SERVER] Zero bytes received from {handler.RemoteEndPoint} on {handler.LocalEndPoint}. Client must have closed the connection.");
			}
		}

		private void Send(Socket handler, ServerResponseMsg response) {
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
				LOG.Debug($"[WHIP_SERVER] Sent {bytesSent} bytes to {handler.RemoteEndPoint} on {handler.LocalEndPoint}");

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
			public Socket workSocket;

			// Receive buffer.  
			public byte[] buffer = new byte[BUFFER_SIZE];

			// Received data.  
			public ClientRequestMsg message = new ClientRequestMsg();
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
