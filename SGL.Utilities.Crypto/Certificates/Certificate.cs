using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents a cryptographic (X509) certificate. It contains a public key and some metadata. It is signed by an issuer using their private key and thus can be verified to be authentic using the issuer's public key.
	/// </summary>
	public class Certificate {
		internal X509Certificate wrapped;

		internal Certificate(X509Certificate wrapped) {
			this.wrapped = wrapped;
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is Certificate certificate && wrapped.Equals(certificate.wrapped);
		/// <inheritdoc/>
		public override int GetHashCode() => wrapped.GetHashCode();
		/// <inheritdoc/>
		public override string? ToString() => wrapped.ToString();

		/// <summary>
		/// Returns the public key of the subject of the certificate.
		/// </summary>
		public PublicKey PublicKey => new PublicKey(wrapped.GetPublicKey());
		/// <summary>
		/// Returns the distinguished name of the subject.
		/// </summary>
		public DistinguishedName SubjectDN => new DistinguishedName(wrapped.SubjectDN);
		/// <summary>
		/// Returns the distinguished name of the issuer.
		/// </summary>
		public DistinguishedName IssuerDN => new DistinguishedName(wrapped.IssuerDN);
		/// <summary>
		/// Returns the date on which the validity period of the certificate begins.
		/// </summary>
		public DateTime NotBefore => wrapped.NotBefore;
		/// <summary>
		/// Returns the date on which the validity period of the certificate ends.
		/// </summary>
		public DateTime NotAfter => wrapped.NotAfter;
		/// <summary>
		/// Returns the serial number of the certificate.
		/// </summary>
		public byte[] SerialNumber => wrapped.SerialNumber.ToByteArray();

		/// <summary>
		/// Returns the key identifier of the subjects public key according to the SubjectKeyIdentifier extension, if the certificate uses it, or null otherwise.
		/// </summary>
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

		/// <summary>
		/// Returns the key identifier, identity and serial number of the issuers public key and CA certificate according to the AuthorityKeyIdentifier extension, if the certificate uses it, or null otherwise.
		/// Currently, only the key identifier is supported by abstracting it using a wrapper object. The identity is currently implementation-dependent and the type may change in later versions. The serial number is provided as a raw byte array.
		/// If the extension is present, a non-null tuple is returned, inside the tuple, the fields that are non-present are null.
		/// </summary>
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
						akidCache = (keyIdent, akid?.AuthorityCertIssuer, akid?.AuthorityCertSerialNumber?.ToByteArray());
					}
				}
				return akidCache;
			}
		}
		private (KeyIdentifier? KeyIdentifier, object? Issuer, byte[]? SerialNumber)? akidCache = null;

		/// <summary>
		/// Provides the allowed usages for the key behind the certificate, according to the KeyUsage and ExtendedKeyUsage extension of the certificate, if present.
		/// </summary>
		public KeyUsages? AllowedKeyUsages {
			get {
				if (keyUsageCache == null) {
					var keyUsageEnc = wrapped.GetExtensionValue(X509Extensions.KeyUsage);
					var extKeyUsageEnc = wrapped.GetExtensionValue(X509Extensions.ExtendedKeyUsage);
					var usages = KeyUsages.NoneDefined;
					if (keyUsageEnc != null) {
						var keyUsageExtension = KeyUsage.GetInstance(keyUsageEnc.GetOctets());
						var keyUsageBitFlags = keyUsageExtension.IntValue;
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.DigitalSignature, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.NonRepudiation, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.KeyEncipherment, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.DataEncipherment, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.KeyAgreement, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.KeyCertSign, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.CrlSign, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.EncipherOnly, ref usages);
						SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.DecipherOnly, ref usages);
					}
					if (extKeyUsageEnc != null) {
						var extKeyUsageExtension = ExtendedKeyUsage.GetInstance(extKeyUsageEnc.GetOctets());
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPServerAuth, KeyUsages.ExtServerAuth, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPClientAuth, KeyUsages.ExtClientAuth, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPCodeSigning, KeyUsages.ExtCodeSigning, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPEmailProtection, KeyUsages.ExtEmailProtection, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPIpsecEndSystem, KeyUsages.ExtIpsecEndSystem, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPIpsecTunnel, KeyUsages.ExtIpsecTunnel, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPIpsecUser, KeyUsages.ExtIpsecUser, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPTimeStamping, KeyUsages.ExtTimeStamping, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPOcspSigning, KeyUsages.ExtOcspSigning, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.IdKPSmartCardLogon, KeyUsages.ExtSmartCardLogon, ref usages);
						SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.AnyExtendedKeyUsage, KeyUsages.ExtAnyPurpose, ref usages);
					}
					keyUsageCache = usages;
				}
				return keyUsageCache;
			}
		}

		private static void SetBitIfUsagePresent(ExtendedKeyUsage extKeyUsages, KeyPurposeID keyPurposeId, KeyUsages usage, ref KeyUsages flagsToSet) {
			if (extKeyUsages.HasKeyPurposeId(keyPurposeId)) {
				flagsToSet |= usage;
			}
		}
		private static void SetBitIfUsagePresent(int keyUsageBitFlags, KeyUsages usage, ref KeyUsages flagsToSet) {
			if ((keyUsageBitFlags & (int)(usage & ~KeyUsages.AllSupportedExt)) != 0) {
				flagsToSet |= usage;
			}
		}

		private KeyUsages? keyUsageCache = null;

		/// <summary>
		/// Loads one certificate from the PEM-encoded data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader containing at least one PEM-encoded certificate.</param>
		/// <returns>The loaded certificate.</returns>
		/// <exception cref="PemException">When the reader either contained no PEM objects or if the PEM object that was read was no certificate.</exception>
		public static Certificate LoadOneFromPem(TextReader reader) => PemHelper.TryLoadCertificate(reader) ?? throw new PemException("Input contained no PEM objects.");
		/// <summary>
		/// Attempts to load one certificate from the PEM-encoded data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader containing at least one PEM-encoded certificate.</param>
		/// <returns>The loaded certificate, or null if <paramref name="reader"/> contains not PEM objects..</returns>
		/// <exception cref="PemException">When the the PEM object read from <paramref name="reader"/> was no certificate.</exception>
		public static Certificate? TryLoadOneFromPem(TextReader reader) => PemHelper.TryLoadCertificate(reader);
		/// <summary>
		/// Loads all certificates from the PEM-encoded data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader containing PEM-encoded certificates.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> containing all loaded certificates.</returns>
		/// <exception cref="PemException">When non-certificate objects were encountered in the PEM data.</exception>
		public static IEnumerable<Certificate> LoadAllFromPem(TextReader reader) => PemHelper.LoadCertificates(reader);
		/// <summary>
		/// Writes the certificate to <paramref name="writer"/> in PEM-encoded form.
		/// </summary>
		/// <param name="writer">The writer to which the certificate should be written.</param>
		public void StoreToPem(TextWriter writer) => PemHelper.Write(writer, this);

		/// <summary>
		/// Attempts to verify the certificate's content according to the given issuer public key.
		/// </summary>
		/// <param name="issuerPublicKey">
		/// Needs to be the public key of the key pair that the issuer used to sign the certificate.
		/// Looking up the correct public key is out-of-scope for this class, however <see cref="ICertificateValidator"/> implementations implement strategies for this.
		/// </param>
		/// <returns>
		/// A <see cref="CertificateCheckOutcome"/> representing the outcome of the check.
		/// If the certificate was successfully verified, <see cref="CertificateCheckOutcome.Valid"/> is returned.
		/// The other enumeration values represent the failure reasons.
		/// </returns>
		public CertificateCheckOutcome Verify(PublicKey issuerPublicKey) => CertificateCheckHelper.CheckCertificate(this, issuerPublicKey);

		/// <summary>
		/// Generates a certificate with the given contents.
		/// </summary>
		/// <param name="signerIdentity">The distinguished name of the issuer / signer, that issues the created certificate.</param>
		/// <param name="signerKey">The private key of the issuer / signer, that will be used to sign the created certificate.</param>
		/// <param name="subjectIdentity">The distinguished name of the subject of the created certificate, i.e. whose key pair is certified by the created certificate.</param>
		/// <param name="subjectKey">The public key that the created certificate will certify.</param>
		/// <param name="validFrom">The date on which the validity period of the certificate begins.</param>
		/// <param name="validTo">The date on which the validity period of the certificate begins.</param>
		/// <param name="serialNumber">The serial number to assign to the certificate.</param>
		/// <param name="signatureDigest">The digest algorithm to use for the signature. It will internally be combined with the type of <paramref name="signerKey"/> to choose the signature algorithm.</param>
		/// <param name="authorityKeyIdentifier">If not null, adds the AuthorityKeyIdentifier extension to the certificate, with the given key identifier. The identifier normally needs to match the <see cref="SubjectKeyIdentifier"/> of the issuer's CA certificate.</param>
		/// <param name="generateSubjectKeyIdentifier">Indicates whether the SubjectKeyIdentifier extension shall be added. If it is added, the identifier is generated using <see cref="KeyIdentifier.KeyIdentifier(PublicKey)"/>.</param>
		/// <param name="keyUsages">
		/// Specifies the valid usages for the <paramref name="subjectKey"/> that the certificate shall indicate.
		/// If no flags are set (<see cref="KeyUsages.NoneDefined"/>), the certificate will contain neither the KeyUsage, nor the ExtendedKeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllBasic"/> are set, the certificate will contain a corresponding KeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllSupportedExt"/> are set, the certificate will contain a corresponding ExtendedKeyUsage extension.
		/// </param>
		/// <returns>The generated and singed certificate.</returns>
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			long serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null,
			bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, BigInteger.ValueOf(serialNumber), signatureDigest,
					authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages);
		/// <summary>
		/// Generates a certificate with the given contents.
		/// </summary>
		/// <param name="signerIdentity">The distinguished name of the issuer / signer, that issues the created certificate.</param>
		/// <param name="signerKey">The private key of the issuer / signer, that will be used to sign the created certificate.</param>
		/// <param name="subjectIdentity">The distinguished name of the subject of the created certificate, i.e. whose key pair is certified by the created certificate.</param>
		/// <param name="subjectKey">The public key that the created certificate will certify.</param>
		/// <param name="validFrom">The date on which the validity period of the certificate begins.</param>
		/// <param name="validTo">The date on which the validity period of the certificate begins.</param>
		/// <param name="serialNumber">The serial number to assign to the certificate.</param>
		/// <param name="signatureDigest">The digest algorithm to use for the signature. It will internally be combined with the type of <paramref name="signerKey"/> to choose the signature algorithm.</param>
		/// <param name="authorityKeyIdentifier">If not null, adds the AuthorityKeyIdentifier extension to the certificate, with the given key identifier. The identifier normally needs to match the <see cref="SubjectKeyIdentifier"/> of the issuer's CA certificate.</param>
		/// <param name="generateSubjectKeyIdentifier">Indicates whether the SubjectKeyIdentifier extension shall be added. If it is added, the identifier is generated using <see cref="KeyIdentifier.KeyIdentifier(PublicKey)"/>.</param>
		/// <param name="keyUsages">
		/// Specifies the valid usages for the <paramref name="subjectKey"/> that the certificate shall indicate.
		/// If no flags are set (<see cref="KeyUsages.NoneDefined"/>), the certificate will contain neither the KeyUsage, nor the ExtendedKeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllBasic"/> are set, the certificate will contain a corresponding KeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllSupportedExt"/> are set, the certificate will contain a corresponding ExtendedKeyUsage extension.
		/// </param>
		/// <returns>The generated and singed certificate.</returns>
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			byte[] serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null,
			bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, new BigInteger(serialNumber), signatureDigest,
					authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages);
		/// <summary>
		/// Generates a certificate with the given contents.
		/// </summary>
		/// <param name="signerIdentity">The distinguished name of the issuer / signer, that issues the created certificate.</param>
		/// <param name="signerKey">The private key of the issuer / signer, that will be used to sign the created certificate.</param>
		/// <param name="subjectIdentity">The distinguished name of the subject of the created certificate, i.e. whose key pair is certified by the created certificate.</param>
		/// <param name="subjectKey">The public key that the created certificate will certify.</param>
		/// <param name="validFrom">The date on which the validity period of the certificate begins.</param>
		/// <param name="validTo">The date on which the validity period of the certificate begins.</param>
		/// <param name="randomSerialNumberGen">A random genrator to use to generate a random serial number from.</param>
		/// <param name="serialNumberLength">The length of the random serial number to generate using <paramref name="randomSerialNumberGen"/>.</param>
		/// <param name="signatureDigest">The digest algorithm to use for the signature. It will internally be combined with the type of <paramref name="signerKey"/> to choose the signature algorithm.</param>
		/// <param name="authorityKeyIdentifier">If not null, adds the AuthorityKeyIdentifier extension to the certificate, with the given key identifier. The identifier normally needs to match the <see cref="SubjectKeyIdentifier"/> of the issuer's CA certificate.</param>
		/// <param name="generateSubjectKeyIdentifier">Indicates whether the SubjectKeyIdentifier extension shall be added. If it is added, the identifier is generated using <see cref="KeyIdentifier.KeyIdentifier(PublicKey)"/>.</param>
		/// <param name="keyUsages">
		/// Specifies the valid usages for the <paramref name="subjectKey"/> that the certificate shall indicate.
		/// If no flags are set (<see cref="KeyUsages.NoneDefined"/>), the certificate will contain neither the KeyUsage, nor the ExtendedKeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllBasic"/> are set, the certificate will contain a corresponding KeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllSupportedExt"/> are set, the certificate will contain a corresponding ExtendedKeyUsage extension.
		/// </param>
		/// <returns>The generated and singed certificate.</returns>
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, DateTime validFrom, DateTime validTo,
			RandomGenerator randomSerialNumberGen, int serialNumberLength, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256,
			KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validFrom, validTo, randomSerialNumberGen, serialNumberLength,
					signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages);

		/// <summary>
		/// Generates a certificate with the given contents.
		/// </summary>
		/// <param name="signerIdentity">The distinguished name of the issuer / signer, that issues the created certificate.</param>
		/// <param name="signerKey">The private key of the issuer / signer, that will be used to sign the created certificate.</param>
		/// <param name="subjectIdentity">The distinguished name of the subject of the created certificate, i.e. whose key pair is certified by the created certificate.</param>
		/// <param name="subjectKey">The public key that the created certificate will certify.</param>
		/// <param name="validityDuration">The duration of the validity period, starting from <see cref="DateTime.UtcNow"/>.</param>
		/// <param name="serialNumber">The serial number to assign to the certificate.</param>
		/// <param name="signatureDigest">The digest algorithm to use for the signature. It will internally be combined with the type of <paramref name="signerKey"/> to choose the signature algorithm.</param>
		/// <param name="authorityKeyIdentifier">If not null, adds the AuthorityKeyIdentifier extension to the certificate, with the given key identifier. The identifier normally needs to match the <see cref="SubjectKeyIdentifier"/> of the issuer's CA certificate.</param>
		/// <param name="generateSubjectKeyIdentifier">Indicates whether the SubjectKeyIdentifier extension shall be added. If it is added, the identifier is generated using <see cref="KeyIdentifier.KeyIdentifier(PublicKey)"/>.</param>
		/// <param name="keyUsages">
		/// Specifies the valid usages for the <paramref name="subjectKey"/> that the certificate shall indicate.
		/// If no flags are set (<see cref="KeyUsages.NoneDefined"/>), the certificate will contain neither the KeyUsage, nor the ExtendedKeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllBasic"/> are set, the certificate will contain a corresponding KeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllSupportedExt"/> are set, the certificate will contain a corresponding ExtendedKeyUsage extension.
		/// </param>
		/// <returns>The generated and singed certificate.</returns>
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			long serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null,
			bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validityDuration, BigInteger.ValueOf(serialNumber), signatureDigest,
					authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages);
		/// <summary>
		/// Generates a certificate with the given contents.
		/// </summary>
		/// <param name="signerIdentity">The distinguished name of the issuer / signer, that issues the created certificate.</param>
		/// <param name="signerKey">The private key of the issuer / signer, that will be used to sign the created certificate.</param>
		/// <param name="subjectIdentity">The distinguished name of the subject of the created certificate, i.e. whose key pair is certified by the created certificate.</param>
		/// <param name="subjectKey">The public key that the created certificate will certify.</param>
		/// <param name="validityDuration">The duration of the validity period, starting from <see cref="DateTime.UtcNow"/>.</param>
		/// <param name="serialNumber">The serial number to assign to the certificate.</param>
		/// <param name="signatureDigest">The digest algorithm to use for the signature. It will internally be combined with the type of <paramref name="signerKey"/> to choose the signature algorithm.</param>
		/// <param name="authorityKeyIdentifier">If not null, adds the AuthorityKeyIdentifier extension to the certificate, with the given key identifier. The identifier normally needs to match the <see cref="SubjectKeyIdentifier"/> of the issuer's CA certificate.</param>
		/// <param name="generateSubjectKeyIdentifier">Indicates whether the SubjectKeyIdentifier extension shall be added. If it is added, the identifier is generated using <see cref="KeyIdentifier.KeyIdentifier(PublicKey)"/>.</param>
		/// <param name="keyUsages">
		/// Specifies the valid usages for the <paramref name="subjectKey"/> that the certificate shall indicate.
		/// If no flags are set (<see cref="KeyUsages.NoneDefined"/>), the certificate will contain neither the KeyUsage, nor the ExtendedKeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllBasic"/> are set, the certificate will contain a corresponding KeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllSupportedExt"/> are set, the certificate will contain a corresponding ExtendedKeyUsage extension.
		/// </param>
		/// <returns>The generated and singed certificate.</returns>
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			byte[] serialNumber, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256, KeyIdentifier? authorityKeyIdentifier = null,
			bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validityDuration, new BigInteger(serialNumber), signatureDigest,
					authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages);
		/// <summary>
		/// Generates a certificate with the given contents.
		/// </summary>
		/// <param name="signerIdentity">The distinguished name of the issuer / signer, that issues the created certificate.</param>
		/// <param name="signerKey">The private key of the issuer / signer, that will be used to sign the created certificate.</param>
		/// <param name="subjectIdentity">The distinguished name of the subject of the created certificate, i.e. whose key pair is certified by the created certificate.</param>
		/// <param name="subjectKey">The public key that the created certificate will certify.</param>
		/// <param name="validityDuration">The duration of the validity period, starting from <see cref="DateTime.UtcNow"/>.</param>
		/// <param name="randomSerialNumberGen">A random genrator to use to generate a random serial number from.</param>
		/// <param name="serialNumberLength">The length of the random serial number to generate using <paramref name="randomSerialNumberGen"/>.</param>
		/// <param name="signatureDigest">The digest algorithm to use for the signature. It will internally be combined with the type of <paramref name="signerKey"/> to choose the signature algorithm.</param>
		/// <param name="authorityKeyIdentifier">If not null, adds the AuthorityKeyIdentifier extension to the certificate, with the given key identifier. The identifier normally needs to match the <see cref="SubjectKeyIdentifier"/> of the issuer's CA certificate.</param>
		/// <param name="generateSubjectKeyIdentifier">Indicates whether the SubjectKeyIdentifier extension shall be added. If it is added, the identifier is generated using <see cref="KeyIdentifier.KeyIdentifier(PublicKey)"/>.</param>
		/// <param name="keyUsages">
		/// Specifies the valid usages for the <paramref name="subjectKey"/> that the certificate shall indicate.
		/// If no flags are set (<see cref="KeyUsages.NoneDefined"/>), the certificate will contain neither the KeyUsage, nor the ExtendedKeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllBasic"/> are set, the certificate will contain a corresponding KeyUsage extension.
		/// If flags in <see cref="KeyUsages.AllSupportedExt"/> are set, the certificate will contain a corresponding ExtendedKeyUsage extension.
		/// </param>
		/// <returns>The generated and singed certificate.</returns>
		public static Certificate Generate(DistinguishedName signerIdentity, PrivateKey signerKey, DistinguishedName subjectIdentity, PublicKey subjectKey, TimeSpan validityDuration,
			RandomGenerator randomSerialNumberGen, int serialNumberLength, CertificateSignatureDigest signatureDigest = CertificateSignatureDigest.Sha256,
			KeyIdentifier? authorityKeyIdentifier = null, bool generateSubjectKeyIdentifier = false, KeyUsages keyUsages = KeyUsages.NoneDefined) =>
				GeneratorHelper.GenerateCertificate(signerIdentity, signerKey, subjectIdentity, subjectKey, validityDuration, randomSerialNumberGen, serialNumberLength,
					signatureDigest, authorityKeyIdentifier, generateSubjectKeyIdentifier, keyUsages);
	}
}
