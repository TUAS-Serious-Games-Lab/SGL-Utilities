using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;

namespace SGL.Utilities.Crypto {
	public class PrivateKey {
		internal AsymmetricKeyParameter wrapped;

		internal PrivateKey(AsymmetricKeyParameter wrapped) {
			if (!wrapped.IsPrivate) throw new KeyException("Expecting a private key but got a public key");
			if (!IsValidWrappedType(wrapped)) throw new KeyException("Unexpected key type");
			this.wrapped = wrapped;
		}

		internal static bool IsValidWrappedType(AsymmetricKeyParameter wrapped) => TryGetKeyType(wrapped) != null;
		internal static KeyType? TryGetKeyType(AsymmetricKeyParameter wrapped) => wrapped switch {
			RsaPrivateCrtKeyParameters rsa => KeyType.RSA,
			ECPrivateKeyParameters => KeyType.EllipticCurves,
			_ => null
		};

		public override bool Equals(object? obj) => obj is PrivateKey privateKey && wrapped.Equals(privateKey.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();

		public KeyType Type => TryGetKeyType(wrapped) ?? throw new KeyException("Unexpected key type");
	}
}
