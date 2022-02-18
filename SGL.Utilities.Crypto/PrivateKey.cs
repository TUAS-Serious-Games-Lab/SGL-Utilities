using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;

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

		public static PrivateKey LoadOneFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadPrivateKey(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		public static IEnumerable<PrivateKey> LoadAllFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadPrivateKeys(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		public void StoreToPem(TextWriter writer, PemEncryptionMode encMode, char[] password, RandomGenerator random) => PemHelper.Write(writer, this, encMode, password, random);
		public PublicKey DerivePublicKey() {
			if (wrapped is RsaPrivateCrtKeyParameters rsa) {
				// We have a RSA private key, extract the public key from the private key.
				// The public key consists only of two components, that are also present in the private key: The modulus and the public exponent.
				return new PublicKey(new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent));
			}
			else if (wrapped is ECPrivateKeyParameters ecPriv) {
				// We have an Elliptic Curves private key, derive the public key from the private key.
				// The public key is a point on the curve, determined by multiplying the generator point (which is part of the curve domain definition)
				// by the integer that forms the private key.
				var q = ecPriv.Parameters.G.Multiply(ecPriv.D);
				return new PublicKey(ecPriv.PublicKeyParamSet != null ? new ECPublicKeyParameters(ecPriv.AlgorithmName, q, ecPriv.PublicKeyParamSet) : new ECPublicKeyParameters(q, ecPriv.Parameters));
			}
			else {
				throw new KeyException("This private key does not support deriving the public key (yet).");
			}
		}
		public KeyPair DeriveKeyPair() => new KeyPair(DerivePublicKey(), this);
	}
}
