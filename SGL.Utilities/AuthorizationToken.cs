﻿using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace SGL.Utilities {

	/// <summary>
	/// Encapsulates the supported authorization header schemes for token authorization.
	/// </summary>
	public enum AuthorizationTokenScheme {
		/// <summary>
		/// Represents bearer token authentication.
		/// </summary>
		Bearer
	}

	/// <summary>
	/// Encapsulates an authorization token used for authentication and authorization purposes between a user login service and other services being used using the authenticated credentials.
	/// The token is issued to the client by a login service and the client passes it along when calling other services.
	/// </summary>
	public struct AuthorizationToken {
		/// <summary>
		/// The scheme used by this token.
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public AuthorizationTokenScheme Scheme { get; }
		/// <summary>
		/// The actual token value used for authentication and authorization.
		/// </summary>
		public string Value { get; }

		/// <summary>
		/// Constructs an authorization token with the given scheme and token value.
		/// </summary>
		[JsonConstructor]
		public AuthorizationToken(AuthorizationTokenScheme Scheme, string Value) {
			this.Scheme = Scheme;
			this.Value = Value;
		}
		/// <summary>
		/// Constructs an authorization token with the given token value and the default scheme of bearer token authentication.
		/// </summary>
		public AuthorizationToken(string Value) : this(AuthorizationTokenScheme.Bearer, Value) { }

		/// <summary>
		/// Returns an <see cref="AuthenticationHeaderValue"/> with the properties in the object, for use with an <see cref="HttpClient"/>.
		/// </summary>
		public AuthenticationHeaderValue ToHttpHeaderValue() => new AuthenticationHeaderValue(Scheme.ToString(), Value);
		/// <summary>
		/// Returns a string representation of the token scheme and value combination.
		/// </summary>
		public override string? ToString() => $"{Scheme.ToString()} {Value}";
	}
}
