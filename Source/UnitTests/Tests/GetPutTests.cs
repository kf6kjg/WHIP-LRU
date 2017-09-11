// ProtocolTests.cs
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
using InWorldz.Whip.Client;
using NUnit.Framework;

namespace UnitTests.Tests {
	[TestFixture]
	public class GetPutTests {
		private RemoteServer _provider;

		private Asset _asset;

		[OneTimeSetUp]
		public void Setup() {
			_provider = new RemoteServer(Constants.SERVICE_ADDRESS, Constants.SERVICE_PORT, Constants.PASSWORD);
			_provider.Start();
		}

		[SetUp]
		public void PretestPrep() {
			_asset = new Asset(
				Guid.NewGuid().ToString(),
				7, // Notecard
				false,
				false,
				(int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds,
				"Just junk",
				"Just junk.",
				new byte[] { 0x31, 0x33, 0x33, 0x37 }
			);
		}

		[OneTimeTearDown]
		public void Teardown() {
			_provider.Stop();
		}

		[Test]
		[Timeout(60000)]
		public void TestCheckServerStatus() {
			string result = null;

			Assert.DoesNotThrow(() => {
				result = _provider.GetServerStatus();
			});

			Assert.That(result?.Contains("ACTIVE") ?? false, $"Result does not contain the string 'ACTIVE':\n{result}");
		}

		[Test]
		[Timeout(60000)]
		public void TestOperationGETDoesNotFail() {
			_provider.PutAsset(_asset);

			Assert.DoesNotThrow(() => {
				_provider.GetAsset(_asset.Uuid);
			}, "Failed to retreive the asset just stored.");
		}

		[Test]
		[Timeout(60000)]
		public void TestOperationGETReturnsMatch() {
			_provider.PutAsset(_asset);

			var asset = _provider.GetAsset(_asset.Uuid);

			Assert.AreEqual(_asset.Uuid, asset.Uuid, "Asset ID fails to match.");
			Assert.AreEqual(_asset.Type, asset.Type, "Asset Type fails to match.");
			Assert.AreEqual(_asset.Local, asset.Local, "Asset Local flag fails to match.");
			Assert.AreEqual(_asset.Temporary, asset.Temporary, "Asset Temporary flag fails to match.");
			Assert.AreEqual(_asset.CreateTime, asset.CreateTime, "Asset CreateTime fails to match.");
			Assert.AreEqual(_asset.Name, asset.Name, "Asset Name fails to match.");
			Assert.AreEqual(_asset.Description, asset.Description, "Asset Description fails to match.");
			Assert.AreEqual(_asset.Data, asset.Data, "Asset Data fails to match.");
		}

		[Test]
		[Timeout(60000)]
		public void TestOperationPUTDoesNotFail() {
			Assert.DoesNotThrow(() => {
				_provider.PutAsset(_asset);
			}, "Failed to put the asset.");
		}

	}
}
