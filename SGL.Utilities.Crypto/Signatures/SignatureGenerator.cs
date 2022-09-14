using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Utilities.IO;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Signatures {
	public class SignatureGenerator {
		private readonly PrivateKey signerKey;
		private readonly SignatureDigest digest;
		// Some signature algorithms need a random source for nonces etc. If not provided, internals create a random source on the fly.
		private readonly RandomGenerator? random;

		private readonly IStreamCalculator signatureCalculator;

		public SignatureGenerator(PrivateKey signerKey, SignatureDigest digest = SignatureDigest.Sha256, RandomGenerator? random = null) {
			this.signerKey = signerKey;
			this.digest = digest;
			this.random = random;
			var signatureFactory = new Asn1SignatureFactory(SignatureHelper.GetSignatureAlgorithmName(signerKey.Type, digest), signerKey.wrapped, random?.wrapped);
			signatureCalculator = signatureFactory.CreateCalculator();
		}

		public void ProcessBytes(byte[] bytes) => signatureCalculator.Stream.Write(bytes, 0, bytes.Length);

		public Task ConsumeBytesAsync(Stream inputStream, CancellationToken ct = default) => inputStream.CopyToAsync(signatureCalculator.Stream, ct);

		public byte[] Sign() {
			signatureCalculator.Stream.Flush();
			signatureCalculator.Stream.Dispose();
			var result = signatureCalculator.GetResult();
			if (result is IBlockResult blockResult) {
				return blockResult.Collect();
			}
			else {
				throw new InvalidOperationException("The signature calculator didn't return the expected result.");
			}
		}
	}
}
