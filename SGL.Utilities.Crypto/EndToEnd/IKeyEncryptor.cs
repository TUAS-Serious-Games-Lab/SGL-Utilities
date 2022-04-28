using SGL.Utilities.Crypto.Keys;
using System.Collections.Generic;

namespace SGL.Utilities.Crypto.EndToEnd {
	/// <summary>
	/// Specifies the interface for classes that provide key encryption functionality for <see cref="DataEncryptor"/>.
	/// </summary>
	public interface IKeyEncryptor {
		/// <summary>
		/// Encrypts the given data key and generates a map of encrypted data key copies for the recipients and optionally a shared message public key.
		/// </summary>
		/// <param name="dataKey">The data key to encrypt, usually coming from <see cref="DataEncryptor"/>.</param>
		/// <returns>The mapping of recipient key ids to the corresponding encrypted copies of the data key and associated meta data, and optionally a shared message public key.</returns>
		(Dictionary<KeyId, DataKeyInfo> dataKeys, byte[]? messagePubKey) EncryptDataKey(byte[] dataKey);
	}
}
