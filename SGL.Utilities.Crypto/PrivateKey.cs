using Org.BouncyCastle.Crypto;
using System;

namespace SGL.Utilities.Crypto {
	public class PrivateKey {
		internal AsymmetricKeyParameter wrapped;

		internal PrivateKey(AsymmetricKeyParameter wrapped) {
			if (!wrapped.IsPrivate) throw new ArgumentException("Expecting a private key but got a public key", nameof(wrapped));
			this.wrapped = wrapped;
		}

		public override bool Equals(object? obj) {
			return obj is PrivateKey privateKey && wrapped.Equals(privateKey.wrapped);
		}

		public override int GetHashCode() {
			return wrapped.GetHashCode();
		}

		public override string? ToString() {
			return wrapped.ToString();
		}

	}
}
