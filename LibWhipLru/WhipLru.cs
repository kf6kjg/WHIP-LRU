// WhipLru.cs
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
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Chattel;
using log4net;
using Nini.Config;
using WHIP_LRU.Server;
using WHIP_LRU.Util;

namespace LibWhipLru {
	public class WhipLru {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private ChattelReader _assetReader;
		private ChattelWriter _assetWriter;
		private PIDFileManager _pidFileManager;
		private WHIPServer _server;
		private Thread _serviceThread;

		private string _address;
		private uint _port;
		private string _password;

		public WhipLru(string address, uint port, string password, PIDFileManager pidFileManager, ChattelConfiguration chattelConfigRead = null, ChattelConfiguration chattelConfigWrite = null) {
			LOG.Debug($"{address}:{port} - Initializing service.");

			_address = address;
			_port = port;
			_password = password;

			_pidFileManager = pidFileManager;

			if (chattelConfigRead != null) {
				chattelConfigRead.DisableCache(); // Force caching off no matter how the INI is set. Doing caching differently here.
				_assetReader = new ChattelReader(chattelConfigRead);
			}
			if (chattelConfigWrite != null) {
				chattelConfigWrite.DisableCache(); // Force caching off no matter how the INI is set. Doing caching differently here.
				_assetWriter = new ChattelWriter(chattelConfigWrite);
			}

			_pidFileManager?.SetStatus(PIDFileManager.Status.Ready);
		}

		/// <summary>
		/// Starts up the service in a seperate thread.
		/// </summary>
		public void Start() {
			LOG.Debug($"{_address}:{_port} - Starting service");

			_server = new WHIPServer(RequestReceivedDelegate, _address, _port, _password);
			_serviceThread = new Thread(_server.Start);
			_serviceThread.IsBackground = true;

			try {
				_serviceThread.Start();
				_pidFileManager?.SetStatus(PIDFileManager.Status.Running);
			}
			catch (SocketException e) {
				LOG.Error("Unable to bind to address or port. Is something already listening on it, or have you granted permissions for WHIP_LRU to listen?", e);
			}
			catch (Exception e) {
				LOG.Warn("Exception during server execution, automatically restarting.", e);
			}
		}

		public void Stop() {
			LOG.Debug($"{_address}:{_port} - Stopping service.");

			try {
				_server?.Dispose();
				Thread.Sleep(100);
			}
			finally {
				_serviceThread?.Abort();
				_server = null;
				_serviceThread = null;
				_pidFileManager?.SetStatus(PIDFileManager.Status.Ready);
			}
		}

		private void RequestReceivedDelegate(ClientRequestMsg request, WHIPServer.RequestResponseDelegate responseHandler, object context) {
			// TODO: do this for real.  The response is done across a callback with the context pointer passed through so that I can queue these things and get back to them.

			responseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, OpenMetaverse.UUID.Zero), context);
		}
	}
}
