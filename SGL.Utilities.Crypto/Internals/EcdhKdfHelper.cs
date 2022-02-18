using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Linq;

namespace SGL.Utilities.Crypto.Internals {
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

		public static byte[] EncodeEcPublicKey(ECPublicKeyParameters pubKey) =>
			// Encoding as described here: https://stackoverflow.com/a/19614887
			SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pubKey).GetEncoded();

		public static ECPublicKeyParameters DecodeEcPublicKey(byte[] pubKeyEncoded) =>
			// Decoding as described here: https://stackoverflow.com/a/19614887
			(ECPublicKeyParameters)PublicKeyFactory.CreateKey(pubKeyEncoded);
	}
}
