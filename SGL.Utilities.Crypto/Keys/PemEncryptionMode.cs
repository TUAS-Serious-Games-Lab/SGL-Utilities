namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents the encryption mode to use for encrypting private keys using a password-derived key when writing them to a PEM writer (e.g. a .pem file).
	/// </summary>
	public enum PemEncryptionMode {
		/// <summary>
		/// Indicates that the key shall be encrypted using a 256-bit AES using the CBC mode of operation.
		/// </summary>
		AES_256_CBC = 1
	}
}
