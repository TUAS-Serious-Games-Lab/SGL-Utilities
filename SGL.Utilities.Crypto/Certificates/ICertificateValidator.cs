namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Specifies the interface for classes that check the validity of certificates.
	/// </summary>
	public interface ICertificateValidator {
		/// <summary>
		/// Checks the given certificate according to the criteria of the implementation class.
		/// </summary>
		/// <param name="cert">The certifiacte to check.</param>
		/// <returns>True if the certificate is valid and should be accepted, False if it failed the check and should not be useed.</returns>
		bool CheckCertificate(Certificate cert);
	}
}
