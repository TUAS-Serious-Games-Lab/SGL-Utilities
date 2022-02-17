using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;

namespace SGL.Utilities.Crypto {
	public class PublicKey {
		internal AsymmetricKeyParameter wrapped;

		internal PublicKey(AsymmetricKeyParameter wrapped) {
			if (wrapped.IsPrivate) throw new ArgumentException("Expecting a public key but got a private key", nameof(wrapped));
			if (wrapped is not (RsaKeyParameters or ECPublicKeyParameters)) throw new KeyException("Unexpected key type");
			this.wrapped = wrapped;
		}

		public override bool Equals(object? obj) => obj is PublicKey publicKey && wrapped.Equals(publicKey.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();

		public KeyType Type => wrapped switch { RsaKeyParameters rsa => KeyType.RSA, ECPublicKeyParameters => KeyType.EllipticCurves, _ => throw new KeyException("Unexpected key type") };
	}
}
