using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using System;
using System.IO;

namespace SGL.Utilities.Crypto {
	/// <summary>
	/// Provides functionality to load a password-encrypted private key from a PEM file and provides access to the key pair of which the private key is a part through the <see cref="KeyPair"/> property.
	/// Currently, the following formats of keys are supported:
	/// <list type="bullet">
	/// <item><description>A PEM file that contains an (encrypted) full key pair of a type that Bouncy Castle's <c>Org.BouncyCastle.OpenSsl.PemReader</c> can load</description></item>
	/// <item><description>A PEM file containing an (encrypted) RSA private key; the public key will be derived from the private key</description></item>
	/// <item><description>A PEM file containing an (encrypted) EC private key; the public key will be derived from the private key</description></item>
	/// </list>
	/// </summary>
	public class PrivateKeyStore {
		private AsymmetricCipherKeyPair? keyPair = null;

		/// <summary>
		/// Provides access to the loaded key pair.
		/// </summary>
		/// <exception cref="InvalidOperationException">When this getter is called before a key pair has been sucessfully loaded.</exception>
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

		/// <summary>
		/// Loads a key pair from the PEM file at the given path, using the given password for decryption if the private key is encrypted.
		/// </summary>
		/// <param name="path">The path to the PEM file containing the key (pair).</param>
		/// <param name="password">The password to use for encrypted key pairs.</param>
		/// <exception cref="ArgumentException">If the file didn't contain a PEM with a key pair or a supported private key.</exception>
		/// <exception cref="Exception">If the file access itself fails, the corresponding exceptions from <see cref="File.OpenText(string)"/> are passed through.</exception>
		public void LoadKeyPair(string path, char[] password) {
			using var fileReader = File.OpenText(path);
			LoadKeyPair(fileReader, password);
		}

		/// <summary>
		/// Loads a keypair from the given reader, using the given password for decryption if the private key is encrypted.
		/// </summary>
		/// <param name="reader">A text reader that contains the key pair in the PEM format.</param>
		/// <param name="password">The password to use for encrypted key pairs.</param>
		/// <exception cref="ArgumentException">If the reader didn't contain a PEM with a key pair or a supported private key.</exception>
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
				keyPair = new AsymmetricCipherKeyPair(ecPriv.PublicKeyParamSet != null ? new ECPublicKeyParameters(ecPriv.AlgorithmName, q, ecPriv.PublicKeyParamSet) : new ECPublicKeyParameters(q, ecPriv.Parameters), ecPriv);
			}
			else {
				throw new ArgumentException("PEM file did not contain a key pair or a supported private key.", nameof(reader));
			}
		}
	}
}
