using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Signatures {
	/// <summary>
	/// Represents the digest to use for a signature.
	/// The digest is combined with a signature technique suitable for the signing private key to pick the signer algorithm.
	/// </summary>
	public enum SignatureDigest {
		/// <summary>
		/// Represents SHA-256.
		/// </summary>
		Sha256 = 2,
		/// <summary>
		/// Represents SHA-384.
		/// </summary>
		Sha384 = 3,
		/// <summary>
		/// Represents SHA-512.
		/// </summary>
		Sha512 = 5
	}
}
