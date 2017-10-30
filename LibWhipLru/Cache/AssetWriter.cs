// AssetWriter.cs
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
using System.Text;
using System.Threading;
using InWorldz.Data.Assets.Stratus;

namespace LibWhipLru.Cache {
	public class AssetWriter {
		private static byte[] MAGIC_NUMBER = Encoding.ASCII.GetBytes("WHIPLRU1");

		private ConcurrentQueue<Tuple<StratusAsset, BackupNode>> _writeQueue;
		private Chattel.ChattelWriter _writer;

		private BackupNode[] _backupNodes;
		private BackupNode _nextAvailableNode;
		private object _nodeLock;

		public AssetWriter(uint recordCount, Chattel.ChattelWriter writer) {
			_writer = writer;

			// Goal here is to have a fixed-size disk-based storage for those rare times
			//  that storing assets in the local filesystem failed. One hopes it suceeded
			//  in the remote server, but if the asset fails there and it failed writing
			//  to primary disk storage, then this would come into the picture as it could
			//  even be on a different disk.

			// Might want to integrate the logic into the CacheManager so that the loading of asset data is easier.

			// Try initializing the file, but cancel if the file exists
				// Initialize file to all zeros. recordCount*BackupNode.BYTE_SIZE

			// Try loading the file.  Use MemoryMapped file. https://docs.microsoft.com/en-us/dotnet/standard/io/memory-mapped-files
				// Check magic number, error out if mismatch.
				// Seek through file in BackupNode.BYTE_SIZE chunks, loading each into memory.
				// Load (somehow) the asset from the cache and put into the _writeQueue.

			// Start up thread(s) that read from the writeQueue and send off to chattel's storage.
				// Upon sucess, or duplication error, call MarkNodeAvailable.
				// Upon other error or timeout, put back into writeQueue.
		}

		public void PutAsset(StratusAsset asset) {
			// This must NEVER THROW.

			BackupNode currentNode;

			lock (_nodeLock) {
				currentNode = _nextAvailableNode;
				currentNode.IsAvailable = false;
				_nextAvailableNode = null;

				while (_nextAvailableNode == null) {
					try {
						_nextAvailableNode = _backupNodes.First(node => node.IsAvailable);
					}
					catch (InvalidOperationException) {
						// No available nodes found, which means we are out of ability to safely continue until one becomes available...
						_nextAvailableNode = null;
						Thread.Sleep(100);
					}
				}
			}

			currentNode.AssetId = asset.Id;

			var nodeBytes = currentNode.ToByteArray();
			// TODO: Write nodeBytes to file at currentNode.FileOffset
		}

		private void MarkNodeAvailable(BackupNode node) {
			// TODO: Write a 0 at node.FileOffset

			node.IsAvailable = true;
		}

		private class BackupNode {
			public static uint BYTE_SIZE = 17;

			public ulong FileOffset { get; private set; }
			public bool IsAvailable { get; set; } // 1 byte
			public Guid AssetId { get; set; } // 16 bytes

			public BackupNode(byte[] bytes, ulong sourceOffset) {
				if (bytes == null) {
					throw new ArgumentNullException(nameof(bytes));
				}
				if (bytes.Length < BYTE_SIZE) {
					throw new ArgumentOutOfRangeException(nameof(bytes), $"Must have at least {BYTE_SIZE} bytes!");
				}

				FileOffset = sourceOffset;
				IsAvailable = bytes[0] == 0;

				var guidBytes = new byte[16];
				Buffer.BlockCopy(bytes, 0, guidBytes, 0, 16);
				AssetId = new Guid(guidBytes);
			}

			public byte[] ToByteArray() {
				var outBytes = new byte[17];

				outBytes[0] = IsAvailable ? (byte)0 : (byte)1;

				Buffer.BlockCopy(AssetId.ToByteArray(), 0, outBytes, 1, 16);

				return outBytes;
			}
		}
	}
}
