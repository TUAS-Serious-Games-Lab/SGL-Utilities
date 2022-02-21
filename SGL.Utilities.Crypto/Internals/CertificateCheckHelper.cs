using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;

namespace SGL.Utilities.Crypto.Internals {
	internal class CertificateCheckHelper {
		public static CertificateCheckOutcome CheckCertificate(Certificate certificate, PublicKey trustedPublicKey) {
			var cert = certificate.wrapped;
			var pubKey = trustedPublicKey.wrapped;
			try {
				cert.CheckValidity();
			}
			catch (CertificateExpiredException) {
				return CertificateCheckOutcome.OutOfValidityPeriod;
			}
			catch (CertificateNotYetValidException) {
				return CertificateCheckOutcome.OutOfValidityPeriod;
			}
			try {
				var certData = cert.GetTbsCertificate();
				var signature = cert.GetSignature();
				var signer = SignerUtilities.GetSigner(cert.SigAlgName);
				signer.Init(forSigning: false, pubKey);
				signer.BlockUpdate(certData, 0, certData.Length);
				var valid = signer.VerifySignature(signature);
				if (valid) {
					return CertificateCheckOutcome.Valid;
				}
				else {
					return CertificateCheckOutcome.InvalidSignature;
				}
			}
			catch (Exception) {
				return CertificateCheckOutcome.OtherError;
			}
		}
	}
}
