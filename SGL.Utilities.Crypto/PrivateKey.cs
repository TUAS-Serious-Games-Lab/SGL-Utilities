using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;

namespace SGL.Utilities.Crypto {
	public class PrivateKey {
		internal AsymmetricKeyParameter wrapped;

		internal PrivateKey(AsymmetricKeyParameter wrapped) {
			if (!wrapped.IsPrivate) throw new ArgumentException("Expecting a private key but got a public key", nameof(wrapped));
			if (wrapped is not (RsaPrivateCrtKeyParameters or ECPrivateKeyParameters)) throw new KeyException("Unexpected key type");
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

		public KeyType Type => wrapped switch { RsaPrivateCrtKeyParameters rsa => KeyType.RSA, ECPrivateKeyParameters => KeyType.EllipticCurves, _ => throw new KeyException("Unexpected key type") };
	}
}
