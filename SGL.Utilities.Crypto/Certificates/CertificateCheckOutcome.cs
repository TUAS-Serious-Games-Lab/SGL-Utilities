namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents the result of a certificate verification check.
	/// </summary>
	public enum CertificateCheckOutcome {
		/// <summary>
		/// The certificate was successfully verified and is within it's validity period.
		/// </summary>
		Valid,
		/// <summary>
		/// The check failed because the current date was not within the validity period of the certificate.
		/// Usually this means, the certificate has expired, but a not-yet-valid certificate also produces this result.
		/// </summary>
		OutOfValidityPeriod,
		/// <summary>
		/// The check failed because the signature verification using the given / found public key of the issuer failed.
		/// </summary>
		InvalidSignature,
		/// <summary>
		/// The check failed due to some other, unexpected error.
		/// </summary>
		OtherError
	}
}
