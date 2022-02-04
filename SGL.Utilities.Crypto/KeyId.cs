using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Utilities.Crypto {
	[JsonConverter(typeof(KeyIdJsonConverter))]
	public class KeyId {
		private byte[] id = new byte[1] { 0 }; // First byte indicates type (0 = empty, 1 = RSA, 2 = EC), remaining 32 bytes = SHA256 fingerprint
		public static KeyId CalculateId(AsymmetricKeyParameter publicKey) {
			switch (publicKey) {
				case null:
					throw new ArgumentNullException(nameof(publicKey));
				case RsaKeyParameters rsa:
					return new KeyId() { id = getKeyId(rsa) };
				case ECPublicKeyParameters ec:
					return new KeyId() { id = getKeyId(ec) };
				default:
					throw new ArgumentException($"Unsupported key type {publicKey.GetType().FullName}.");
			}
		}

		private static byte[] getKeyId(ECPublicKeyParameters ec) {
			var digest = new Sha256Digest();
			var keyBytes = ec.Q.GetEncoded(compressed: false); // TODO: Recheck, if this is deterministic
			digest.BlockUpdate(keyBytes, 0, keyBytes.Length);
			byte[] result = new byte[33];
			digest.DoFinal(result, 1);
			result[0] = 2;
			return result;
		}

		private static byte[] getKeyId(RsaKeyParameters rsa) {
			var digest = new Sha256Digest();
			var modulusBytes = rsa.Modulus.ToByteArrayUnsigned();
			digest.BlockUpdate(modulusBytes, 0, modulusBytes.Length);
			byte[] result = new byte[33];
			digest.DoFinal(result, 1);
			result[0] = 1;
			return result;
		}

		public override bool Equals(object? obj) {
			if (obj is KeyId k) {
				return id.SequenceEqual(k.id);
			}
			else {
				return false;
			}
		}

		public override int GetHashCode() {
			int hc = 0;
			const int rot = 5;
			foreach (var b in id) {
				hc = (hc << rot | hc >> 32 - rot) ^ b;
			}
			return hc;
		}

		public override string? ToString() {
			StringBuilder sb = new StringBuilder(33 /*bytes in id*/ * 2 /*hex per byte*/ + 8 /*separators*/);
			sb.AppendFormat("{0:X2}", id.First());
			var remainder = id.Skip(1);
			while (remainder.Count() > 0) {
				sb.AppendFormat(":{0:X2}{1:X2}{2:X2}{3:X2}", remainder.ElementAt(0), remainder.ElementAt(1), remainder.ElementAt(2), remainder.ElementAt(3));
				remainder = remainder.Skip(4);
			}
			return sb.ToString();
		}

		public static KeyId Parse(string str) {
			var otherChars = str.Where(c => !Uri.IsHexDigit(c));
			if (otherChars.Any(c => c != ':')) throw new ArgumentException("Invalid characters in KeyId.");
			if (otherChars.Count() != 8) throw new ArgumentException("Incorrect number of separators in KeyId");
			var digits = str.Where(c => Uri.IsHexDigit(c)).ToArray();
			if (digits.Length != 33 /*bytes in id*/ * 2 /*hex per byte*/) throw new ArgumentException("Incorrect number of characters for KeyId.");
			var id = Convert.FromHexString(digits);
			if (id[0] < 1 || id[0] > 2) throw new ArgumentException("Given KeyId uses unknown type identifier.");
			return new KeyId() { id = id };
		}
	}

	public class KeyIdJsonConverter : JsonConverter<KeyId> {
		public override KeyId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var str = reader.GetString();
			return str != null ? KeyId.Parse(str) : null;
		}

#if NET6_0_OR_GREATER
		public override KeyId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var str = reader.GetString();
			if (str == null) throw new Exception("Got null value for KeyId used in dictionary key.");
			return KeyId.Parse(str);
		}
#endif

		public override void Write(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options) {
			writer.WriteStringValue(value.ToString());
		}

#if NET6_0_OR_GREATER
		public override void WriteAsPropertyName(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options) {
			writer.WritePropertyName(value.ToString()!);
		}
#endif
	}
}
