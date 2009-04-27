﻿//-----------------------------------------------------------------------
// <copyright file="AnonymousIdentifierProviderBase.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.ApplicationBlock.Provider {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OpenId;

	public abstract class AnonymousIdentifierProviderBase {
		private int newSaltLength = 20;

		/// <summary>
		/// Initializes a new instance of the <see cref="AnonymousIdentifierProviderBase"/> class.
		/// </summary>
		/// <param name="baseIdentifier">The base URI on which to append the anonymous part.</param>
		public AnonymousIdentifierProviderBase(Uri baseIdentifier) {
			if (baseIdentifier == null) {
				throw new ArgumentNullException("baseIdentifier");
			}

			this.Hasher = HashAlgorithm.Create("SHA256");
			this.Encoder = Encoding.UTF8;
			this.BaseIdentifier = baseIdentifier;
		}

		public Uri BaseIdentifier { get; private set; }

		protected HashAlgorithm Hasher { get; private set; }

		protected Encoding Encoder { get; private set; }

		protected int NewSaltLength {
			get {
				return this.newSaltLength;
			}

			set {
				if (value <= 0) {
					throw new ArgumentOutOfRangeException("value");
				}

				this.newSaltLength = value;
			}
		}

		#region IAnonymousIdentifierProvider Members

		public Uri GetAnonymousIdentifier(Identifier localIdentifier, Realm relyingPartyRealm) {
			byte[] salt = this.GetHashSaltForLocalIdentifier(localIdentifier);
			string valueToHash = localIdentifier + "#" + (relyingPartyRealm ?? string.Empty);
			byte[] valueAsBytes = this.Encoder.GetBytes(valueToHash);
			byte[] bytesToHash = new byte[valueAsBytes.Length + salt.Length];
			valueAsBytes.CopyTo(bytesToHash, 0);
			salt.CopyTo(bytesToHash, valueAsBytes.Length);
			byte[] hash = this.Hasher.ComputeHash(bytesToHash);
			string base64Hash = Convert.ToBase64String(hash);
			Uri anonymousIdentifier = this.AppendIdentifiers(this.BaseIdentifier, base64Hash);
			return anonymousIdentifier;
		}

		#endregion

		protected virtual byte[] GetNewSalt() {
			// We COULD use a crypto random function, but for a salt it seems overkill.
			return Util.GetNonCryptoRandomData(this.NewSaltLength);
		}

		protected Uri AppendIdentifiers(Uri baseIdentifier, string uriHash) {
			if (baseIdentifier == null) {
				throw new ArgumentNullException("baseIdentifier");
			}
			if (String.IsNullOrEmpty(uriHash)) {
				throw new ArgumentNullException("uriHash");
			}

			if (string.IsNullOrEmpty(baseIdentifier.Query)) {
				// The uriHash will appear on the path itself.
				string pathEncoded = Uri.EscapeUriString(uriHash.Replace('/', '_'));
				return new Uri(baseIdentifier, pathEncoded);
			} else {
				// The uriHash will appear on the query string.
				string dataEncoded = Uri.EscapeDataString(uriHash);
				return new Uri(baseIdentifier + dataEncoded);
			}
		}

		/// <summary>
		/// Gets the salt to use for generating an anonymous identifier for a given OP local identifier.
		/// </summary>
		/// <param name="localIdentifier">The OP local identifier.</param>
		/// <returns>The salt to use in the hash.</returns>
		/// <remarks>
		/// It is important that this method always return the same value for a given 
		/// <paramref name="localIdentifier"/>.  
		/// New salts can be generated for local identifiers without previously assigned salt
		/// values by calling <see cref="GetNewSalt"/> or by a custom method.
		/// </remarks>
		protected abstract byte[] GetHashSaltForLocalIdentifier(Identifier localIdentifier);

#if CONTRACTS_FULL
		/// <summary>
		/// Verifies conditions that should be true for any valid state of this object.
		/// </summary>
		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Called by code contracts.")]
		[ContractInvariantMethod]
		protected void ObjectInvariant() {
			Contract.Invariant(this.Hasher != null);
			Contract.Invariant(this.Encoder != null);
			Contract.Invariant(this.BaseIdentifier != null);
			Contract.Invariant(this.NewHashLength > 0);
		}
#endif
	}
}
