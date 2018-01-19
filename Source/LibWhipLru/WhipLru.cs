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
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using LibWhipLru.Server;
using LibWhipLru.Util;
using log4net;
using static InWorldz.Whip.Client.ClientRequestMsg;

namespace LibWhipLru {
	public class WhipLru {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly StorageManager _cacheManager;
		private readonly PIDFileManager _pidFileManager;
		private WHIPServer _server;
		private Thread _serviceThread;

		private string _address;
		private uint _port;
		private string _password;
		private uint _listenBacklogLength;

		private BlockingCollection<Request> _requests;

		public WhipLru(string address, uint port, string password, PIDFileManager pidFileManager, StorageManager cacheManager, ChattelConfiguration chattelConfigRead = null, ChattelConfiguration chattelConfigWrite = null, uint listenBacklogLength = WHIPServer.DEFAULT_BACKLOG_LENGTH) {
			if (address == null) {
				throw new ArgumentNullException(nameof(address));
			}
			if (pidFileManager == null) {
				throw new ArgumentNullException(nameof(pidFileManager));
			}
			if (cacheManager == null) {
				throw new ArgumentNullException(nameof(cacheManager));
			}
			LOG.Debug($"{address}:{port} - Initializing service.");

			_address = address;
			_port = port;
			_password = password;
			_listenBacklogLength = listenBacklogLength;

			_cacheManager = cacheManager;
			_pidFileManager = pidFileManager;

			if (chattelConfigRead != null) {
				chattelConfigRead.DisableCache(); // Force caching off no matter how the INI is set. Doing caching differently here.
				_cacheManager.SetChattelReader(new ChattelReader(chattelConfigRead, cacheManager));
			}
			if (chattelConfigWrite != null) {
				chattelConfigWrite.DisableCache(); // Force caching off no matter how the INI is set. Doing caching differently here.
				_cacheManager.SetChattelWriter(new ChattelWriter(chattelConfigWrite, cacheManager));
			}

			_pidFileManager?.SetStatus(PIDFileManager.Status.Ready);
		}

		/// <summary>
		/// Starts up the service in a seperate thread.
		/// </summary>
		public void Start() {
			if (_server != null) {
				throw new InvalidOperationException("Cannot start a running service without stopping it first!");
			}
			LOG.Debug($"{_address}:{_port} - Starting service");

			_server = new WHIPServer(RequestReceivedDelegate, _address, _port, _password, _listenBacklogLength);
			_serviceThread = new Thread(_server.Start) {
				IsBackground = true
			};
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

			_requests = new BlockingCollection<Request>();

			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
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

			_requests.CompleteAdding();
		}

		private void RequestReceivedDelegate(ClientRequestMsg request, WHIPServer.RequestResponseDelegate responseHandler, object context) {
			// Queue up for processing.
			_requests.Add(new Request {
				Context = context,
				RequestMessage = request,
				ResponseHandler = responseHandler,
			});
		}

		private void ProcessRequest(Request req) {
			// WARNING: this method is being executed in its own thread, and may even be being executed in parallel in multiple threads.

			ServerResponseMsg response;

			switch (req.RequestMessage.Type) {
				case RequestType.GET:
					response = HandleGetAsset(req.RequestMessage.AssetId);
					break;
				case RequestType.GET_DONTCACHE:
					response = HandleGetAsset(req.RequestMessage.AssetId, false);
					break;
				case RequestType.MAINT_PURGELOCALS:
				case RequestType.PURGE:
					response = new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty);
					break;
				case RequestType.PUT:
					response = HandlePutAsset(req.RequestMessage.AssetId, req.RequestMessage.Data);
					break;
				case RequestType.STATUS_GET:
					response = HandleGetStatus();
					break;
				case RequestType.STORED_ASSET_IDS_GET:
					response = HandleGetStoredAssetIds(req.RequestMessage.AssetId.ToString("N").Substring(0, 3));
					break;
				case RequestType.TEST:
					response = HandleTest(req.RequestMessage.AssetId);
					break;
				default:
					response = new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty);
					break;
			}
			req.ResponseHandler(response, req.Context);
		}

		#region Handlers

		private ServerResponseMsg HandleGetAsset(Guid assetId, bool cacheResult = true) {
			if (assetId == Guid.Empty) {
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed.");
			}

			StratusAsset asset;

			try {
				asset = _cacheManager.GetAsset(assetId, cacheResult);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request.");
			}

			if (asset == null) {
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_NOTFOUND, assetId);
			}

			return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_FOUND, assetId, StratusAsset.ToWHIPSerialized(asset));
		}

		private ServerResponseMsg HandleGetStatus() {
			var output = new StringBuilder();

			var connections = _server?.ActiveConnections;

			output.Append($@"WHIP Server Status

-General
  Clients Connected: {connections.Count()}
-Client Status
");
			foreach (var clientInfo in connections) {
				output.Append($"  {clientInfo.RemoteEndpoint}: ");

				var connectionSeconds = (DateTimeOffset.UtcNow - clientInfo.Started).TotalSeconds;

				if (clientInfo.State == State.Acceptance) {
					output.Append("Unauthenticated");
				}
				if (clientInfo.State == State.Disconnected) {
					output.Append("DISCONNECTED");
				}
				else if (clientInfo.RequestInfo != null) {
					output.Append($"ACTIVE {connectionSeconds} [{clientInfo.RequestInfo}]");
				}
				else {
					output.Append($"IDLE {connectionSeconds}");
				}

				output.Append("\n");
			}

			output.Append($@"-VFS Backend
  Disk queue size: 
  Avg Disk Queue Wait: ms
  Avg Disk Op Latency: ms
-VFS Queue Items
");
			//for each assetrequest in queue
			//	output.Append($"  {assetrequest.description}\n") // description is based on the type of request, could be "GET {uuid}", "PURGE", etc...

			LOG.Debug($"Sending:\n{output}");
			return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, output.ToString());
		}

		private ServerResponseMsg HandleGetStoredAssetIds(string prefix) {
			var ids = _cacheManager?.ActiveIds(prefix);

			return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, string.Join(",", ids.Select(id => id.ToString("N"))));
		}

		private ServerResponseMsg HandlePutAsset(Guid assetId, byte[] data) {
			if (assetId == Guid.Empty) {
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed.");
			}

			StratusAsset asset;

			try {
				asset = StratusAsset.FromWHIPSerialized(data);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request.");
			}

			switch (_cacheManager.PutAsset(asset)) {
				case StorageManager.PutResult.DONE:
				case StorageManager.PutResult.WIP:
					return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, assetId);
				default:
					return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Duplicate assets are not allowed.");
			}
		}

		private ServerResponseMsg HandleTest(Guid assetId) {
			if (assetId == Guid.Empty) {
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed.");
			}

			bool result;

			try {
				result = _cacheManager.CheckAsset(assetId);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request.");
			}

			return new ServerResponseMsg(result ? ServerResponseMsg.ResponseCode.RC_FOUND : ServerResponseMsg.ResponseCode.RC_NOTFOUND, assetId);
		}

		#endregion

		private class Request {
			public ClientRequestMsg RequestMessage { get; set; }
			public WHIPServer.RequestResponseDelegate ResponseHandler { get; set; }
			public object Context { get; set; }
		}
	}
}
