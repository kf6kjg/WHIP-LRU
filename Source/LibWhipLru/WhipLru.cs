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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using LibWhipLru.Server;
using LibWhipLru.Util;
using log4net;
using static InWorldz.Whip.Client.ClientRequestMsg;

namespace LibWhipLru {
	/// <summary>
	/// Main class that controls the whole WHIP-LRU process.
	/// </summary>
	public class WhipLru {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly StorageManager _storageManager;
		private readonly PIDFileManager _pidFileManager;
		private WHIPServer _server;
		private Task _serviceTask;

		private string _address;
		private uint _port;
		private string _password;
		private uint _listenBacklogLength;

		private BlockingCollection<Request> _requests;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:LibWhipLru.WhipLru"/> class.
		/// </summary>
		/// <param name="address">Address to listen on.</param>
		/// <param name="port">Port to listen on.</param>
		/// <param name="password">Password to filter conenctions by.</param>
		/// <param name="pidFileManager">Pidfile manager.</param>
		/// <param name="storageManager">Storage manager.</param>
		/// <param name="listenBacklogLength">Listen backlog length.</param>
		public WhipLru(
			string address,
			uint port,
			string password,
			PIDFileManager pidFileManager,
			StorageManager storageManager,
			uint listenBacklogLength = WHIPServer.DEFAULT_BACKLOG_LENGTH
		) {
			LOG.Debug($"{address}:{port} - Initializing service.");

			_address = address ?? throw new ArgumentNullException(nameof(address));
			_port = port;
			_password = password;
			_listenBacklogLength = listenBacklogLength;

			_storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
			_pidFileManager = pidFileManager ?? throw new ArgumentNullException(nameof(pidFileManager));

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
			_serviceTask = new Task(_server.Start, TaskCreationOptions.LongRunning);
			_serviceTask.ContinueWith(ServerTaskExceptionHandler, TaskContinuationOptions.OnlyOnFaulted);

			_serviceTask.Start();
			_pidFileManager?.SetStatus(PIDFileManager.Status.Running);

			_requests = new BlockingCollection<Request>();

			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
			Task.Run(() => { foreach (var request in _requests.GetConsumingEnumerable()) { ProcessRequest(request); } });
		}

		/// <summary>
		/// Stop the service and tells existing connections to finish off.
		/// </summary>
		public void Stop() {
			LOG.Debug($"{_address}:{_port} - Stopping service.");

			try {
				_server?.Dispose();
				Thread.Sleep(100);
			}
			finally {
				_server?.Stop();
				_server = null;
				_serviceTask = null;
				_pidFileManager?.SetStatus(PIDFileManager.Status.Ready);
			}

			_requests?.CompleteAdding();
		}

		private void ServerTaskExceptionHandler(Task serviceTask) {
			LOG.Error("Server process error(s).", serviceTask.Exception);
			Stop();
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

			switch (req.RequestMessage.Type) {
				case RequestType.GET:
					HandleGetAsset(req.RequestMessage.AssetId, req);
					break;
				case RequestType.GET_DONTCACHE:
					HandleGetAsset(req.RequestMessage.AssetId, req, false);
					break;
				case RequestType.MAINT_PURGELOCALS:
					HandlePurgeAssetsMarkedLocal(req);
					break;
				case RequestType.PURGE:
					HandlePurgeAsset(req.RequestMessage.AssetId, req);
					break;
				case RequestType.PUT:
					HandlePutAsset(req.RequestMessage.AssetId, req.RequestMessage.Data, req);
					break;
				case RequestType.STATUS_GET:
					HandleGetStatus(req);
					break;
				case RequestType.STORED_ASSET_IDS_GET:
					HandleGetStoredAssetIds(req.RequestMessage.AssetId.ToString("N").Substring(0, 3), req);
					break;
				case RequestType.TEST:
					HandleTest(req.RequestMessage.AssetId, req);
					break;
				default:
					req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty), req.Context);
					break;
			}
		}

		#region Handlers

		private void HandleGetAsset(Guid assetId, Request req, bool storeResultLocally = true) {
			if (assetId == Guid.Empty) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed."), req.Context);
				return;
			}

			StratusAsset asset = null;
			Exception exception = null;

			try {
				_storageManager.GetAsset(assetId, resultAsset => asset = resultAsset, () => {}, storeResultLocally);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				exception = e;
			}

			if (exception != null) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request."), req.Context);
				return;
			}

			if (asset == null) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_NOTFOUND, assetId), req.Context);
				return;
			}

			req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_FOUND, assetId, StratusAsset.ToWHIPSerialized(asset)), req.Context);
		}

		private void HandleGetStatus(Request req) {
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
			req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, output.ToString()), req.Context);
		}

		private void HandleGetStoredAssetIds(string prefix, Request req) {
			var ids = _storageManager.GetLocallyKnownAssetIds(prefix);

			req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty, string.Join(",", ids.Select(id => id.ToString("N")))), req.Context);
		}

		private void HandlePurgeAsset(Guid assetId, Request req) {
			if (assetId == Guid.Empty) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed."), req.Context);
			}

			StorageManager.PurgeResult result = StorageManager.PurgeResult.NOT_FOUND_LOCALLY;
			var error = false;

			try {
				_storageManager.PurgeAsset(assetId, purgeResult => result = purgeResult);
			}
			catch (Exception e) {
				LOG.Debug($"Exception purging asset {assetId}", e);
				error = true;
			}

			if (error) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request."), req.Context);
			}
			else {
				req.ResponseHandler(new ServerResponseMsg(result == StorageManager.PurgeResult.DONE ? ServerResponseMsg.ResponseCode.RC_OK : ServerResponseMsg.ResponseCode.RC_NOTFOUND, assetId), req.Context);
			}
		}

		private void HandlePurgeAssetsMarkedLocal(Request req) {
			var error = false;

			try {
				_storageManager.PurgeAllLocalAssets();
			}
			catch (Exception e) {
				LOG.Debug($"Exception purging assets marked local.", e);
				error = true;
			}

			if (error) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, Guid.Empty, "Error processing request."), req.Context);
			}
			else {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, Guid.Empty), req.Context);
			}
		}

		private void HandlePutAsset(Guid assetId, byte[] data, Request req) {
			if (assetId == Guid.Empty) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed."), req.Context);
				return;
			}

			StratusAsset asset;

			try {
				asset = StratusAsset.FromWHIPSerialized(data);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request."), req.Context);
				return;
			}

			_storageManager.StoreAsset(asset, result => {
				switch (result) {
					case StorageManager.PutResult.DONE:
						req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, assetId), req.Context);
						break;
					case StorageManager.PutResult.DUPLICATE:
						req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Duplicate assets are not allowed."), req.Context);
						break;
					default:
						req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId), req.Context);
						break;
				}
			});
		}

		private void HandleTest(Guid assetId, Request req) {
			if (assetId == Guid.Empty) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Zero UUID not allowed."), req.Context);
			}

			var result = false;
			var error = false;

			try {
				_storageManager.CheckAsset(assetId, found => result = found);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				error = true;
			}

			if (error) {
				req.ResponseHandler(new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, assetId, "Error processing request."), req.Context);
			}
			else {
				req.ResponseHandler(new ServerResponseMsg(result ? ServerResponseMsg.ResponseCode.RC_FOUND : ServerResponseMsg.ResponseCode.RC_NOTFOUND, assetId), req.Context);
			}
		}

		#endregion

		private class Request {
			public ClientRequestMsg RequestMessage { get; set; }
			public WHIPServer.RequestResponseDelegate ResponseHandler { get; set; }
			public object Context { get; set; }
		}
	}
}
