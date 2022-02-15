using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;
using Org.BouncyCastle.X509;
using System;

namespace SGL.Utilities.Crypto {
	internal class CertificateCheckHelper {
		public enum Outcome {
			Valid, OutOfValidityPeriod, InvalidSignature, OtherError
		}

		public static Outcome CheckCertificate(X509Certificate certificate, AsymmetricKeyParameter trustedPublicKey) {
			try {
				certificate.CheckValidity();
			}
			catch (CertificateExpiredException) {
				return Outcome.OutOfValidityPeriod;
			}
			catch (CertificateNotYetValidException) {
				return Outcome.OutOfValidityPeriod;
			}
			try {
				var certData = certificate.GetTbsCertificate();
				var signature = certificate.GetSignature();
				var signer = SignerUtilities.GetSigner(certificate.SigAlgName);
				signer.Init(forSigning: false, trustedPublicKey);
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
