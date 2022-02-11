using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto {
	public class KeyOnlyTrustValidator : ICertificateValidator {
		private readonly ILogger<KeyOnlyTrustValidator> logger;
		private readonly List<AsymmetricKeyParameter> trustedPublicKeys = new List<AsymmetricKeyParameter>();


		public KeyOnlyTrustValidator(TextReader reader, string sourceName, ILogger<KeyOnlyTrustValidator> logger) {
			this.logger = logger;
			LoadPublicKeysFromReader(reader, sourceName);
		}

		private void LoadPublicKeysFromReader(TextReader reader, string sourceName) {
			PemReader pemReader = new PemReader(reader);
			object content;
			int loadedCount = 0;
			while ((content = pemReader.ReadObject()) != null) {
				if (content is AsymmetricKeyParameter key && !key.IsPrivate) {
					trustedPublicKeys.Add(key);
					loadedCount++;
				}
				else if (content is AsymmetricKeyParameter privKey && privKey.IsPrivate) {
					logger.LogWarning("Source {src} contained a PRIVATE KEY of type {type}, expecting only public keys, ignoring this object. This should not be present here at all.", sourceName, content.GetType().FullName);
				}
				else {
					logger.LogWarning("Source {src} contained an object of type {type}, expecting AsymmetricKeyParameter objects, ignoring this object.", sourceName, content.GetType().FullName);
				}
			}
			if (loadedCount == 0) {
				logger.LogWarning("Source {src} contained no usable public keys.", sourceName);
			}
		}

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
