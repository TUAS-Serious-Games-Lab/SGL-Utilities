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
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;

namespace SGL.Utilities.Crypto.Internals {
	internal class GeneratorHelper {
		public static KeyPair GenerateEcKeyPair(RandomGenerator random, int keyLength, string? curveName = null) {
			try {
				var keyGen = new ECKeyPairGenerator();
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
				var keyGen = new RsaKeyPairGenerator();
				keyGen.Init(new KeyGenerationParameters(random.wrapped, keyLength));
				return new KeyPair(keyGen.GenerateKeyPair());
			}
			catch (Exception ex) {
				throw new KeyException("Failed generating RSA key pair.", ex);
			}
		}
		public static KeyPair GenerateKeyPair(RandomGenerator random, KeyType type, int keyLength, string? paramSetName = null) => type switch {
			KeyType.RSA => GenerateRsaKeyPair(random, keyLength),
			KeyType.EllipticCurves => GenerateEcKeyPair(random, keyLength, paramSetName),
			_ => throw new KeyException("Unsupported key type"),
		};

		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
				BigInteger serialNumber, SignatureDigest signatureDigest = SignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false,
				KeyUsages keyUsages = KeyUsages.NoneDefined, (bool IsCA, int? CAPathLength)? caConstraint = null) {
			var certGen = new X509V3CertificateGenerator();
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
				if ((keyUsages & KeyUsages.AllBasic) != 0) {
					var extObj = new KeyUsage((int)(keyUsages & KeyUsages.AllBasic));
					certGen.AddExtension(X509Extensions.KeyUsage, true, extObj.ToAsn1Object());
				}
				if ((keyUsages & KeyUsages.AllSupportedExt) != 0) {
					ExtendedKeyUsage extObj = MapKeyUsageEnumToExtensionObject(keyUsages);
					certGen.AddExtension(X509Extensions.ExtendedKeyUsage, true, extObj.ToAsn1Object());
				}
				if (caConstraint != null) {
					BasicConstraints extObj;
					if (caConstraint.Value.IsCA) {
						extObj = new BasicConstraints(caConstraint.Value.CAPathLength ?? 0);
					}
					else {
						extObj = new BasicConstraints(false);
					}
					certGen.AddExtension(X509Extensions.BasicConstraints, true, extObj.ToAsn1Object());
				}
			}
			catch (Exception ex) {
				throw new CertificateException("Failed setting up certificate data.", ex);
			}
			var signatureFactory = new Asn1SignatureFactory(SignatureHelper.GetSignatureAlgorithmName(signerKey.Type, signatureDigest), signerKey.wrapped);
			try {
				return new Certificate(certGen.Generate(signatureFactory));
			}
			catch (Exception ex) {
				throw new CertificateException("Failed generating certificate.", ex);
			}
		}

		internal static ExtendedKeyUsage MapKeyUsageEnumToExtensionObject(KeyUsages keyUsages) {
			var purposeIds = new List<KeyPurposeID>();
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtAnyPurpose, KeyPurposeID.AnyExtendedKeyUsage, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtServerAuth, KeyPurposeID.id_kp_serverAuth, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtClientAuth, KeyPurposeID.id_kp_clientAuth, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtCodeSigning, KeyPurposeID.id_kp_codeSigning, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtEmailProtection, KeyPurposeID.id_kp_emailProtection, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtIpsecEndSystem, KeyPurposeID.id_kp_ipsecEndSystem, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtIpsecTunnel, KeyPurposeID.id_kp_ipsecTunnel, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtIpsecUser, KeyPurposeID.id_kp_ipsecUser, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtTimeStamping, KeyPurposeID.id_kp_timeStamping, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtOcspSigning, KeyPurposeID.id_kp_OCSPSigning, purposeIds);
			AddPurposeIfUsagePresent(keyUsages, KeyUsages.ExtSmartCardLogon, KeyPurposeID.id_kp_smartcardlogon, purposeIds);
			var extObj = new ExtendedKeyUsage(purposeIds);
			return extObj;
		}

		private static void AddPurposeIfUsagePresent(KeyUsages presentUsages, KeyUsages usageToCheck, KeyPurposeID purpose, List<KeyPurposeID> purposes) {
			if (presentUsages.HasFlag(usageToCheck)) {
				purposes.Add(purpose);
			}
		}

		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey,
			TimeSpan validityDuration, BigInteger serialNumber, SignatureDigest signatureDigest = SignatureDigest.Sha256,
			KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined,
			(bool IsCA, int? CAPathLength)? caConstraint = null) =>
				GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, DateTime.UtcNow, DateTime.UtcNow.Add(validityDuration), serialNumber,
					signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages, caConstraint);
		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey,
			DateTime validFrom, DateTime validTo, RandomGenerator randomSerialNumberGen, int serialNumberLength, SignatureDigest signatureDigest = SignatureDigest.Sha256,
			KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined,
			(bool IsCA, int? CAPathLength)? caConstraint = null) =>
				GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, randomSerialNumberGen.GetRandomBigInteger(serialNumberLength),
					signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages, caConstraint);
		public static Certificate GenerateCertificate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey,
			TimeSpan validityDuration, RandomGenerator randomSerialNumberGen, int serialNumberLength, SignatureDigest signatureDigest = SignatureDigest.Sha256,
			KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined,
			(bool IsCA, int? CAPathLength)? caConstraint = null) =>
				GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, DateTime.UtcNow, DateTime.UtcNow.Add(validityDuration),
					randomSerialNumberGen.GetRandomBigInteger(serialNumberLength), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages, caConstraint);
	}
}
