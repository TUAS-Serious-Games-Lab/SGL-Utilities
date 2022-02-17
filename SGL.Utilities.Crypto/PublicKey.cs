using Org.BouncyCastle.Crypto;
using System;

namespace SGL.Utilities.Crypto {
	public class PublicKey {
		internal AsymmetricKeyParameter wrapped;

		internal PublicKey(AsymmetricKeyParameter wrapped) {
			if (wrapped.IsPrivate) throw new ArgumentException("Expecting a public key but got a private key", nameof(wrapped));
			this.wrapped = wrapped;
		}

		public override bool Equals(object? obj) {
			return obj is PublicKey publicKey && wrapped.Equals(publicKey.wrapped);
		}

		public override int GetHashCode() {
			return wrapped.GetHashCode();
		}

		public override string? ToString() {
			return wrapped.ToString();
		}
	}
}
