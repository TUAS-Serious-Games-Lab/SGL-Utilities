using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Utilities.Crypto.Keys {

	/// <summary>
	/// Represents a SGL.Utilities.Crypto-specific identifier of key pairs, based on their public key.
	/// </summary>
	/// <remarks>
	/// The id has a prefix (the first byte) that indicates the type of the key pair. The rest of the id is comprised of the SHA256 hash of the significant part of the public key as listed below:
	/// <list type="table">
	/// <listheader>
	/// <term>Prefix</term><term>Key Type</term><term>Hashed Key Component</term>
	/// </listheader>
	/// <item><term><c>01</c></term><term>RSA</term><term>The modulus</term></item>
	/// <item><term><c>02</c></term><term>Elliptic Curves</term><term>The key's point on the curve, in uncompressed encoding.</term></item>
	/// </list>
	/// </remarks>
	[JsonConverter(typeof(KeyIdJsonConverter))]
	public class KeyId {
		private byte[] id = new byte[1] { 0 }; // First byte indicates type (0 = empty, 1 = RSA, 2 = EC), remaining 32 bytes = SHA256 fingerprint

		/// <summary>
		/// Constructs a KeyId that represents the given id.
		/// </summary>
		/// <param name="id">The raw id bytes.</param>
		/// <exception cref="ArgumentException">If the given id isn't structurally valid.</exception>
		public KeyId(byte[] id) {
			if (id.Length != 33) throw new ArgumentException("The given id has an invalid format.", nameof(id));
			if (id[0] is not (1 or 2)) throw new ArgumentException("The given id doesn't have a valid type.", nameof(id));
			this.id = id;
		}
		/// <summary>
		/// Returns a copy of the raw id bytes.
		/// </summary>
		public byte[] Id => (byte[])id.Clone();

		/// <summary>
		/// Tests this <see cref="KeyId"/> object and <paramref name="obj"/> for value equality, i.e. if both objects represent the same key id.
		/// </summary>
		public override bool Equals(object? obj) {
			if (obj is KeyId k) {
				return id.SequenceEqual(k.id);
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// Calculates a hash code from the value of the <see cref="KeyId"/> object, i.e. from the represented id.
		/// </summary>
		public override int GetHashCode() {
			int hc = 0;
			const int rot = 5;
			foreach (var b in id) {
				hc = (hc << rot | hc >> 32 - rot) ^ b;
			}
			return hc;
		}

		/// <summary>
		/// Tests <paramref name="left"/> and <paramref name="right"/> for value equality, i.e. if both objects represent the same key id.
		/// </summary>
		public static bool operator ==(KeyId left, KeyId right) => left.Equals(right);
		/// <summary>
		/// Tests <paramref name="left"/> and <paramref name="right"/> for value inequality, i.e. if both objects represent the different key ids.
		/// </summary>
		public static bool operator !=(KeyId left, KeyId right) => !left.Equals(right);

		/// <summary>
		/// Formats the key id in a human-friendly, yet easily parsable format, where the bytes are printed in hexadecimal,
		/// with <c>:</c> separators between the prefix and the hash, as well as between each group of 8 hex digits of the hash.
		/// </summary>
		/// <returns>The formatted <see cref="KeyId"/></returns>
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

		/// <summary>
		/// Parses a string representation as produced by <see cref="ToString"/> back into a <see cref="KeyId"/> object.
		/// </summary>
		/// <param name="str">A string representation of a key if in the format produced by <see cref="ToString"/></param>
		/// <returns>The parsed <see cref="KeyId"/> object, representing the same value.</returns>
		/// <exception cref="ArgumentException">When the given string is not a representation of a valid string id.
		/// This includes invalid characters, incorrect length, incorrect number of separators, and unknown key type prefixes.</exception>
		public static KeyId Parse(string str) {
			var otherChars = str.Where(c => !Uri.IsHexDigit(c));
			if (otherChars.Any(c => c != ':')) throw new ArgumentException("Invalid characters in KeyId.");
			if (otherChars.Count() != 8) throw new ArgumentException("Incorrect number of separators in KeyId");
			var digits = str.Where(c => Uri.IsHexDigit(c)).ToArray();
			if (digits.Length != 33 /*bytes in id*/ * 2 /*hex per byte*/) throw new ArgumentException("Incorrect number of characters for KeyId.");
			var id = Convert.FromHexString(digits);
			if (id[0] < 1 || id[0] > 2) throw new ArgumentException("Given KeyId uses unknown type identifier.");
			return new KeyId(id);
		}
	}

	/// <summary>
	/// Implements serialization and deserialization of <see cref="KeyId"/> objects to / from JSON.
	/// </summary>
	public class KeyIdJsonConverter : JsonConverter<KeyId> {
		/// <inheritdoc/>
		public override KeyId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var str = reader.GetString();
			return str != null ? KeyId.Parse(str) : null;
		}

#if NET6_0_OR_GREATER
		/// <inheritdoc/>
		public override KeyId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			var str = reader.GetString();
			if (str == null) throw new NotSupportedException("Null values are not allowed as dictionary keys.");
			return KeyId.Parse(str);
		}
#endif

		/// <inheritdoc/>
		public override void Write(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

#if NET6_0_OR_GREATER
		/// <inheritdoc/>
		public override void WriteAsPropertyName(Utf8JsonWriter writer, KeyId value, JsonSerializerOptions options) => writer.WritePropertyName(value.ToString()!);
#endif
	}

	/// <summary>
	/// Implements serialization and deserialization of <see cref="Dictionary{TKey, TValue}"/> objects that use <see cref="KeyId"/> objects as keys to / from JSON.
	/// This is provided for backward compatibility to .Net prior to 6.0, where <see cref="JsonConverter{T}"/> lacks <c>ReadAsPropertyName</c> and <c>WriteAsPropertyName</c>,
	/// preventing the use of the built-in <see cref="Dictionary{TKey, TValue}"/> serializer with <see cref="KeyId"/> keys.
	/// </summary>
	public class KeyIdDictionaryJsonConverter<Value> : JsonConverter<Dictionary<KeyId, Value>> {
		/// <inheritdoc/>
		public override Dictionary<KeyId, Value>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Unexpected JSON token.");
			var dict = new Dictionary<KeyId, Value>();
			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) break;
				string keyStr = reader.GetString() ?? throw new JsonException("Couldn't read JSON property name.");
				reader.Read();
				var key = KeyId.Parse(keyStr);
				var value = JsonSerializer.Deserialize<Value>(ref reader, options);
				if (value == null) {
					throw new JsonException($"Couldn't reed value for key '{keyStr}'.");
				}
				dict.Add(key, value);
			}
			if (reader.TokenType == JsonTokenType.EndObject) {
				return dict;
			}
			else {
				throw new JsonException("Unexpected end of JSON object.");
			}
		}

		/// <inheritdoc/>
		public override void Write(Utf8JsonWriter writer, Dictionary<KeyId, Value> value, JsonSerializerOptions options) {
			writer.WriteStartObject();
			foreach (var kv in value) {
				var keyIdStr = kv.Key.ToString();
				if (keyIdStr == null) throw new NotSupportedException("Null values are not allowed as dictionary keys.");
				writer.WritePropertyName(keyIdStr);
				JsonSerializer.Serialize(writer, kv.Value, options);
			}
			writer.WriteEndObject();
		}
	}
}
