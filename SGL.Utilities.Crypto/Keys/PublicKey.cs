using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using SGL.Utilities.Crypto.Internals;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Keys {
	public class PublicKey {
		internal AsymmetricKeyParameter wrapped;

		internal PublicKey(AsymmetricKeyParameter wrapped) {
			if (wrapped.IsPrivate) throw new KeyException("Expecting a public key but got a private key");
			if (!IsValidWrappedType(wrapped)) throw new KeyException("Unexpected key type");
			this.wrapped = wrapped;
		}

		internal static bool IsValidWrappedType(AsymmetricKeyParameter wrapped) => TryGetKeyType(wrapped) != null;
		internal static KeyType? TryGetKeyType(AsymmetricKeyParameter wrapped) => wrapped switch {
			RsaKeyParameters rsa => KeyType.RSA,
			ECPublicKeyParameters => KeyType.EllipticCurves,
			_ => null
		};

		public override bool Equals(object? obj) => obj is PublicKey publicKey && wrapped.Equals(publicKey.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();

		public KeyType Type => TryGetKeyType(wrapped) ?? throw new KeyException("Unexpected key type");
		public static PublicKey LoadOneFromPem(TextReader reader) => PemHelper.LoadPublicKey(reader);
		public static IEnumerable<PublicKey> LoadAllFromPem(TextReader reader) => PemHelper.LoadPublicKeys(reader);
		public void StoreToPem(TextWriter writer) => PemHelper.Write(writer, this);
	}
}
