using Org.BouncyCastle.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Internals;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents a key pair, used for asymmetric encryption, consisting of a private and a public key that are mathematically linked such that only the private key can decrypt information that has been encrypted using the public key.
	/// Such key pairs are also used for cryptographic signatures, where only the private key can sign data in a way that the signature is successfully verifyable using the public key.
	/// </summary>
	public class KeyPair {
		/// <summary>
		/// Returns the public key of the pair.
		/// </summary>
		public PublicKey Public { get; }
		/// <summary>
		/// Returns the private key of the pair.
		/// </summary>
		public PrivateKey Private { get; }

		/// <summary>
		/// Constructs a key pair object from the given public and private key.
		/// It is the callers obligation to ensure that they actually belong together.
		/// </summary>
		/// <param name="public">The public key of the pair.</param>
		/// <param name="private">The private key of the pair.</param>
		/// <exception cref="KeyException">If the key objects don't even match in their type.</exception>
		public KeyPair(PublicKey @public, PrivateKey @private) {
			if (@public.Type != @private.Type) throw new KeyException("Given public and private keys don't match in type.");
			Public = @public;
			Private = @private;
		}

		internal KeyPair(AsymmetricCipherKeyPair keyPair) {
			if (!PublicKey.IsValidWrappedType(keyPair.Public)) throw new KeyException("Unsupported public key type.");
			if (!PrivateKey.IsValidWrappedType(keyPair.Private)) throw new KeyException("Unsupported private key type.");
			if (PublicKey.TryGetKeyType(keyPair.Public) != PrivateKey.TryGetKeyType(keyPair.Private)) throw new KeyException("Public and private keys in given pair don't match in type.");
			Public = new PublicKey(keyPair.Public);
			Private = new PrivateKey(keyPair.Private);
		}
		internal AsymmetricCipherKeyPair ToWrappedPair() => new AsymmetricCipherKeyPair(Public.wrapped, Private.wrapped);

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is KeyPair pair && Public.Equals(pair.Public) && Private.Equals(pair.Private);
		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(Public, Private);
		/// <inheritdoc/>
		public override string? ToString() => "KeyPair: Public: " + Public.ToString() + " Private:" + Private.ToString();

		/// <summary>
		/// Returns the type of the key pair (e.g. RSA or Elliptic Curves).
		/// </summary>
		public KeyType Type => Private.Type;

		/// <summary>
		/// Loads one key pair from the PEM-encoded data in <paramref name="reader"/>.
		/// As key pairs are usually stored as just their private key, the public key will be derived from the private key when only a private key is loaded.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> containing at least one PEM-encoded key pair or private key.</param>
		/// <param name="passwordGetter">A function object that is called to obtain the password used for decrypting the private key.</param>
		/// <returns>The loaded key pair.</returns>
		public static KeyPair LoadOneFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadKeyPair(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		/// <summary>
		/// Loads all key pairs from the PEM-encoded data in <paramref name="reader"/>.
		/// As key pairs are usually stored as just their private key, the public key will be derived from the private key when only a private key is loaded.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> containing PEM-encoded key pairs.</param>
		/// <param name="passwordGetter">A function object that is called to obtain the password used for decrypting the private keys.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> over all key pairs loaded from <paramref name="reader"/>.</returns>
		public static IEnumerable<KeyPair> LoadAllFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadKeyPairs(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		/// <summary>
		/// Writes the key pair to <paramref name="writer"/> in PEM-encoded form.
		/// </summary>
		/// <param name="writer">The writer to which the key pair should be written.</param>
		/// <param name="encMode">The encryption mode to use for the private key.</param>
		/// <param name="password">The password to use for encryption of the private key.</param>
		/// <param name="random">A random generator to be used for generating an initialization vector and password salt.</param>
		public void StoreToPem(TextWriter writer, PemEncryptionMode encMode, char[] password, RandomGenerator random) => PemHelper.Write(writer, this, encMode, password, random);

		/// <summary>
		/// Generates a new RSA key pair of the given key length.
		/// </summary>
		/// <param name="random">The random generator to use for generating the key pair.</param>
		/// <param name="keyLength">The length of the keys in bits.</param>
		/// <returns>The generated key pair.</returns>
		public static KeyPair GenerateRSA(RandomGenerator random, int keyLength) => GeneratorHelper.GenerateRsaKeyPair(random, keyLength);
		/// <summary>
		/// Generates a new Elliptic Curves key pair of the given key length.
		/// The curve used can either be specified explicitly in <paramref name="curveName"/>, or a suitable default curve for the given key length is used otherwise.
		/// </summary>
		/// <param name="random">The random generator to use for generating the key pair.</param>
		/// <param name="keyLength">The length of the keys in bits.</param>
		/// <param name="curveName">The name of the named curve parameter set to use.</param>
		/// <returns>The generated key pair.</returns>
		public static KeyPair GenerateEllipticCurves(RandomGenerator random, int keyLength, string? curveName = null) => GeneratorHelper.GenerateEcKeyPair(random, keyLength, curveName);
		/// <summary>
		/// Generates a new key pair of the given key length and of the type specified by <paramref name="type"/>.
		/// If <paramref name="type"/> is <see cref="KeyType.EllipticCurves"/>, the curve used can either be specified explicitly in <paramref name="curveName"/>, or a suitable default curve for the given key length is used otherwise.
		/// </summary>
		/// <param name="random">The random generator to use for generating the key pair.</param>
		/// <param name="type">The type of key pair to generate.</param>
		/// <param name="keyLength">The length of the keys in bits.</param>
		/// <param name="curveName">The name of the named curve parameter set to use if <paramref name="type"/> is <see cref="KeyType.EllipticCurves"/>.</param>
		/// <returns>The generated key pair.</returns>
		public static KeyPair Generate(RandomGenerator random, KeyType type, int keyLength, string? curveName = null) => GeneratorHelper.GenerateKeyPair(random, type, keyLength, curveName);

	}
}
