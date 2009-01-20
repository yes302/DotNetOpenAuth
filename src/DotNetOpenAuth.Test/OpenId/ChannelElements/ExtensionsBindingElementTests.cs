﻿//-----------------------------------------------------------------------
// <copyright file="ExtensionsBindingElementTests.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.Test.OpenId.ChannelElements {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OpenId;
	using DotNetOpenAuth.OpenId.ChannelElements;
	using DotNetOpenAuth.OpenId.Extensions;
	using DotNetOpenAuth.OpenId.Messages;
	using DotNetOpenAuth.OpenId.RelyingParty;
	using DotNetOpenAuth.Test.Mocks;
	using DotNetOpenAuth.Test.OpenId.Extensions;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	[TestClass]
	public class ExtensionsBindingElementTests : OpenIdTestBase {
		private OpenIdExtensionFactory factory;
		private ExtensionsBindingElement element;
		private IProtocolMessageWithExtensions request;

		[TestInitialize]
		public override void SetUp() {
			base.SetUp();

			this.factory = new OpenIdExtensionFactory();
			this.factory.RegisterExtension(MockOpenIdExtension.Factory);
			this.element = new ExtensionsBindingElement(this.factory);
			this.request = new SignedResponseRequest(Protocol.Default.Version, OpenIdTestBase.ProviderUri, AuthenticationRequestMode.Immediate);
		}

		[TestMethod]
		public void RoundTripFullStackTest() {
			IOpenIdMessageExtension request = new MockOpenIdExtension("requestPart", "requestData");
			IOpenIdMessageExtension response = new MockOpenIdExtension("responsePart", "responseData");
			ExtensionTestUtilities.Roundtrip(
				Protocol.Default,
				new IOpenIdMessageExtension[] { request },
				new IOpenIdMessageExtension[] { response });
		}

		[TestMethod]
		public void ExtensionFactory() {
			Assert.AreSame(this.factory, this.element.ExtensionFactory);
		}

		[TestMethod, ExpectedException(typeof(ArgumentNullException))]
		public void PrepareMessageForSendingNull() {
			this.element.PrepareMessageForSending(null);
		}

		/// <summary>
		/// Verifies that false is returned when a non-extendable message is sent.
		/// </summary>
		[TestMethod]
		public void PrepareMessageForSendingNonExtendableMessage() {
			IProtocolMessage request = new AssociateDiffieHellmanRequest(Protocol.Default.Version, OpenIdTestBase.ProviderUri);
			Assert.IsFalse(this.element.PrepareMessageForSending(request));
		}

		[TestMethod]
		public void PrepareMessageForSending() {
			this.request.Extensions.Add(new MockOpenIdExtension("part", "extra"));
			Assert.IsTrue(this.element.PrepareMessageForSending(this.request));

			string alias = GetAliases(this.request.ExtraData).Single();
			Assert.AreEqual(MockOpenIdExtension.MockTypeUri, this.request.ExtraData["openid.ns." + alias]);
			Assert.AreEqual("part", this.request.ExtraData["openid." + alias + ".Part"]);
			Assert.AreEqual("extra", this.request.ExtraData["openid." + alias + ".data"]);
		}

		[TestMethod]
		public void PrepareMessageForReceiving() {
			this.request.ExtraData["openid.ns.mock"] = MockOpenIdExtension.MockTypeUri;
			this.request.ExtraData["openid.mock.Part"] = "part";
			this.request.ExtraData["openid.mock.data"] = "extra";
			Assert.IsTrue(this.element.PrepareMessageForReceiving(this.request));
			MockOpenIdExtension ext = this.request.Extensions.OfType<MockOpenIdExtension>().Single();
			Assert.AreEqual("part", ext.Part);
			Assert.AreEqual("extra", ext.Data);
		}

		/// <summary>
		/// Verifies that extension responses are included in the OP's signature.
		/// </summary>
		[TestMethod]
		public void ExtensionResponsesAreSigned() {
			Protocol protocol = Protocol.Default;
			var op = this.CreateProvider();
			IndirectSignedResponse response = new IndirectSignedResponse(protocol.Version, RPUri);
			response.ReturnTo = RPUri;
			response.ProviderEndpoint = ProviderUri;
			var ext = new MockOpenIdExtension("pv", "ev");
			response.Extensions.Add(ext);
			op.Channel.Send(response);
			ITamperResistantOpenIdMessage signedResponse = (ITamperResistantOpenIdMessage)response;
			string extensionAliasKey = signedResponse.ExtraData.Single(kv => kv.Value == MockOpenIdExtension.MockTypeUri).Key;
			Assert.IsTrue(extensionAliasKey.StartsWith("openid.ns."));
			string extensionAlias = extensionAliasKey.Substring("openid.ns.".Length);

			// Make sure that the extension members and the alias=namespace declaration are all signed.
			Assert.IsNotNull(signedResponse.SignedParameterOrder);
			string[] signedParameters = signedResponse.SignedParameterOrder.Split(',');
			Assert.IsTrue(signedParameters.Contains(extensionAlias + ".Part"));
			Assert.IsTrue(signedParameters.Contains(extensionAlias + ".data"));
			Assert.IsTrue(signedParameters.Contains("ns." + extensionAlias));
		}

		/// <summary>
		/// Verifies that unsigned extension responses (where any or all fields are unsigned) are ignored.
		/// </summary>
		[TestMethod, Ignore]
		public void UnsignedExtensionsAreIgnored() {
			Assert.Inconclusive("Not yet implemented.");
		}

		/// <summary>
		/// Verifies that two extensions with the same TypeURI cannot be applied to the same message.
		/// </summary>
		/// <remarks>
		/// OpenID Authentication 2.0 section 12 states that
		/// "A namespace MUST NOT be assigned more than one alias in the same message".
		/// </remarks>
		[TestMethod]
		public void TwoExtensionsSameTypeUri() {
			IOpenIdMessageExtension request1 = new MockOpenIdExtension("requestPart1", "requestData1");
			IOpenIdMessageExtension request2 = new MockOpenIdExtension("requestPart2", "requestData2");
			try {
				ExtensionTestUtilities.Roundtrip(
					Protocol.Default,
					new IOpenIdMessageExtension[] { request1, request2 },
					new IOpenIdMessageExtension[0]);
				Assert.Fail("Expected ProtocolException not thrown.");
			} catch (AssertFailedException ex) {
				Assert.IsInstanceOfType(ex.InnerException, typeof(ProtocolException));
			}
		}

		private static IEnumerable<string> GetAliases(IDictionary<string, string> extraData) {
			Regex regex = new Regex(@"^openid\.ns\.(\w+)");
			return from key in extraData.Keys
				   let m = regex.Match(key)
				   where m.Success
				   select m.Groups[1].Value;
		}
	}
}
