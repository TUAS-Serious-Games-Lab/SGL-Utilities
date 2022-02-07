using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto {
	public class KeyOnlyTrustValidator : ICertificateValidator {
		private readonly ILogger<KeyOnlyTrustValidator> logger;
		private readonly List<AsymmetricKeyParameter> trustedPublicKeys;

		public bool CheckCertificate(X509Certificate cert) {
			foreach (var trustedKey in trustedPublicKeys) {
				var outcome = CertificateCheckHelper.CheckCertificate(cert, trustedKey);
				if (outcome == CertificateCheckHelper.Outcome.OutOfValidityPeriod) {
					logger.LogError("The certificate {subjDN} is out of it's validity period (expired or not yet valid).", cert.SubjectDN);
					return false;
				}
				if (outcome == CertificateCheckHelper.Outcome.Valid) {
					return true;
				}
			}
			logger.LogError("The certificate {subjDN} could not be validated with any of the trusted public keys.", cert.SubjectDN);
			return false;
		}
	}
}
