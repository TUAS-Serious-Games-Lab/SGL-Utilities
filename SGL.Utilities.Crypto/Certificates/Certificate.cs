using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Certificates {
	public class Certificate {
		internal X509Certificate wrapped;

		internal Certificate(X509Certificate wrapped) {
			this.wrapped = wrapped;
		}

		public override bool Equals(object? obj) => obj is Certificate certificate && wrapped.Equals(certificate.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();

		public PublicKey PublicKey => new PublicKey(wrapped.GetPublicKey());
		public DistinguishedName SubjectDN => new DistinguishedName(wrapped.SubjectDN);
		public DistinguishedName IssuerDN => new DistinguishedName(wrapped.IssuerDN);
		public DateTime NotBefore => wrapped.NotBefore;
		public DateTime NotAfter => wrapped.NotAfter;
		public byte[] SerialNumber => wrapped.SerialNumber.ToByteArrayUnsigned();

		public KeyIdentifier? SubjectKeyIdentifier {
			get {
				if (skidCache == null) {
					var skid = wrapped.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
					if (skid == null) {
						return null;
					}
					else {
						skidCache = new KeyIdentifier(Org.BouncyCastle.Asn1.X509.SubjectKeyIdentifier.GetInstance(Asn1Object.FromByteArray(skid.GetOctets())));
					}
				}
				return skidCache;
			}
		}
		private KeyIdentifier? skidCache = null;
		public (KeyIdentifier? KeyIdentifier, object? Issuer, byte[]? SerialNumber)? AuthorityIdentifier {
			get {
				if (akidCache == null) {
					var akidEnc = wrapped.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
					if (akidEnc == null) {
						return null;
					}
					else {
						var akid = AuthorityKeyIdentifier.GetInstance(Asn1Object.FromByteArray(akidEnc.GetOctets()));
						var akidRaw = akid?.GetKeyIdentifier();
						var keyIdent = akidRaw != null ? new KeyIdentifier(new SubjectKeyIdentifier(akidRaw)) : null;
						akidCache = (keyIdent, akid?.AuthorityCertIssuer, akid?.AuthorityCertSerialNumber?.ToByteArrayUnsigned());
					}
				}
				return akidCache;
			}
		}
		private (KeyIdentifier? KeyIdentifier, object? Issuer, byte[]? SerialNumber)? akidCache = null;

		public static Certificate LoadOneFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadCertificate(reader);
		public static IEnumerable<Certificate> LoadAllFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadCertificates(reader);
		public void StoreToPem(TextWriter writer) => PemHelper.Write(writer, this);

		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			long serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, BigInteger.ValueOf(serialNumber), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			byte[] serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, new BigInteger(serialNumber), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			RandomGenerator randomSerialNumberGen, int serialNumberLength, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, randomSerialNumberGen, serialNumberLength, signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);

		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			long serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validityDuration, BigInteger.ValueOf(serialNumber), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			byte[] serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validityDuration, new BigInteger(serialNumber), signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			RandomGenerator randomSerialNumberGen, int serialNumberLength, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validityDuration, randomSerialNumberGen, serialNumberLength, signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier);
	}
}
