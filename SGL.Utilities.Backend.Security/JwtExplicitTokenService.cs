using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Security {
	/// <summary>
	/// Provides the <see cref="UseJwtExplicitTokenService(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class JwtExplicitTokenServiceExtensions {
		/// <summary>
		/// Adds the <see cref="JwtExplicitTokenService"/> as the implementation for <see cref="IExplicitTokenService"/>, along with its configuration in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root configuration obejct to obtain configuration options from.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseJwtExplicitTokenService(this IServiceCollection services, IConfiguration config) {
			services.Configure<JwtOptions>(config.GetSection(JwtOptions.Jwt));
			services.AddScoped<IExplicitTokenService, JwtExplicitTokenService>();
			return services;
		}
	}


	/// <summary>
	/// An implementation of <see cref="IExplicitTokenService"/> that issues JWT bearer tokens.
	/// </summary>
	public class JwtExplicitTokenService : IExplicitTokenService {
		private readonly ILogger<JwtExplicitTokenService> logger;
		private readonly JwtOptions options;
		private readonly SecurityKey signingKey;
		private readonly SigningCredentials signingCredentials;
		private readonly JwtSecurityTokenHandler jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

		/// <summary>
		/// Initializes the service using the given logger, config options, and claims to be issued.
		/// </summary>
		public JwtExplicitTokenService(ILogger<JwtExplicitTokenService> logger, IOptions<JwtOptions> options) {
			this.logger = logger;
			this.options = options.Value;

			if (this.options.SymmetricKey != null) {
				signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SymmetricKey));
				signingCredentials = new SigningCredentials(signingKey, this.options.Explicit.SigningAlgorithm ?? this.options.LoginService.SigningAlgorithm);
				logger.LogTrace("Instantiated JwtExplicitTokenService using symmetric key.");
			}
			else {
				throw new InvalidOperationException("No signing key given for JwtOptions.");
			}
		}

		/// <summary>
		/// Issues a JWT bearer authentication token using the <see cref="JwtOptions"/> specified at construction, containing the given claims.
		/// </summary>
		/// <param name="claims">Claims to put into the issued token.</param>
		/// <returns>The authentication token, wrapped in an <see cref="AuthorizationData"/> struct.</returns>
		public AuthorizationData IssueAuthenticationToken(params (string Type, string Value)[] claims) {
			var claimObjets = claims.Select(c => new Claim(c.Type, c.Value)).ToArray();
			var expirationTime = DateTime.UtcNow.Add(options.Explicit.ExpirationTime ?? options.LoginService.ExpirationTime);
			var token = new JwtSecurityToken(
				issuer: options.Issuer,
				audience: options.Audience,
				claims: claimObjets,
				expires: expirationTime,
				signingCredentials: signingCredentials
			);
			var authToken = new AuthorizationToken(AuthorizationTokenScheme.Bearer, jwtSecurityTokenHandler.WriteToken(token));
			if (logger.IsEnabled(LogLevel.Trace)) {
				logger.LogTrace("Issuing authentication token with claims {claims}, valid until {expiry}", "{" + string.Join("; ", claimObjets.Select(c => $"{c.Type}={c.Value}")) + "}", expirationTime);
			}
			return new AuthorizationData(authToken, expirationTime);
		}
	}
}
