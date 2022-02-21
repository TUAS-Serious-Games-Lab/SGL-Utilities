using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Security.Certificates;
using SGL.Utilities.Crypto.Internals;
using System.IO;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Implementes certificate validation according to a list of trusted certifiacte authority (CA) certificates, where a valid certifiacte must be signed by any of the CA certificates.
	/// </summary>
	public class CACertTrustValidator : ICertificateValidator {
		private readonly CertificateStore caCerts;
		private readonly ILogger<CACertTrustValidator> logger;

		private class TrustedValidator : ICertificateValidator {
			private readonly ILogger<CACertTrustValidator> logger;
			private readonly bool ignoreValidityPeriod;

			public TrustedValidator(ILogger<CACertTrustValidator> logger, bool ignoreValidityPeriod) {
				this.logger = logger;
				this.ignoreValidityPeriod = ignoreValidityPeriod;
			}

			public bool CheckCertificate(Certificate cert) {
				try {
					cert.wrapped.CheckValidity();
					return true;
				}
				catch (CertificateExpiredException) {
					if (ignoreValidityPeriod) {
						logger.LogWarning("The CA certificate {subjDN} is expired (or not yet valid). However, as the CACertTrustValidator is set to ignore the validity period, it will still be used.", cert.SubjectDN);
						return true;
					}
					else {
						logger.LogError("The CA certificate {subjDN} is expired (or not yet valid). It will therefore not be used and certificates signed by it will not be considered valid.", cert.SubjectDN);
						return false;
					}
				}
			}
		}

		/// <summary>
		/// Creates a <see cref="CACertTrustValidator"/> that trusts the CA certificates that are loaded from the given <see cref="TextReader"/> <paramref name="pemContent"/> in PEM format.
		/// </summary>
		/// <param name="pemContent">A <see cref="TextReader"/> that contains the certifiactes as PEM data.</param>
		/// <param name="sourceName">A name for the source behind <paramref name="pemContent"/> to use for log messages. This can, e.g. be a filename or an URL.</param>
		/// <param name="ignoreValidityPeriod">Specified whether validity periods of the CA certificates are ignored. This can be used to avoid expiration of the certifiactes when they are baked into shipped software, that may no be updated in time to replace expired CA certificates.</param>
		/// <param name="logger">A logger to use for the operations of the <see cref="CACertTrustValidator"/> itself.</param>
		/// <param name="caCertStoreLogger">A logger to use for the internal <see cref="CertificateStore"/> that stores the CA certifiactes.</param>
		public CACertTrustValidator(TextReader pemContent, string sourceName, bool ignoreValidityPeriod, ILogger<CACertTrustValidator> logger, ILogger<CertificateStore> caCertStoreLogger) {
			this.logger = logger;
			caCerts = new CertificateStore(new TrustedValidator(logger, ignoreValidityPeriod), caCertStoreLogger);
			caCerts.LoadCertificatesFromReader(pemContent, sourceName);
		}
		/// <summary>
		/// Creates a <see cref="CACertTrustValidator"/> that trusts the CA certificates that are loaded from the given string <paramref name="pemContent"/> in PEM format.
		/// This overload is intended for loading embedded certificates from a string constant and thus uses <see cref="CertificateStore.LoadCertificatesFromEmbeddedStringConstant(string)"/>.
		/// </summary>
		/// <param name="pemContent">A string containing the CA certificates as PEM format text.</param>
		/// <param name="ignoreValidityPeriod">Specified whether validity periods of the CA certificates are ignored. This can be used to avoid expiration of the certifiactes when they are baked into shipped software, that may no be updated in time to replace expired CA certificates.</param>
		/// <param name="logger">A logger to use for the operations of the <see cref="CACertTrustValidator"/> itself.</param>
		/// <param name="caCertStoreLogger">A logger to use for the internal <see cref="CertificateStore"/> that stores the CA certifiactes.</param>
		public CACertTrustValidator(string pemContent, bool ignoreValidityPeriod, ILogger<CACertTrustValidator> logger, ILogger<CertificateStore> caCertStoreLogger) {
			this.logger = logger;
			caCerts = new CertificateStore(new TrustedValidator(logger, ignoreValidityPeriod), caCertStoreLogger);
			caCerts.LoadCertificatesFromEmbeddedStringConstant(pemContent);
		}

		/// <summary>
		/// Checks if the given certifiacte is within its validity period, is signed by a one of the CA certifiactes trusted by this validator, and the siganture can be successfully verified.
		/// The signing certifiacte can be looked up either by finding a CA certificate with a SubjectKeyIdentifier matching the AuthorityKeyIdentifier of the checked certificate, or if no AuthorityKeyIdentifier is present,
		/// by finding a CA certificate with a SubjectDistinguishedName matching the IssueDistinguishedName of the checked certificate.
		/// </summary>
		/// <param name="certificate">The certificate to validate.</param>
		/// <returns>True if the certifiacte passed all checks and was successfully validated, False otherwise.</returns>
		public bool CheckCertificate(Certificate certificate) {
			Certificate? caCert = null;
			var akid = certificate.AuthorityIdentifier;
			if (akid != null) {
				if (akid.Value.KeyIdentifier != null) {
					caCert = caCerts.GetCertificateBySubjectKeyIdentifier(akid.Value.KeyIdentifier);
					if (caCert == null) {
						logger.LogError("The certificate {subjDN} uses the AuthorityKeyIdentifier extension and is signed by {akid}, but no CA certificate with a matching SubjectKeyIdentifier could be found. An additional lookup using the IssuerDistinguishedName field will not be made because the AuthorityKeyIdentifier is present. Thus, the certificate could not be validated.", certificate.SubjectDN, akid);
						return false;
					}
				}
				else {
					// If only the AuthorityCertIssuer and AuthorityCertSerialNumber fields are set, fall through to normal SubjectDN path, but warn due to potential conflicts as working with IssuerDN + SerialNumber is currently not supported.
					logger.LogWarning("The certificate {subjDN} uses the AuthorityKeyIdentifier extension without a key id. Matching by AuthorityCertSerialNumber and AuthorityCertIssuer or IssuerDistinguishedName is however currently not supported." +
						"Falling back to IssuerDistinguishedName-only lookup. If AuthorityKeyIdentifier was given to differentiate between multiple CA certificates with a matching SubjectDistinguishedName by serial number, this may cause the wrong certificate to be found." +
						"Thus the validation may fail.", certificate.SubjectDN);
				}
			}
			if (caCert == null) {
				caCert = caCerts.GetCertificateBySubjectDN(certificate.IssuerDN);
			}
			if (caCert == null) {
				logger.LogError("The certificate {subjDN} is signed by {issDN} which is no known CA certificate. Thus, the certificate could not be validated.", certificate.SubjectDN, certificate.IssuerDN);
				return false;
			}
			var outcome = CertificateCheckHelper.CheckCertificate(certificate, caCert.PublicKey);
			switch (outcome) {
				case CertificateCheckOutcome.Valid:
					return true;
				case CertificateCheckOutcome.OutOfValidityPeriod:
					logger.LogError("The certificate {subjDN} is out of it's validity period (expired or not yet valid).", certificate.SubjectDN);
					return false;
				case CertificateCheckOutcome.InvalidSignature:
					logger.LogError("The certificate {subjDN} claims to be signed by {issDN} but the verification of the signature against the signers public key failed. This either indicates an attempted manipulation of the certificate or a problem with the signing process or the signer certificate.", certificate.SubjectDN, certificate.IssuerDN);
					return false;
				case CertificateCheckOutcome.OtherError:
					return false;
				default:
					return false;
			}
		}
	}
}
