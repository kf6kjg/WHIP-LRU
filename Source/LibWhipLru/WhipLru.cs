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
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Chattel;
using log4net;
using LibWhipLru.Cache;
using LibWhipLru.Server;
using LibWhipLru.Util;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using InWorldz.Data.Assets.Stratus;
using System.Globalization;

namespace LibWhipLru {
	public class WhipLru {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		// Unix-epoch starts at January 1st 1970, 00:00:00 UTC. And all our times in the server are (or at least should be) in UTC.
		private static readonly DateTime UNIX_EPOCH = DateTime.ParseExact("1970-01-01 00:00:00 +0", "yyyy-MM-dd hh:mm:ss z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime();

		private readonly CacheManager _cacheManager;
		private readonly PIDFileManager _pidFileManager;
		private WHIPServer _server;
		private Thread _serviceThread;

		private string _address;
		private uint _port;
		private string _password;

		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private BlockingCollection<Request> _requests;

		public WhipLru(string address, uint port, string password, PIDFileManager pidFileManager, CacheManager cacheManager, ChattelConfiguration chattelConfigRead = null, ChattelConfiguration chattelConfigWrite = null) {
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

			_cacheManager = cacheManager;
			_pidFileManager = pidFileManager;

			if (chattelConfigRead != null) {
				chattelConfigRead.DisableCache(); // Force caching off no matter how the INI is set. Doing caching differently here.
				_cacheManager.SetChattelReader(new ChattelReader(chattelConfigRead));
			}
			if (chattelConfigWrite != null) {
				chattelConfigWrite.DisableCache(); // Force caching off no matter how the INI is set. Doing caching differently here.
				_cacheManager.SetChattelWriter(new ChattelWriter(chattelConfigWrite));
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

			_server = new WHIPServer(RequestReceivedDelegate, _address, _port, _password);
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

			var parallelOptions = new ParallelOptions {
				MaxDegreeOfParallelism = 4, // Keeps memory use from spiraling out of control, see https://canbilgin.wordpress.com/2017/02/05/curious-case-of-parallel-foreach-with-blockingcollection/
				CancellationToken = _cancellationTokenSource.Token,
			};

			Task.Factory.StartNew(() => Parallel.ForEach(_requests.GetConsumingEnumerable(), parallelOptions, ProcessRequest));
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
			_requests.Add(new Request() {
				context = context,
				request = request,
				responseHandler = responseHandler,
			});
		}

		private void ProcessRequest(Request req) {
			// WARNING: this method is being executed in its own thread, and may even be being executed in parallel in multiple threads.

			ServerResponseMsg response;

			switch (req.request.Type) {
				case ClientRequestMsg.RequestType.RT_GET:
				case ClientRequestMsg.RequestType.RT_GET_DONTCACHE:
				case ClientRequestMsg.RequestType.RT_MAINT_PURGELOCALS:
				case ClientRequestMsg.RequestType.RT_PURGE:
					response = new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, OpenMetaverse.UUID.Zero);
					break;
				case ClientRequestMsg.RequestType.RT_PUT:
					response = HandlePutAsset(req.request.AssetId.Guid, req.request.Data);
					break;
				case ClientRequestMsg.RequestType.RT_STATUS_GET:
					response = HandleGetStatus();
					break;
				case ClientRequestMsg.RequestType.RT_STORED_ASSET_IDS_GET:
					response = HandleGetStoredAssetIds(req.request.AssetId.ToString().Substring(0, 3));
					break;
				case ClientRequestMsg.RequestType.RT_TEST:
				default:
					response = new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, OpenMetaverse.UUID.Zero);
					break;
			}
			req.responseHandler(response, req.context);
		}

		#region Handlers

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
			return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, OpenMetaverse.UUID.Zero, output.ToString());
		}

		private ServerResponseMsg HandleGetStoredAssetIds(string prefix) {
			var ids = _cacheManager?.ActiveIds(prefix);

			return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, OpenMetaverse.UUID.Zero, string.Join(",", ids));
		}

		private ServerResponseMsg HandlePutAsset(Guid assetId, byte[] data) {
			if (assetId == Guid.Empty) {
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, new OpenMetaverse.UUID(assetId), "Zero UUID not allowed.");
			}

			InWorldz.Whip.Client.Asset whipAsset;

			try {
				var rawData = new InWorldz.Whip.Client.AppendableByteArray(data.Length);
				rawData.Append(data);
				whipAsset = new InWorldz.Whip.Client.Asset(rawData);
			}
			catch (Exception e) {
				LOG.Debug($"Exception reading data for asset {assetId}", e);
				return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, new OpenMetaverse.UUID(assetId), "Error processing request.");
			}

			var asset = new StratusAsset {
				CreateTime = UnixToUTCDateTime(whipAsset.CreateTime),
				Data = whipAsset.Data,
				Description = whipAsset.Description,
				Id = assetId,
				Local = whipAsset.Local,
				Name = whipAsset.Name,
				Temporary = whipAsset.Temporary,
				Type = (sbyte)whipAsset.Type,
			};

			switch (_cacheManager.PutAsset(asset)) {
				case CacheManager.PutResult.DONE:
				case CacheManager.PutResult.WIP:
					return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_OK, new OpenMetaverse.UUID(assetId));
				default:
					return new ServerResponseMsg(ServerResponseMsg.ResponseCode.RC_ERROR, new OpenMetaverse.UUID(assetId), "Duplicate assets are not allowed.");
			}
		}

		#endregion

		private static DateTime UnixToUTCDateTime(long seconds) {
			return UNIX_EPOCH.AddSeconds(seconds);
		}

		private class Request {
			public ClientRequestMsg request { get; set; }
			public WHIPServer.RequestResponseDelegate responseHandler { get; set; }
			public object context { get; set; }
		}
	}
}
