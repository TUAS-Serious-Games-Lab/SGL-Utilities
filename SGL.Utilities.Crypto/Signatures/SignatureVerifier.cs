using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Signatures {
	public class SignatureVerifier {
		private readonly PublicKey signerKey;
		private readonly SignatureDigest digest;
		private readonly IStreamCalculator verificationCalculator;

		public SignatureVerifier(PublicKey signerKey, SignatureDigest digest = SignatureDigest.Sha256) {
			var verifierFactory = new Asn1VerifierFactory(SignatureHelper.GetSignatureAlgorithmName(signerKey.Type, digest), signerKey.wrapped);
			verificationCalculator = verifierFactory.CreateCalculator();
			this.signerKey = signerKey;
			this.digest = digest;
		}
		public void ProcessBytes(byte[] bytes) => verificationCalculator.Stream.Write(bytes, 0, bytes.Length);

		public Task ConsumeBytesAsync(Stream inputStream, CancellationToken ct = default) => inputStream.CopyToAsync(verificationCalculator.Stream, ct);

		public bool IsValidSignature(byte[] signature) {
			var result = verificationCalculator.GetResult();
			if (result is IVerifier verifier) {
				return verifier.IsVerified(signature);
			}
			else {
				throw new InvalidOperationException("The verification calculator didn't return the expected result.");
			}
		}
	}
}
