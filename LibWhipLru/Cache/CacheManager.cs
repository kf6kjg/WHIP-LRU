// CacheManager.cs
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Caching;
using Chattel;
using InWorldz.Data.Assets.Stratus;
using LightningDB;
using log4net;

namespace LibWhipLru.Cache {
	public class CacheManager : IDisposable {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private LightningEnvironment _dbenv;

		private readonly OrderedGuidCache _activeIds;
		private readonly ChattelReader _assetReader;
		private readonly ChattelWriter _assetWriter;

		public IEnumerable<Guid> ActiveIds(string prefix) => _activeIds?.ItemsWithPrefix(prefix);

		// TODO: write a negative cache to store IDs that are failures.  Remember to remove any ODs that wind up being Put.  Don't need to disk-backup this.

		// TODO: restore _activeIds from the LMDB on startup.

		// TODO: keep and restore on restart a list of assets that haven't yet been uploaded.

		// TODO: Have a way to detect a maximum (disk size, record count, something) and when that's reached remove enough items to gain more space than is needed to insert the new item.

		public CacheManager(string pathToDatabaseFolder, float freeDiskSpacePercentage, ChattelReader assetReader, ChattelWriter assetWriter) {
			if (string.IsNullOrWhiteSpace(pathToDatabaseFolder)) {
				throw new ArgumentNullException(nameof(pathToDatabaseFolder), "No database path means no go.");
			}
			if (freeDiskSpacePercentage < 0f || freeDiskSpacePercentage > 1f) {
				throw new ArgumentOutOfRangeException(nameof(freeDiskSpacePercentage), "Must be in range 0.0 to 1.0 inclusive.");
			}
			try {
				_dbenv = new LightningEnvironment(pathToDatabaseFolder);

				_dbenv.Open(EnvironmentOpenFlags.None, UnixAccessMode.OwnerRead | UnixAccessMode.OwnerWrite);
			}
			catch (LightningException e) {
				throw new ArgumentException($"Given path invalid: '{pathToDatabaseFolder}'", nameof(pathToDatabaseFolder), e);
			}

			_activeIds = new OrderedGuidCache();

			_assetReader = assetReader;
			_assetWriter = assetWriter;
		}

		public void PutAsset(StratusAsset asset) {
			var assetId = asset.Id.ToString();

			if (!_activeIds.Contains(asset.Id)) {
				// The asset ID didn't exist in the cache, so let's add it to the local and remote storage.

				using (var memStream = new MemoryStream()) {
					ProtoBuf.Serializer.Serialize(memStream, asset);
					memStream.Position = 0;

					try {
						using (var tx = _dbenv.BeginTransaction())
						using (var db = tx.OpenDatabase($"assetstore-{assetId.Substring(0, 3)}", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create })) {
							tx.Put(db, System.Text.Encoding.UTF8.GetBytes(assetId), memStream.GetBuffer());
							tx.Commit();
						}
					}
					catch (LightningException e) {
						throw new CacheException("Problem opening database to put asset.", e);
					}
				}

				// TODO: put the asset ID into an output queue.

				// TODO: detect if some form of asset storage limit has been hit, call _activeIds.Remove(int, out int) to gain some space.

			}
		}

		/* TODO
		public StratusAsset GetAsset(UUID assetId) {

		}
		*/

		#region IDisposable Support

		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects).
					_dbenv.Dispose();
					_dbenv = null;
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~CacheManager() {
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
