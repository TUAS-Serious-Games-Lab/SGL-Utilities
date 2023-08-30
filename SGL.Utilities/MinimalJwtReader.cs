using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a simple implementation of JWT reading that avoids dependencies on JwtSecurityTokenHandler or similar.
	/// Instead, it only uses <see cref="Convert.FromBase64String"/>, standard string methods, <c>System.Text.Json</c> and <see cref="ObjectDictionaryJsonConverter"/>.
	/// </summary>
	public class MinimalJwtReader {
		/// <summary>
		/// The key for the registered issuer claim.
		/// </summary>
		public const string IssuerKey = "iss";
		/// <summary>
		/// The key for the registered subject claim.
		/// </summary>
		public const string SubjectKey = "sub";
		/// <summary>
		/// The key for the registered audience claim.
		/// </summary>
		public const string AudienceKey = "aud";
		/// <summary>
		/// The key for the registered expiration time claim.
		/// </summary>
		public const string ExpirationTimeKey = "exp";
		/// <summary>
		/// The key for the registered not before claim.
		/// </summary>
		public const string NotBeforeKey = "nbf";
		/// <summary>
		/// The key for the registered issued at claim.
		/// </summary>
		public const string IssuedAtKey = "iat";
		/// <summary>
		/// The key for the registered JWT ID claim.
		/// </summary>
		public const string JwtIdKey = "jti";

		private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
			Converters = { new ObjectDictionaryJsonConverter() }
		};
		/// <summary>
		/// Reads the payload claims from a JWT authorization token as a JSON-style dictionary with string keys and
		/// values of types that match the contained tokens, i.e. numbers are represented as their appropriate type (int, long, or double),
		/// strings that contain are parseable as a <see cref="DateTime"/> are represented as a <see cref="DateTime"/>,
		/// strings that contain are parseable as a <see cref="Guid"/> are represented as a <see cref="Guid"/>,
		/// other strings are represented as-is, and boolean tokens are represented as a bool.
		/// </summary>
		/// <param name="encodedJwt">The encoded JWT string to read.</param>
		/// <returns>A dictionary mapping the payload claims from the token.</returns>
		public static Dictionary<string, object?> ReadJwtPayload(string encodedJwt) {
			var components = encodedJwt.Split('.');
			if (components.Length != 3) {
				throw new ArgumentException("The given string is not in the expected format for JWT.", nameof(encodedJwt));
			}
			return ReadJson(DecodeBase64Url(components[1]));
		}

		private static byte[] DecodeBase64Url(string base64Url) {
			// quick and dirty Base64Url decode after: https://stackoverflow.com/a/26354677
			var base64Payload = base64Url.Replace('_', '/').Replace('-', '+');
			switch (base64Payload.Length % 4) {
				case 2: base64Payload += "=="; break;
				case 3: base64Payload += "="; break;
			}
			// Now we should have normal Base64, leave error handling to FromBase64String:
			return Convert.FromBase64String(base64Payload);
		}

		private static Dictionary<string, object?> ReadJson(byte[] utf8Data) {
			return JsonSerializer.Deserialize<Dictionary<string, object?>>(utf8Data.AsSpan(), jsonOptions) ??
				throw new JsonException("Read unexpected null value.");
		}

		/// <summary>
		/// Takes a value from a JWT claim dictionary as returned by <see cref="ReadJwtPayload(string)"/>
		/// where the using code expects a <see cref="DateTime"/> value and interprets various date/time representations
		/// as a <see cref="DateTime"/>, most importantly including Unix timestamps.
		/// </summary>
		/// <param name="dictValue">The value taken from the dictionary as returned by <see cref="ReadJwtPayload(string)"/>.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException">When the value was null.</exception>
		/// <exception cref="ArgumentException">When the value could not be interpreted as a <see cref="DateTime"/>.</exception>
		public static DateTime ReadDateTimeValue(object? dictValue) => dictValue switch {
			DateTime dateTime => dateTime,
			int unixTimeInt => DateTime.UnixEpoch.AddSeconds(unixTimeInt),
			long unixTimeLong => DateTime.UnixEpoch.AddSeconds(unixTimeLong),
			null => throw new ArgumentNullException(nameof(dictValue)),
			string s when DateTime.TryParse(s, out var dateTimeFromString) => dateTimeFromString,
			_ => throw new ArgumentException("Value expected to be a date/time is not a valid date/time representation.", nameof(dictValue))
		};
	}
}
