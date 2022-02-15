namespace SGL.Utilities.Crypto {
	/// <summary>
	/// Specifies the interface for classes that provide key decryption functionality for <see cref="DataDecryptor"/>.
	/// </summary>
	public interface IKeyDecryptor {
		/// <summary>
		/// Decrypts the encrypted data key contained in <paramref name="dataKeyInfo"/> and returns the clear text data key.
		/// </summary>
		/// <param name="dataKeyInfo">The encrypted data key and associated metadata.</param>
		/// <param name="sharedSenderPublicKey">The shared sender public key for the encrypted data key, only required if <paramref name="dataKeyInfo"/> uses a shared key.</param>
		/// <returns>The clear text data key, that can be used by <see cref="DataDecryptor"/>.</returns>
		byte[] DecryptKey(DataKeyInfo dataKeyInfo, byte[]? sharedSenderPublicKey);
		/// <summary>
		/// Looks up the encrypted data key from <paramref name="encryptionInfo"/> that matches the recipient key in the instance of the <see cref="IKeyDecryptor"/> implementation and decrypts it.
		/// </summary>
		/// <param name="encryptionInfo">The metadata for a data object for which the data key shall be obtained.</param>
		/// <returns>The decrypted data key, of null if <paramref name="encryptionInfo"/> does not contain an encrypted data key for the recipient key in the instance of the <see cref="IKeyDecryptor"/> implementation.</returns>
		byte[]? DecryptKey(EncryptionInfo encryptionInfo);
	}
}
