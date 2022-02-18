using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
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
			return ReadKeyPair(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<KeyPair> LoadKeyPairs(TextReader reader, IPasswordFinder passwordFinder) {
			PemReader pemReader = new PemReader(reader, passwordFinder);
			KeyPair? kp = null;
			while ((kp = ReadKeyPair(pemReader)) != null) {
				yield return kp;
			}
		}

		public static KeyPair? ReadKeyPair(PemReader pemReader) {
			var pemContent = pemReader.ReadObject();
			if (pemContent == null) return null;
			if (pemContent is AsymmetricCipherKeyPair kp) {
				return new KeyPair(kp);
			}
			else if (pemContent is AsymmetricKeyParameter key && key.IsPrivate && PrivateKey.IsValidWrappedType(key)) {
				// The PEM file contains a private key, derive the public key from the private key.
				var privKey = new PrivateKey(key);
				return new KeyPair(privKey.DerivePublicKey(), privKey);
			}
			else if (pemContent is AsymmetricKeyParameter pk && pk.IsPrivate) {
				throw new PemException("PEM did contain an unsupported type private key instead of the expected key pair.");
			}
			else if (pemContent is AsymmetricKeyParameter) {
				throw new PemException("PEM did contain a public key instead of the expected key pair.");
			}
			else {
				throw new PemException("PEM did contain an object that is neither a key pair nor a supported private key.", pemContentType: pemContent.GetType());
			}
		}

		public static PublicKey LoadPublicKey(TextReader reader) {
			PemReader pemReader = new PemReader(reader);
			return ReadPublicKey(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<PublicKey> LoadPublicKeys(TextReader reader) {
			PemReader pemReader = new PemReader(reader);
			PublicKey? pk = null;
			while ((pk = ReadPublicKey(pemReader)) != null) {
				yield return pk;
			}
		}

		public static PublicKey? ReadPublicKey(PemReader pemReader) {
			var pemContent = pemReader.ReadObject();
			if (pemContent == null) return null;
			if (pemContent is AsymmetricKeyParameter key && !key.IsPrivate && PublicKey.IsValidWrappedType(key)) {
				return new PublicKey(key);
			}
			else if (pemContent is AsymmetricKeyParameter pk && !pk.IsPrivate) {
				throw new KeyException("Unsupported type of public key.");
			}
			else if (pemContent is AsymmetricKeyParameter) {
				throw new KeyException("Expecting a public key but PEM contained a private key");
			}
			else if (pemContent is AsymmetricCipherKeyPair kp && PublicKey.IsValidWrappedType(kp.Public)) {
				// The PEM contains a full key pair and we want just a private key, simply return the private key from the pair.
				return new PublicKey(kp.Public);
			}
			else {
				throw new PemException("PEM did contain an object that is not a supported public key or key pair.", pemContentType: pemContent.GetType());
			}
		}

		public static PrivateKey LoadPrivateKey(TextReader reader, IPasswordFinder passwordFinder) {
			PemReader pemReader = new PemReader(reader, passwordFinder);
			return ReadPrivateKey(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<PrivateKey> LoadPrivateKeys(TextReader reader, IPasswordFinder passwordFinder) {
			PemReader pemReader = new PemReader(reader, passwordFinder);
			PrivateKey? pk = null;
			while ((pk = ReadPrivateKey(pemReader)) != null) {
				yield return pk;
			}
		}

		public static PrivateKey? ReadPrivateKey(PemReader pemReader) {
			var pemContent = pemReader.ReadObject();
			if (pemContent == null) return null;
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
				// The PEM contains a full key pair and we want just a private key, simply return the private key from the pair.
				return new PrivateKey(kp.Private);
			}
			else {
				throw new PemException("PEM did contain an object that is not a supported private key or key pair.", pemContentType: pemContent.GetType());
			}
		}

		public static Certificate LoadCertificate(TextReader reader) {
			PemReader pemReader = new PemReader(reader);
			return ReadCertificate(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<Certificate> LoadCertificates(TextReader reader) {
			PemReader pemReader = new PemReader(reader);
			Certificate? cert = null;
			while ((cert = ReadCertificate(pemReader)) != null) {
				yield return cert;
			}
		}

		public static Certificate? ReadCertificate(PemReader pemReader) {
			var pemContent = pemReader.ReadObject();
			if (pemContent == null) return null;
			if (pemContent is X509Certificate cert) {
				return new Certificate(cert);
			}
			else {
				throw new PemException("PEM did contain an object that is not a certificate.", pemContentType: pemContent.GetType());
			}
		}

		public static void Write(TextWriter writer, PublicKey pubKey) {
			var pemWriter = new PemWriter(writer);
			pemWriter.WriteObject(pubKey.wrapped);
		}
		public static void Write(TextWriter writer, Certificate cert) {
			var pemWriter = new PemWriter(writer);
			pemWriter.WriteObject(cert.wrapped);
		}
		private static string GetEncryptionModeStr(PemEncryptionMode encMode) => encMode switch {
			PemEncryptionMode.AES_256_CBC => "AES-256-CBC",
			_ => throw new PemException($"Unsupported PEM encryption mode {encMode}")
		};
		public static void Write(TextWriter writer, PrivateKey privKey, PemEncryptionMode encMode, char[] password, RandomGenerator random) {
			var pemWriter = new PemWriter(writer);
			pemWriter.WriteObject(privKey.wrapped, GetEncryptionModeStr(encMode), password, random.wrapped);
		}
		public static void Write(TextWriter writer, KeyPair keyPair, PemEncryptionMode encMode, char[] password, RandomGenerator random) {
			var pemWriter = new PemWriter(writer);
			pemWriter.WriteObject(keyPair.ToWrappedPair(), GetEncryptionModeStr(encMode), password, random.wrapped);
		}
	}
}
