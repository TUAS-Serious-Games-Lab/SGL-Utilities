using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using System;
using System.IO;

namespace SGL.Utilities.Crypto {
	public class PrivateKeyStore {
		private AsymmetricCipherKeyPair? keyPair = null;
		public AsymmetricCipherKeyPair KeyPair {
			get {
				if (keyPair == null) throw new InvalidOperationException("Key pair has not been loaded yet.");
				return keyPair;
			}
		}

		private class PasswordFinder : IPasswordFinder {
			private readonly char[] password;

			public PasswordFinder(char[] password) {
				this.password = password;
			}

			public char[] GetPassword() {
				return password;
			}
		}
		public void LoadKeyPair(string path, char[] password) {
			using var fileReader = File.OpenText(path);
			LoadKeyPair(fileReader, password);
		}
		public void LoadKeyPair(TextReader reader, char[] password) {
			PemReader pemReader = new PemReader(reader, new PasswordFinder(password));
			var pemContent = pemReader.ReadObject();
			if (pemContent is AsymmetricCipherKeyPair kp) {
				keyPair = kp;
			}
			else if (pemContent is RsaPrivateCrtKeyParameters rsa) {
				// The PEM file contains only the RSA private key, extract the public key from the private key.
				// The public key consists only of two components, that are also present in the private key: The modulus and the public exponent.
				keyPair = new AsymmetricCipherKeyPair(new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent), rsa);
			}
			else if (pemContent is ECPrivateKeyParameters ecPriv) {
				// The PEM file contains only the Elliptic Curves private key, derive the public key from the given private key.
				// The public key is a point on the curve, determined by multiplying the generator point (which is part of the curve domain definition)
				// by the integer that forms the private key.
				var q = ecPriv.Parameters.G.Multiply(ecPriv.D);
				keyPair = new AsymmetricCipherKeyPair(ecPriv.PublicKeyParamSet != null ? new ECPublicKeyParameters(q, ecPriv.PublicKeyParamSet) : new ECPublicKeyParameters(q, ecPriv.Parameters), ecPriv);
			}
			else {
				throw new ArgumentException("PEM file did not contain a key pair or a supported private key.", nameof(reader));
			}
		}
	}
}
