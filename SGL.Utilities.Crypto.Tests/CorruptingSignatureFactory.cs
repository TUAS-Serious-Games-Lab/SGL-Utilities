using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;
using System;
using System.IO;

namespace SGL.Utilities.Crypto.Tests {
	internal class CorruptingSignatureFactory : ISignatureFactory {
		private class CorruptingSignatureStream : BaseOutputStream {
			private readonly SignerSink sink;
			private bool firstByte = true;

			public CorruptingSignatureStream(SignerSink sink) {
				this.sink = sink;
			}

			public override void Write(byte[] buffer, int offset, int count) {
				if (firstByte) buffer[offset] ^= 0x02; // Corrupt first byte of input to signature
				firstByte = false;
				sink.Write(buffer, offset, count);
			}
			public override void WriteByte(byte b) {
				sink.WriteByte(firstByte ? ((byte)(b ^ 0x02)) : b);
				firstByte = false;
			}
		}

		private class CorruptingSignatureResult : IBlockResult {
			private readonly ISigner signer;

			public CorruptingSignatureResult(ISigner signer) {
				this.signer = signer;
			}

			public byte[] Collect() {
				var sig = signer.GenerateSignature();
				sig[0] ^= 0x02; // Corrupt first byte of signature
				return sig;
			}

			public int Collect(byte[] destination, int offset) {
				byte[] sig = Collect();
				sig.CopyTo(destination, offset);
				return sig.Length;
			}

			public int Collect(Span<byte> output) {
				byte[] sig = Collect();
				sig.CopyTo(output);
				return sig.Length;
			}

			public int GetMaxResultLength() {
				return signer.GetMaxSignatureSize();
			}
		}

		private class CorruptingSignatureCalculator : IStreamCalculator<IBlockResult> {
			private readonly SignerSink sink;
			private readonly CorruptingSignatureFactory factory;

			public CorruptingSignatureCalculator(ISigner signer, CorruptingSignatureFactory factory) {
				sink = new SignerSink(signer);
				this.factory = factory;
			}

			public Stream Stream => factory.CorruptPreSignature ? new CorruptingSignatureStream(sink) : sink;

			public IBlockResult GetResult() {
				return factory.CorruptPostSignature ? new CorruptingSignatureResult(sink.Signer) : new DefaultSignatureResult(sink.Signer);
			}
		}

		private readonly Asn1SignatureFactory inner;
		private readonly AsymmetricKeyParameter privateKey;
		private readonly SecureRandom random;
		private readonly string algorithm;

		public bool CorruptPreSignature { get; set; } = false;
		public bool CorruptPostSignature { get; set; } = false;

		public CorruptingSignatureFactory(string algorithm, AsymmetricKeyParameter privateKey, SecureRandom random) {
			inner = new Asn1SignatureFactory(algorithm, privateKey);
			this.algorithm = algorithm;
			this.privateKey = privateKey;
			this.random = random;
		}

		public object AlgorithmDetails => inner.AlgorithmDetails;

		public IStreamCalculator<IBlockResult> CreateCalculator() {
			return new CorruptingSignatureCalculator(SignerUtilities.InitSigner(algorithm, forSigning: true, privateKey, random), this);
		}
	}
}

