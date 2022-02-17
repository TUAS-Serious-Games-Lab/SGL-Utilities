using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using System;
using System.IO;

namespace SGL.Utilities.Crypto {
	internal static class PemHelper {
		public class FuncPasswordFinder : IPasswordFinder {
			private Func<char[]> passwordGetter;

			public FuncPasswordFinder(Func<char[]> passwordGetter) {
				this.passwordGetter = passwordGetter;
			}

			public char[] GetPassword() => passwordGetter();
		}
		public static KeyPair LoadKeyPair(TextReader reader, IPasswordFinder passwordFinder) {
			PemReader pemReader = new PemReader(reader, passwordFinder);
			var pemContent = pemReader.ReadObject();
			if (pemContent is AsymmetricCipherKeyPair kp) {
				return new KeyPair(kp);
			}
			else if (pemContent is AsymmetricKeyParameter key && key.IsPrivate && PrivateKey.IsValidWrappedType(key)) {
				// The PEM file contains a private key, derive the public key from the private key.
				var privKey = new PrivateKey(key);
				return new KeyPair(privKey.DerivePublicKey(), privKey);
			}
			else if (pemContent is AsymmetricKeyParameter pk && pk.IsPrivate) {
				throw new PemException("PEM file did contain an unsupported type private key instead of the expected key pair.");
			}
			else if (pemContent is AsymmetricKeyParameter) {
				throw new PemException("PEM file did contain a public key instead of the expected key pair.");
			}
			else {
				throw new PemException("PEM file did not contain a key pair or a supported private key.");
			}
		}
		public static PrivateKey LoadPrivateKey(TextReader reader, IPasswordFinder passwordFinder) {
			PemReader pemReader = new PemReader(reader, passwordFinder);
			var pemContent = pemReader.ReadObject();
			if (pemContent is AsymmetricKeyParameter key && key.IsPrivate && PrivateKey.IsValidWrappedType(key)) {
				return new PrivateKey(key);
			}
			else if (pemContent is AsymmetricKeyParameter pk && pk.IsPrivate) {
				throw new KeyException("Unsupported type of private key.");
			}
			else if (pemContent is AsymmetricKeyParameter) {
				throw new KeyException("Expecting a private key but PEM contained a public key");
			}
			else if (pemContent is AsymmetricCipherKeyPair kp && PrivateKey.IsValidWrappedType(kp.Private)) {
				return new PrivateKey(kp.Private);
			}
			else {
				throw new PemException("PEM file did not contain a supported private key or key pair.");
			}
		}

	}
}
