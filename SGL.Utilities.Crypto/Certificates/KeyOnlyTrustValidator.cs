using Microsoft.Extensions.Logging;
using Org.BouncyCastle.OpenSsl;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Implementes certificate validation according to a list of trusted public keys, where a valid certifiacte must be signed by one of these public keys.
	/// </summary>
	public class KeyOnlyTrustValidator : ICertificateValidator {
		private readonly ILogger<KeyOnlyTrustValidator> logger;
		private readonly List<PublicKey> trustedPublicKeys = new List<PublicKey>();

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
			int loadedCount = 0;
			for (; ; ) {
				try {
					var pubKey = PemHelper.ReadPublicKey(pemReader);
					if (pubKey == null) break;
					trustedPublicKeys.Add(pubKey);
					loadedCount++;
				}
				catch (PemException pe) {
					logger.LogWarning(pe, "Read unexpected PEM object of type {type} from input {src}. This object will be skipped.", pe.PemContentType?.FullName, sourceName);
				}
				catch (KeyException ke) {
					logger.LogWarning(ke, "Read unexpected key from PEM input {src}. This object will be skipped.", sourceName);
				}
			}
			if (loadedCount == 0) {
				logger.LogWarning("Source {src} contained no usable public keys.", sourceName);
			}
			else {
				logger.LogInformation("Loaded {count} trusted public keys from {src}", loadedCount, sourceName);
			}
		}

		/// <summary>
		/// Checks if the given certifiacte is within its validity period, is signed by a one of the public keys trusted by this validator, and the siganture can be successfully verified.
		/// </summary>
		/// <remarks>
		/// As the public keys are not associated with the certificate through metadata, the trusted keys are tried one after another to verify the signature until ony succeeds.
		/// If none of them can verify the signature, the certifiacte is rejected.
		/// </remarks>
		/// <param name="certificate">The certificate to validate.</param>
		/// <returns>True if the certifiacte passed all checks and was successfully validated, False otherwise.</returns>
		public bool CheckCertificate(Certificate certificate) {
			foreach (var trustedKey in trustedPublicKeys) {
				var outcome = CertificateCheckHelper.CheckCertificate(certificate, trustedKey);
				if (outcome == CertificateCheckOutcome.OutOfValidityPeriod) {
					logger.LogError("The certificate {subjDN} is out of it's validity period (expired or not yet valid).", certificate.SubjectDN);
					return false;
				}
				if (outcome == CertificateCheckOutcome.Valid) {
					return true;
				}
			}
			logger.LogError("The certificate {subjDN} could not be validated with any of the trusted public keys.", certificate.SubjectDN);
			return false;
		}
	}
}
