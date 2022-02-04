using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SGL.Utilities.Crypto {
	public class KeyTrustChecker {
		private AsymmetricKeyParameter trustedPublicKey;

		public KeyTrustChecker(string pemFileName) {
			trustedPublicKey = loadPublicKey(pemFileName);
		}

		public IEnumerable<KeyValuePair<KeyId, X509Certificate>> FilterByTrust(IEnumerable<KeyValuePair<KeyId, X509Certificate>> availableCertificates) {
			return availableCertificates.Where(cert => {
				bool valid = checkCertificate(cert.Value);
				if (!valid) {
					Console.Error.WriteLine($"Warning: Certificate for {cert.Value.SubjectDN}, key id {cert.Key} failed the validity and signature check. It will not be used.");
				}
				return valid;
			});
		}

		private bool checkCertificate(X509Certificate certificate) {
			try { // We could maybe skip this, as compromised certificates could be 'revoked' by simply removing them from the list on the server.
				certificate.CheckValidity();
			}
			catch (CertificateExpiredException) {
				return false;
			}
			var certData = certificate.GetTbsCertificate();
			var signature = certificate.GetSignature();
			var signer = SignerUtilities.GetSigner(certificate.SigAlgName);
			signer.Init(forSigning: false, trustedPublicKey);
			signer.BlockUpdate(certData, 0, certData.Length);
			return signer.VerifySignature(signature);
		}

		private AsymmetricKeyParameter loadPublicKey(string path) {
			using var fileReader = File.OpenText(path);
			PemReader pemReader = new PemReader(fileReader);
			var content = pemReader.ReadObject();
			if (content == null) {
				throw new Exception($"Warning: Attempting to load a public key from {path} yielded null.");
			}
			if (content is ECPublicKeyParameters ec) {
				return ec;
			}
			else if (content is RsaKeyParameters rsa && !rsa.IsPrivate) {
				return rsa;
			}
			else {
				throw new Exception($"Warning: File {path} contained an object of type {content.GetType().FullName} instead of the expected public key (either RSA or EC).");
			}
		}
	}
}
