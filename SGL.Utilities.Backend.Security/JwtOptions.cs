using Microsoft.IdentityModel.Tokens;
using System;

namespace SGL.Utilities.Backend.Security {
	/// <summary>
	/// Encapsulates the configuration options for <see cref="JwtLoginService"/>.
	/// It is split into general JWT options, that are also needed for services consuming the issued tokens, and the <see cref="JwtLoginServiceOptions"/>, only needed for the login service itself.
	/// </summary>
	public class JwtOptions {
		/// <summary>
		/// A constant defining the key path under which the configuration options are located.
		/// Under this key, the options are named as their respective properties.
		/// The top-level key is <c>Jwt</c>.
		/// </summary>
		public const string Jwt = "Jwt";
		/// <summary>
		/// A secret string used as the signing key for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public string? SymmetricKey { get; set; }
		/// <summary>
		/// The issuer identification to use for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public string Issuer { get; set; } = "SGL";
		/// <summary>
		/// The audience identification to use for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public string Audience { get; set; } = "SGL";
		/// <summary>
		/// The configuration options for the <see cref="JwtLoginService"/>.
		/// </summary>
		public JwtLoginServiceOptions LoginService { get; set; } = new JwtLoginServiceOptions();
		/// <summary>
		/// The configuration options for the <see cref="JwtInternalTokenService"/>.
		/// </summary>
		public JwtInternalTokenServiceOptions Internal { get; set; } = new JwtInternalTokenServiceOptions();
		/// <summary>
		/// The configuration options for the <see cref="JwtExplicitTokenService"/>.
		/// </summary>
		public JwtExplicitTokenServiceOptions Explicit { get; set; } = new JwtExplicitTokenServiceOptions();
	}

	/// <summary>
	/// Encapsulates the configuration options for <see cref="JwtInternalTokenService"/> that are only needed on the issuing side.
	/// </summary>
	public class JwtInternalTokenServiceOptions {
		/// <summary>
		/// Specifies the cryptographic signing algorithm to use to sign the issued tokens.
		/// </summary>
		public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256;
		/// <summary>
		/// Specifies the expiration time for the tokens issued by <see cref="JwtInternalTokenService"/>.
		/// </summary>
		public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromHours(1);
	}

	/// <summary>
	/// Encapsulates the configuration options for <see cref="JwtExplicitTokenService"/> that are only needed on the issuing side.
	/// </summary>
	public class JwtExplicitTokenServiceOptions {
		/// <summary>
		/// Specifies the cryptographic signing algorithm to use to sign the issued tokens.
		/// If left empty, <see cref="JwtLoginServiceOptions.SigningAlgorithm"/> is used.
		/// </summary>
		public string? SigningAlgorithm { get; set; } = null;
		/// <summary>
		/// Specifies the expiration time for the tokens issued by <see cref="JwtExplicitTokenService"/>.
		/// If left empty, <see cref="JwtLoginServiceOptions.ExpirationTime"/> is used.
		/// </summary>
		public TimeSpan? ExpirationTime { get; set; } = null;
	}

	/// <summary>
	/// Encapsulates the configuration options for <see cref="JwtLoginService"/> that are only needed on the authentication side.
	/// </summary>
	public class JwtLoginServiceOptions {
		/// <summary>
		/// Specifies the duration of the fixed failure delay time.
		/// </summary>
		public TimeSpan FailureDelay { get; set; } = TimeSpan.FromMilliseconds(1500);
		/// <summary>
		/// Specifies the cryptographic signing algorithm to use to sign the issued tokens.
		/// </summary>
		public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256;
		/// <summary>
		/// Specifies the expiration time for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromDays(1);
	}
}
