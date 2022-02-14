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
	/// <summary>
	/// Implementes certificate validation according to a list of trusted public keys, where a valid certifiacte must be signed by one of these public keys.
	/// </summary>
	public class KeyOnlyTrustValidator : ICertificateValidator {
		private readonly ILogger<KeyOnlyTrustValidator> logger;
		private readonly List<AsymmetricKeyParameter> trustedPublicKeys = new List<AsymmetricKeyParameter>();

		/// <summary>
		/// Creates a <see cref="KeyOnlyTrustValidator"/> that trusts the signer public keys that are loaded from the given <paramref name="reader"/> in PEM format.
		/// </summary>
		/// <param name="reader">A <see cref="TextReader"/> that contains the signer public keys as PEM data.</param>
		/// <param name="sourceName">A name for the source behind <paramref name="reader"/> to use for log messages. This can, e.g. be a filename or an URL.</param>
		/// <param name="logger">A logger to use for the operations of the validator.</param>
		public KeyOnlyTrustValidator(TextReader reader, string sourceName, ILogger<KeyOnlyTrustValidator> logger) {
			this.logger = logger;
			LoadPublicKeysFromReader(reader, sourceName);
		}


		/// <summary>
		/// Creates a <see cref="KeyOnlyTrustValidator"/> that trusts the signer public keys that are loaded from the given string in PEM format.
		/// This overload is intended for loading embedded certificates from a string constant and thus uses <c>[embedded data]</c> as the source name.
		/// </summary>
		/// <param name="pemContent">A string that contains the signer public keys as PEM data.</param>
		/// <param name="logger">A logger to use for the operations of the validator.</param>
		public KeyOnlyTrustValidator(string pemContent, ILogger<KeyOnlyTrustValidator> logger) {
			this.logger = logger;
			using var reader = new StringReader(pemContent);
			LoadPublicKeysFromReader(reader, "[embedded data]");
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

		/// <summary>
		/// Checks if the given certifiacte is within its validity period, is signed by a one of the public keys trusted by this validator, and the siganture can be successfully verified.
		/// </summary>
		/// <remarks>
		/// As the public keys are not associated with the certificate through metadata, the trusted keys are tried one after another to verify the signature until ony succeeds.
		/// If none of them can verify the signature, the certifiacte is rejected.
		/// </remarks>
		/// <param name="cert">The certificate to validate.</param>
		/// <returns>True if the certifiacte passed all checks and was successfully validated, False otherwise.</returns>
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
