using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Security.Certificates;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto {
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

			public bool CheckCertificate(X509Certificate cert) {
				try {
					cert.CheckValidity();
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

		public CACertTrustValidator(string pemContent, bool ignoreValidityPeriod, ILogger<CACertTrustValidator> logger, ILogger<CertificateStore> caCertStoreLogger) {
			this.logger = logger;
			caCerts = new CertificateStore(caCertStoreLogger, new TrustedValidator(logger, ignoreValidityPeriod));
			caCerts.LoadCertificatesFromEmbeddedStringConstant(pemContent);
		}

		public bool CheckCertificate(X509Certificate cert) {
			X509Certificate? caCert = null;
			var akidEnc = cert.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
			if (akidEnc != null) {
				var akid = AuthorityKeyIdentifier.GetInstance(Asn1Object.FromByteArray(akidEnc.GetOctets()));
				var akidRaw = akid?.GetKeyIdentifier();
				if (akidRaw != null) {
					caCert = caCerts.GetCertificateBySubjectKeyIdentifier(new SubjectKeyIdentifier(akidRaw));
					if (caCert == null) {
						logger.LogError("The certificate {subjDN} uses the AuthorityKeyIdentifier extension and is signed by {akid}, but no CA certificate with a matching SubjectKeyIdentifier could be found. An additional lookup using the IssuerDistinguishedName field will not be made because the AuthorityKeyIdentifier is present. Thus, the certificate could not be validated.", cert.SubjectDN, akid);
						return false;
					}
				}
				else {
					// If only the AuthorityCertIssuer and AuthorityCertSerialNumber fields are set, fall through to normal SubjectDN path, but warn due to potential conflicts as working with IssuerDN + SerialNumber is currently not supported.
					logger.LogWarning("The certificate {subjDN} uses the AuthorityKeyIdentifier extension without a key id. Matching by AuthorityCertSerialNumber and AuthorityCertIssuer or IssuerDistinguishedName is however currently not supported." +
						"Falling back to IssuerDistinguishedName-only lookup. If AuthorityKeyIdentifier was given to differentiate between multiple CA certificates with a matching SubjectDistinguishedName by serial number, this may cause the wrong certificate to be found." +
						"Thus the validation may fail.", cert.SubjectDN);
				}
			}
			if (caCert == null) {
				caCert = caCerts.GetCertificateBySubjectDN(cert.IssuerDN);
			}
			if (caCert == null) {
				logger.LogError("The certificate {subjDN} is signed by {issDN} which is no known CA certificate. Thus, the certificate could not be validated.", cert.SubjectDN, cert.IssuerDN);
				return false;
			}
			var outcome = CertificateCheckHelper.CheckCertificate(cert, caCert.GetPublicKey());
			switch (outcome) {
				case CertificateCheckHelper.Outcome.Valid:
					return true;
				case CertificateCheckHelper.Outcome.OutOfValidityPeriod:
					logger.LogError("The certificate {subjDN} is out of it's validity period (expired or not yet valid).", cert.SubjectDN);
					return false;
				case CertificateCheckHelper.Outcome.InvalidSignature:
					logger.LogError("The certificate {subjDN} claims to be signed by {issDN} but the verification of the signature against the signers public key failed. This either indicates an attempted manipulation of the certificate or a problem with the signing process or the signer certificate.", cert.SubjectDN, cert.IssuerDN);
					return false;
				case CertificateCheckHelper.Outcome.OtherError:
					return false;
				default:
					return false;
			}
		}
	}
}
