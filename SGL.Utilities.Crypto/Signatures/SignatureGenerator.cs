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
	/// <summary>
	/// Provides functionality to generate a cryptographic signature over arbitrary content bytes.
	/// The content is given by one or multiple calls to <see cref="ProcessBytes(byte[])"/> and / or <see cref="ConsumeBytesAsync(Stream, CancellationToken)"/>,
	/// combining the taken bytes in the order of the calls.
	/// Afterwards the signature is obtained by calling <see cref="Sign()"/>, which finished the operation.
	/// After calling <see cref="Sign()"/>, no more content bytes can be added.
	/// </summary>
	public class SignatureGenerator {
		private readonly IStreamCalculator<IBlockResult> signatureCalculator;

		/// <summary>
		/// Instantiates a signature generator using the given private key of the signer and the given digest algorithm.
		/// </summary>
		/// <param name="signerKey">The private key of the key pair to use for the signature. The verification must be done with the matching public key.</param>
		/// <param name="digest">The digest algorithm to apply to the content. The resulting hash is then signed using <paramref name="signerKey"/>.</param>
		/// <param name="random">
		/// Allows providing the random soure for signature algorithms that need a random source for nonces etc.,
		/// if not provided, an internally generated random source is used when needed.
		/// </param>
		public SignatureGenerator(PrivateKey signerKey, SignatureDigest digest = SignatureDigest.Sha256, RandomGenerator? random = null) {
			var signatureFactory = new Asn1SignatureFactory(SignatureHelper.GetSignatureAlgorithmName(signerKey.Type, digest), signerKey.wrapped, random?.wrapped);
			signatureCalculator = signatureFactory.CreateCalculator();
		}

		/// <summary>
		/// Processes the given bytes as the content or a content fragment.
		/// </summary>
		/// <param name="bytes">The bytes to use as content.</param>
		public void ProcessBytes(byte[] bytes) => signatureCalculator.Stream.Write(bytes, 0, bytes.Length);

		/// <summary>
		/// Asynchronously reads through <paramref name="inputStream"/> until its end and processes the read bytes as content.
		/// </summary>
		/// <param name="inputStream">The stream to read from.</param>
		/// <param name="ct">
		/// A <see cref="CancellationToken"/> to allow cancelling the asynchronous operation.
		/// If a <see cref="ConsumeBytesAsync"/> operation is cancelled, <see cref="Sign"/> should not be called afterwards,
		/// as the signature would be over an undefined subsequence of the data and thus would itself be undefined.
		/// </param>
		/// <returns>A task representing the operation.</returns>
		public Task ConsumeBytesAsync(Stream inputStream, CancellationToken ct = default) => inputStream.CopyToAsync(signatureCalculator.Stream, ct);

		/// <summary>
		///	Creates a signature over all bytes processed so far and returns the raw signature.
		/// </summary>
		/// <returns>A byte sequence storing the signature, i.e. the signed hash.</returns>
		public byte[] Sign() {
			signatureCalculator.Stream.Flush();
			signatureCalculator.Stream.Dispose();
			var result = signatureCalculator.GetResult();
			return result.Collect();
		}
	}
}
