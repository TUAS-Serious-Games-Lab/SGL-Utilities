using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;

namespace SGL.Utilities.Crypto {
	public class KeyPair {
		public PublicKey Public { get; }
		public PrivateKey Private { get; }

		public KeyPair(PublicKey @public, PrivateKey @private) {
			Public = @public;
			Private = @private;
		}

		internal KeyPair(AsymmetricCipherKeyPair keyPair) {
			Public = new PublicKey(keyPair.Public);
			Private = new PrivateKey(keyPair.Private);
		}

		public override bool Equals(object? obj) {
			return obj is KeyPair pair &&
				   EqualityComparer<PublicKey>.Default.Equals(Public, pair.Public) &&
				   EqualityComparer<PrivateKey>.Default.Equals(Private, pair.Private);
		}

		public override int GetHashCode() {
			return HashCode.Combine(Public, Private);
		}

		public override string? ToString() {
			return "KeyPair: Public: " + Public.ToString() + " " + Private.ToString();
		}
	}
}
