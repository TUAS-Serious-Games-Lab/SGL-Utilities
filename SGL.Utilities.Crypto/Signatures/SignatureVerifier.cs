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
	/// <summary>
	/// Provides functionality to verify a cryptographic signature over arbitrary content bytes.
	/// The content is given by one or multiple calls to <see cref="ProcessBytes(byte[])"/> and / or <see cref="ConsumeBytesAsync(Stream, CancellationToken)"/>,
	/// combining the taken bytes in the order of the calls.
	/// Afterwards the signature is verified by calling <see cref="IsValidSignature(byte[])"/> or <see cref="CheckSignature(byte[])"/> with the signed hash to verify.
	/// This finished the verification operation and after calling <see cref="IsValidSignature(byte[])"/> or <see cref="CheckSignature(byte[])"/>, no more content bytes can be added.
	/// </summary>
	public class SignatureVerifier {
		private readonly IStreamCalculator<IVerifier> verificationCalculator;

		/// <summary>
		/// Instantiates a signature verifier using the given public key of the signer and the given digest algorithm.
		/// </summary>
		/// <param name="signerKey">The public key of the key pair used for the signature. This must be the matching public key for the private key used when the data was signed.</param>
		/// <param name="digest">The digest algorithm to apply to the content. The resulting hash is then checked against the signature with <paramref name="signerKey"/>.</param>
		public SignatureVerifier(PublicKey signerKey, SignatureDigest digest = SignatureDigest.Sha256) {
			var verifierFactory = new Asn1VerifierFactory(SignatureHelper.GetSignatureAlgorithmName(signerKey.Type, digest), signerKey.wrapped);
			verificationCalculator = verifierFactory.CreateCalculator();
		}

		/// <summary>
		/// Processes the given bytes as the content or a content fragment.
		/// </summary>
		/// <param name="bytes">The bytes to use as content.</param>
		public void ProcessBytes(byte[] bytes) => verificationCalculator.Stream.Write(bytes, 0, bytes.Length);

		/// <summary>
		/// Asynchronously reads through <paramref name="inputStream"/> until its end and processes the read bytes as content.
		/// </summary>
		/// <param name="inputStream">The stream to read from.</param>
		/// <param name="ct">
		/// A <see cref="CancellationToken"/> to allow cancelling the asynchronous operation.
		/// If a <see cref="ConsumeBytesAsync"/> operation is cancelled, <see cref="IsValidSignature(byte[])"/> or <see cref="CheckSignature(byte[])"/> should not be called afterwards,
		/// as this would verify a signature over an undefined subsequence of the data and thus would very likely fail, even if the stream contains valid data.
		/// </param>
		/// <returns>A task representing the operation.</returns>
		public Task ConsumeBytesAsync(Stream inputStream, CancellationToken ct = default) => inputStream.CopyToAsync(verificationCalculator.Stream, ct);

		/// <summary>
		/// Checks the given <paramref name="signature"/> against the hash over all bytes processed so far and
		/// indicates whether the signature is valid for the content using the public key.
		/// </summary>
		/// <param name="signature">A byte sequence storing the signature, i.e. the signed hash, as returned by <see cref="SignatureGenerator.Sign"/></param>
		/// <returns>
		/// <see langword="true"/> if the content was not altered from the signed content and
		/// the verification was done using the correct public key and an uncorrupted signature.
		/// <see langword="false"/> if any of the following happened:
		/// <list type="bullet">
		/// <item><description>The content was altered in comparison to the signed content.</description></item>
		/// <item><description>The signature bytes were corrupted.</description></item>
		/// <item><description>The verification was done using a public key that doesn't correspond to the private key used for signing.</description></item>
		/// </list>
		/// </returns>
		public bool IsValidSignature(byte[] signature) {
			var result = verificationCalculator.GetResult();
			return result.IsVerified(signature);
		}

		/// <summary>
		/// Checks the given <paramref name="signature"/> against the hash over all bytes processed and throws an exception when the verification failed.
		/// </summary>
		/// <param name="signature">A byte sequence storing the signature, i.e. the signed hash, as returned by <see cref="SignatureGenerator.Sign"/></param>
		/// <exception cref="SignatureException">If any of the following happened:
		/// <list type="bullet">
		/// <item><description>The content was altered in comparison to the signed content.</description></item>
		/// <item><description>The signature bytes were corrupted.</description></item>
		/// <item><description>The verification was done using a public key that doesn't correspond to the private key used for signing.</description></item>
		/// </list></exception>
		public void CheckSignature(byte[] signature) {
			if (!IsValidSignature(signature)) {
				throw new SignatureException("Signature verification failed. The signature didn't match the given content and public key.");
			}
		}
	}
}
