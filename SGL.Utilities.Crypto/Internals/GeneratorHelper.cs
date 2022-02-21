﻿using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;

namespace SGL.Utilities.Crypto.Internals {
	internal class GeneratorHelper {
		public static KeyPair GenerateEcKeyPair(RandomGenerator random, int keyLength, string? curveName = null) {
			try {
				ECKeyPairGenerator keyGen = new ECKeyPairGenerator();
				if (curveName != null) {
					keyGen.Init(new ECKeyGenerationParameters(ECNamedCurveTable.GetOid(curveName), random.wrapped));
				}
				else {
					keyGen.Init(new KeyGenerationParameters(random.wrapped, keyLength));
				}
				return new KeyPair(keyGen.GenerateKeyPair());
			}
			catch (Exception ex) {
				throw new KeyException("Failed generating Elliptic Curves key pair.", ex);
			}
		}
		public static KeyPair GenerateRsaKeyPair(RandomGenerator random, int keyLength) {
			try {
				RsaKeyPairGenerator keyGen = new RsaKeyPairGenerator();
				keyGen.Init(new KeyGenerationParameters(random.wrapped, keyLength));
				return new KeyPair(keyGen.GenerateKeyPair());
			}
			catch (Exception ex) {
				throw new KeyException("Failed generating Elliptic Curves key pair.", ex);
			}
		}
		public static KeyPair GenerateKeyPair(RandomGenerator random, KeyType type, int keyLength, string? paramSetName = null) {
			switch (type) {
				case KeyType.RSA: return GenerateRsaKeyPair(random, keyLength);
				case KeyType.EllipticCurves: return GenerateEcKeyPair(random, keyLength, paramSetName);
				default: throw new KeyException("Unsupported key type");
			}
		}

		private static string GetSignerName(KeyType keyType, CertificateSignatureDigest digest) => keyType switch {
			KeyType.RSA => digest switch {
				CertificateSignatureDigest.Sha256 => PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id,
				CertificateSignatureDigest.Sha384 => PkcsObjectIdentifiers.Sha384WithRsaEncryption.Id,
				CertificateSignatureDigest.Sha512 => PkcsObjectIdentifiers.Sha512WithRsaEncryption.Id,
				_ => throw new CertificateException($"Unsupported digest {digest}")
			},
			KeyType.EllipticCurves => digest switch {
				CertificateSignatureDigest.Sha256 => X9ObjectIdentifiers.ECDsaWithSha256.Id,
				CertificateSignatureDigest.Sha384 => X9ObjectIdentifiers.ECDsaWithSha384.Id,
				CertificateSignatureDigest.Sha512 => X9ObjectIdentifiers.ECDsaWithSha512.Id,
				_ => throw new CertificateException($"Unsupported digest {digest}")
			},
			_ => throw new CertificateException($"Unsupported key type {keyType}")
		};

		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
				BigInteger serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) {
			X509V3CertificateGenerator certGen = new X509V3CertificateGenerator();
			try {
				certGen.SetIssuerDN(signerIdentity.wrapped);
				certGen.SetSubjectDN(subjectIdentity.wrapped);
				certGen.SetPublicKey(subjectKey.wrapped);
				certGen.SetNotBefore(validFrom);
				certGen.SetNotAfter(validTo);
				certGen.SetSerialNumber(serialNumber);

				if (authorityKeyIdentifier != null) {
					certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifier(authorityKeyIdentifier.wrapped.GetKeyIdentifier()));
				}
				if (generateSubjectKeyIdentifier) {
					certGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new KeyIdentifier(subjectKey).wrapped);
				}
			}
			catch (Exception ex) {
				throw new CertificateException("Failed setting up certificate data.", ex);
			}
			Asn1SignatureFactory signatureFactory = new Asn1SignatureFactory(GetSignerName(signerKey.Type, signatureDigest), signerKey.wrapped);
			try {
				return new Certificate(certGen.Generate(signatureFactory));
			}
			catch (Exception ex) {
				throw new CertificateException("Failed generating certificate.", ex);
			}
		}
		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			BigInteger serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, DateTime.UtcNow, DateTime.UtcNow.Add(validityDuration), serialNumber, signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			RandomGenerator randomSerialNumberGen, int serialNumberLength, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, randomSerialNumberGen.GetRandomBigInteger(serialNumberLength), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			RandomGenerator randomSerialNumberGen, int serialNumberLength, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, DateTime.UtcNow, DateTime.UtcNow.Add(validityDuration), randomSerialNumberGen.GetRandomBigInteger(serialNumberLength), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
	}
}
