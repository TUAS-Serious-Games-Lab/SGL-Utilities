﻿using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ConstrainedExecution;

namespace SGL.Utilities.Crypto.Internals {
	internal static class PemHelper {
		public class FuncPasswordFinder : IPasswordFinder {
			private Func<char[]> passwordGetter;

			public FuncPasswordFinder(Func<char[]> passwordGetter) {
				this.passwordGetter = passwordGetter;
			}

			public char[] GetPassword() => passwordGetter();
		}

		public static KeyPair? TryLoadKeyPair(TextReader reader, IPasswordFinder passwordFinder) {
			var pemReader = new PemReader(reader, passwordFinder);
			return ReadKeyPair(pemReader);
		}
		public static IEnumerable<KeyPair> LoadKeyPairs(TextReader reader, IPasswordFinder passwordFinder) {
			var pemReader = new PemReader(reader, passwordFinder);
			KeyPair? kp;
			while ((kp = ReadKeyPair(pemReader)) != null) {
				yield return kp;
			}
		}

		public static KeyPair? ReadKeyPair(PemReader pemReader) {
			object pemContent;
			try {
				pemContent = pemReader.ReadObject();
			}
			catch (Exception ex) {
				throw new PemException("Failed reading key pair from PEM reader.", innerException: ex);
			}
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

		public static PublicKey? TryLoadPublicKey(TextReader reader) {
			var pemReader = new PemReader(reader);
			return ReadPublicKey(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<PublicKey> LoadPublicKeys(TextReader reader) {
			var pemReader = new PemReader(reader);
			PublicKey? pk;
			while ((pk = ReadPublicKey(pemReader)) != null) {
				yield return pk;
			}
		}

		public static PublicKey? ReadPublicKey(PemReader pemReader) {
			object pemContent;
			try {
				pemContent = pemReader.ReadObject();
			}
			catch (Exception ex) {
				throw new PemException("Failed reading public key from PEM reader.", innerException: ex);
			}
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

		public static PrivateKey? TryLoadPrivateKey(TextReader reader, IPasswordFinder passwordFinder) {
			var pemReader = new PemReader(reader, passwordFinder);
			return ReadPrivateKey(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<PrivateKey> LoadPrivateKeys(TextReader reader, IPasswordFinder passwordFinder) {
			var pemReader = new PemReader(reader, passwordFinder);
			PrivateKey? pk;
			while ((pk = ReadPrivateKey(pemReader)) != null) {
				yield return pk;
			}
		}

		public static PrivateKey? ReadPrivateKey(PemReader pemReader) {
			object pemContent;
			try {
				pemContent = pemReader.ReadObject();
			}
			catch (Exception ex) {
				throw new PemException("Failed reading private key from PEM reader.", innerException: ex);
			}
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

		public static Certificate? TryLoadCertificate(TextReader reader) {
			var pemReader = new PemReader(reader);
			return ReadCertificate(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}
		public static IEnumerable<Certificate> LoadCertificates(TextReader reader) {
			var pemReader = new PemReader(reader);
			Certificate? cert;
			while ((cert = ReadCertificate(pemReader)) != null) {
				yield return cert;
			}
		}

		public static Certificate? ReadCertificate(PemReader pemReader) {
			object pemContent;
			try {
				pemContent = pemReader.ReadObject();
			}
			catch (Exception ex) {
				throw new PemException("Failed reading certificate from PEM reader.", innerException: ex);
			}
			if (pemContent == null) return null;
			if (pemContent is X509Certificate cert) {
				return new Certificate(cert);
			}
			else {
				throw new PemException("PEM did contain an object that is not a certificate.", pemContentType: pemContent.GetType());
			}
		}

		public static void Write(TextWriter writer, PublicKey pubKey) {
			try {
				var pemWriter = new PemWriter(writer);
				pemWriter.WriteObject(pubKey.wrapped);
			}
			catch (CryptographyException) {
				throw;
			}
			catch (Exception ex) {
				throw new PemException("Failed writing public key to PEM writer.", innerException: ex);
			}
		}
		public static void Write(TextWriter writer, Certificate cert) {
			try {
				var pemWriter = new PemWriter(writer);
				pemWriter.WriteObject(cert.wrapped);
			}
			catch (CryptographyException) {
				throw;
			}
			catch (Exception ex) {
				throw new PemException("Failed writing certificate to PEM writer.", innerException: ex);
			}
		}
		private static string GetEncryptionModeStr(PemEncryptionMode encMode) => encMode switch {
			PemEncryptionMode.AES_256_CBC => "AES-256-CBC",
			_ => throw new PemException($"Unsupported PEM encryption mode {encMode}")
		};
		public static void Write(TextWriter writer, PrivateKey privKey, PemEncryptionMode encMode, char[] password, RandomGenerator random) {
			try {
				var pemWriter = new PemWriter(writer);
				if (encMode == PemEncryptionMode.UNENCRYPTED) {
					pemWriter.WriteObject(privKey.wrapped);
				}
				else {
					pemWriter.WriteObject(privKey.wrapped, GetEncryptionModeStr(encMode), password, random.wrapped);
				}
			}
			catch (CryptographyException) {
				throw;
			}
			catch (Exception ex) {
				throw new PemException("Failed writing private key to PEM writer.", innerException: ex);
			}
		}
		public static void Write(TextWriter writer, KeyPair keyPair, PemEncryptionMode encMode, char[] password, RandomGenerator random) {
			try {
				var pemWriter = new PemWriter(writer);
				if (encMode == PemEncryptionMode.UNENCRYPTED) {
					pemWriter.WriteObject(keyPair.ToWrappedPair());
				}
				else {
					pemWriter.WriteObject(keyPair.ToWrappedPair(), GetEncryptionModeStr(encMode), password, random.wrapped);
				}
			}
			catch (CryptographyException) {
				throw;
			}
			catch (Exception ex) {
				throw new PemException("Failed writing key pair to PEM writer.", innerException: ex);
			}
		}

		internal static CertificateSigningRequest? TryLoadCertificateSigningRequest(TextReader reader) {
			var pemReader = new PemReader(reader);
			return ReadCertificateSigningRequest(pemReader) ?? throw new PemException("Input contained no PEM objects.");
		}

		internal static IEnumerable<CertificateSigningRequest> LoadCertificateSigningRequests(TextReader reader) {
			var pemReader = new PemReader(reader);
			CertificateSigningRequest? csr;
			while ((csr = ReadCertificateSigningRequest(pemReader)) != null) {
				yield return csr;
			}
		}

		private static CertificateSigningRequest? ReadCertificateSigningRequest(PemReader pemReader) {
			object pemContent;
			try {
				pemContent = pemReader.ReadObject();
			}
			catch (Exception ex) {
				throw new PemException("Failed reading certificate signing request from PEM reader.", innerException: ex);
			}
			if (pemContent == null) return null;
			if (pemContent is Pkcs10CertificationRequest csr) {
				return new CertificateSigningRequest(csr);
			}
			else {
				throw new PemException("PEM did contain an object that is not a certificate signing request.", pemContentType: pemContent.GetType());
			}
		}

		internal static void Write(TextWriter writer, CertificateSigningRequest certificateSigningRequest) {
			try {
				var pemWriter = new PemWriter(writer);
				pemWriter.WriteObject(certificateSigningRequest.wrapped);
			}
			catch (CryptographyException) {
				throw;
			}
			catch (Exception ex) {
				throw new PemException("Failed writing certificate signing request to PEM writer.", innerException: ex);
			}
		}
	}
}
