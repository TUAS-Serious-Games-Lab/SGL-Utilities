using Org.BouncyCastle.OpenSsl;
using SGL.Utilities.Crypto.Internals;
using System;
using System.IO;

namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Provides functionality to load a password-encrypted private key from a PEM file and provides access to the key pair of which the private key is a part through the <see cref="KeyPair"/> property.
	/// Currently, the following formats of keys are supported:
	/// <list type="bullet">
	/// <item><description>A PEM file that contains an (encrypted) full key pair of a type that the underlying cryptography implementation (Bouncy Castle's <c>Org.BouncyCastle.OpenSsl.PemReader</c>) can load</description></item>
	/// <item><description>A PEM file containing an (encrypted) RSA private key; the public key will be derived from the private key</description></item>
	/// <item><description>A PEM file containing an (encrypted) EC private key; the public key will be derived from the private key</description></item>
	/// </list>
	/// </summary>
	public class PrivateKeyStore {
		private KeyPair? keyPair = null;

		/// <summary>
		/// Provides access to the loaded key pair.
		/// </summary>
		/// <exception cref="InvalidOperationException">When this getter is called before a key pair has been sucessfully loaded.</exception>
		public KeyPair KeyPair {
			get {
				if (keyPair == null) throw new InvalidOperationException("Key pair has not been loaded yet.");
				return keyPair;
			}
		}

		/// <summary>
		/// Loads a key pair from the PEM file at the given path, using the given password for decryption if the private key is encrypted.
		/// </summary>
		/// <param name="path">The path to the PEM file containing the key (pair).</param>
		/// <param name="password">The password to use for encrypted key pairs.</param>
		/// <exception cref="PemException">If the file didn't contain a PEM with a key pair or a supported private key.</exception>
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
		/// <exception cref="PemException">If the reader didn't contain a PEM with a key pair or a supported private key.</exception>
		public void LoadKeyPair(TextReader reader, char[] password) => keyPair = KeyPair.LoadOneFromPem(reader, () => password);
	}
}
