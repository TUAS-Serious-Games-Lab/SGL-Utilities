namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents the type of a key pair, public key, or private key.
	/// </summary>
	public enum KeyType {
		/// <summary>
		/// The key (pair) is for the RSA algorithm.
		/// </summary>
		RSA = 1,
		/// <summary>
		/// The key (pair) is for Elliptic Curve Cryptography.
		/// </summary>
		EllipticCurves = 2
	}
}
