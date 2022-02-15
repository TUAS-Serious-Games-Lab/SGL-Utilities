namespace SGL.Utilities.Crypto {
	public interface IKeyDecryptor {
		byte[] DecryptKey(DataKeyInfo dataKeyInfo, byte[]? sharedSenderPublicKey);
		byte[]? DecryptKey(EncryptionInfo encryptionInfo);
	}
}