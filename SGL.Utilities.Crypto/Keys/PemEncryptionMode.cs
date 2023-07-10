namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents the encryption mode to use for encrypting private keys using a password-derived key when writing them to a PEM writer (e.g. a .pem file).
	/// </summary>
	public enum PemEncryptionMode {
		/// <summary>
		/// Indicates that the key shall be encrypted using a 256-bit AES using the CBC mode of operation.
		/// </summary>
		AES_256_CBC = 1,
		/// <summary>
		/// Indicates that the key shall not be encrypted. This is generally not recommended for security reasons,
		/// but may be required e.g. for generating key material for automated testing.
		/// </summary>
		UNENCRYPTED = int.MaxValue
	}
}
