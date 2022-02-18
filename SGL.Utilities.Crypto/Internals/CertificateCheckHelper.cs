using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;

namespace SGL.Utilities.Crypto.Internals {
	internal class CertificateCheckHelper {
		public enum Outcome {
			Valid, OutOfValidityPeriod, InvalidSignature, OtherError
		}

		public static Outcome CheckCertificate(Certificate certificate, PublicKey trustedPublicKey) {
			var cert = certificate.wrapped;
			var pubKey = trustedPublicKey.wrapped;
			try {
				cert.CheckValidity();
			}
			catch (CertificateExpiredException) {
				return Outcome.OutOfValidityPeriod;
			}
			catch (CertificateNotYetValidException) {
				return Outcome.OutOfValidityPeriod;
			}
			try {
				var certData = cert.GetTbsCertificate();
				var signature = cert.GetSignature();
				var signer = SignerUtilities.GetSigner(cert.SigAlgName);
				signer.Init(forSigning: false, pubKey);
				signer.BlockUpdate(certData, 0, certData.Length);
				var valid = signer.VerifySignature(signature);
				if (valid) {
					return Outcome.Valid;
				}
				else {
					return Outcome.InvalidSignature;
				}
			}
			catch (Exception) {
				return Outcome.OtherError;
			}
		}
	}
}
