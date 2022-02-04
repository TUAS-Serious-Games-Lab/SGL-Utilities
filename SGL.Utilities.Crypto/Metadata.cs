using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Utilities.Crypto {

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum DataEncryptionMode {
		AES_256_CCM,
	}

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum KeyEncryptionMode {
		RSA_PKCS1,
		ECDH_KDF2_SHA256_AES_256_CCM
	}

	public class EncryptionInfo {
		public DataEncryptionMode DataMode { get; set; }
		public byte[] IV { get; set; } = new byte[0];
#if !NET6_0_OR_GREATER
		[JsonConverter(typeof(KeyIdDictionaryJsonConverter<DataKeyInfo>))]
#endif
		public Dictionary<KeyId, DataKeyInfo> DataKeys { get; set; } = new Dictionary<KeyId, DataKeyInfo>();
		public byte[]? SenderPublicKey { get; set; }
	}

	public class DataKeyInfo {
		public KeyEncryptionMode Mode { get; set; }
		public byte[] EncryptedKey { get; set; } = new byte[0];
		public byte[]? SenderPublicKey { get; set; }
	}
}
