namespace SGL.Utilities.Crypto.Keys {
	/// <summary>
	/// Represents a type of a <see cref="KeyPair"/>, <see cref="PrivateKey"/>, or <see cref="PublicKey"/>.
	/// </summary>
	public enum KeyType {
		/// <summary>
		/// The key (pair) is for the RSA algorithm.
		/// </summary>
		RSA,
		/// <summary>
		/// The key (pair) is for Elliptic Curve Cryptography.
		/// </summary>
		EllipticCurves
	}
}
