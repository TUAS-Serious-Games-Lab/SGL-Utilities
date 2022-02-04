using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using System.Linq;

namespace SGL.Utilities.Crypto {
	internal class EcdhKdfHelper {
		public static ParametersWithIV DeriveKeyAndIV(byte[] agreement, byte[] senderPublicKey) {
			const int keyLength = 32;
			const int ivLength = 7;
			var kdf = new Kdf2BytesGenerator(new Sha256Digest());
			kdf.Init(new KdfParameters(agreement, senderPublicKey));
			var keyAndIV = new byte[keyLength + ivLength];
			kdf.GenerateBytes(keyAndIV, 0, keyAndIV.Length);
			return new ParametersWithIV(new KeyParameter(keyAndIV.Take(keyLength).ToArray()), keyAndIV.Skip(keyLength).ToArray());
		}
	}
}
