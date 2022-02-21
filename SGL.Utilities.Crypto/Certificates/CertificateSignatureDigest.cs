namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents the digest to use for signing a cerificate.
	/// The digest is combined with a signature technique suitable for the signing private key to pick the signer algorithm.
	/// </summary>
	public enum CertificateSignatureDigest {
		/// <summary>
		/// Represents SHA-256.
		/// </summary>
		Sha256,
		/// <summary>
		/// Represents SHA-384.
		/// </summary>
		Sha384,
		/// <summary>
		/// Represents SHA-512.
		/// </summary>
		Sha512
	}
}
