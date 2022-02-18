using Org.BouncyCastle.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Internals;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Keys {
	public class KeyPair {
		public PublicKey Public { get; }
		public PrivateKey Private { get; }

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

		public override bool Equals(object? obj) => obj is KeyPair pair && Public.Equals(pair.Public) && Private.Equals(pair.Private);
		public override int GetHashCode() => HashCode.Combine(Public, Private);
		public override string? ToString() => "KeyPair: Public: " + Public.ToString() + " Private:" + Private.ToString();

		public KeyType Type => Private.Type;

		public static KeyPair LoadOneFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadKeyPair(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		public static IEnumerable<KeyPair> LoadAllFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadKeyPairs(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
		public void StoreToPem(TextWriter writer, PemEncryptionMode encMode, char[] password, RandomGenerator random) => PemHelper.Write(writer, this, encMode, password, random);

		public static KeyPair GenerateRSA(RandomGenerator random, int keyLength) => GeneratorHelper.GenerateRsaKeyPair(random, keyLength);
		public static KeyPair GenerateEllipticCurves(RandomGenerator random, int keyLength, string? curveName = null) => GeneratorHelper.GenerateEcKeyPair(random, keyLength, curveName);
		public static KeyPair Generate(RandomGenerator random, KeyType type, int keyLength, string? curveName = null) => GeneratorHelper.GenerateKeyPair(random, type, keyLength, curveName);

	}
}
