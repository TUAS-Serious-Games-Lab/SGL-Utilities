using Org.BouncyCastle.Crypto;
using System;
using System.IO;

namespace SGL.Utilities.Crypto {
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

		public override bool Equals(object? obj) => obj is KeyPair pair && Public.Equals(pair.Public) && Private.Equals(pair.Private);
		public override int GetHashCode() => HashCode.Combine(Public, Private);
		public override string? ToString() => "KeyPair: Public: " + Public.ToString() + " Private:" + Private.ToString();

		public static KeyPair LoadFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadKeyPair(reader, new PemHelper.FuncPasswordFinder(passwordGetter));
	}
}
