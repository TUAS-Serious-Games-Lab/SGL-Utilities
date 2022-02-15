using System.Collections.Generic;

namespace SGL.Utilities.Crypto {
	public interface IKeyEncryptor {
		(Dictionary<KeyId, DataKeyInfo> dataKeys, byte[]? senderPubKey) EncryptDataKey(byte[] dataKey);
	}
}