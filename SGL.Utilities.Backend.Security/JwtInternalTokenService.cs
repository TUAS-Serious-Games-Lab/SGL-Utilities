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

#pragma warning disable CS0419 // Ambiguous reference in cref attribute
	/// <summary>
	/// Provides the <see cref="UseJwtInternalTokenService"/> extension method.
	/// </summary>
	public static class JwtInternalTokenServiceExtensions {
#pragma warning restore CS0419 // Ambiguous reference in cref attribute
		/// <summary>
		/// Adds the <see cref="JwtInternalTokenService"/> as the implementation for <see cref="IInternalTokenService"/>, along with its configuration in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root configuration obejct to obtain configuration options from.</param>
		/// <param name="claims">The claims to issue in requested tokens.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseJwtInternalTokenService(this IServiceCollection services, IConfiguration config, params (string ClaimType, Func<string> GetClaimValue)[] claims) {
			services.Configure<JwtOptions>(config.GetSection(JwtOptions.Jwt));
			services.AddScoped<IInternalTokenService, JwtInternalTokenService>(svc =>
				new JwtInternalTokenService(svc.GetRequiredService<ILogger<JwtInternalTokenService>>(),
											svc.GetRequiredService<IOptions<JwtOptions>>(),
											claims));
			return services;
		}
		/// <summary>
		/// Adds the <see cref="JwtInternalTokenService"/> as the implementation for <see cref="IInternalTokenService"/>, along with its configuration in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root configuration obejct to obtain configuration options from.</param>
		/// <param name="serviceClaim">The service name to issue as the "service" claim.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseJwtInternalTokenService(this IServiceCollection services, IConfiguration config, string serviceClaim) =>
			services.UseJwtInternalTokenService(config, ("service", () => serviceClaim));

	}

	/// <summary>
	/// An implementation of <see cref="IInternalTokenService"/> that issues JWT bearer tokens for internal services.
	/// </summary>
	public class JwtInternalTokenService : IInternalTokenService {
		private readonly ILogger<JwtInternalTokenService> logger;
		private readonly JwtOptions options;
		private readonly (string ClaimType, Func<string> GetClaimValue)[] claims;

		private readonly SecurityKey signingKey;
		private readonly SigningCredentials signingCredentials;
		private readonly JwtSecurityTokenHandler jwtSecurityTokenHandler = new JwtSecurityTokenHandler();

		/// <summary>
		/// Initializes the service using the given logger, config options, and claims to be issued.
		/// </summary>
		public JwtInternalTokenService(ILogger<JwtInternalTokenService> logger, IOptions<JwtOptions> options, (string ClaimType, Func<string> GetClaimValue)[] claims) {
			this.logger = logger;
			this.options = options.Value;
			this.claims = claims;

			if (this.options.SymmetricKey != null) {
				signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SymmetricKey));
				signingCredentials = new SigningCredentials(signingKey, this.options.Internal.SigningAlgorithm);
				logger.LogTrace("Instantiated JwtInternalTokenService using symmetric key.");
			}
			else {
				throw new InvalidOperationException("No signing key given for JwtOptions.");
			}
		}

		/// <summary>
		/// Issues a JWT bearer authentication token using the <see cref="JwtOptions"/> specified at construction, containing the claims given at construction.
		/// </summary>
		public AuthorizationData ObtainInternalServiceAuthenticationToken() {
			var claimObjets = claims.Select(cg => new Claim(cg.ClaimType, cg.GetClaimValue())).ToArray();
			var expirationTime = DateTime.UtcNow.Add(options.Internal.ExpirationTime);
			var token = new JwtSecurityToken(
				issuer: options.Issuer,
				audience: options.Audience,
				claims: claimObjets,
				expires: expirationTime,
				signingCredentials: signingCredentials
			);
			var authToken = new AuthorizationToken(AuthorizationTokenScheme.Bearer, jwtSecurityTokenHandler.WriteToken(token));
			if (logger.IsEnabled(LogLevel.Trace)) {
				logger.LogTrace("Issuing internal service token with claims {claims}, valid until {expiry}", "{" + string.Join("; ", claimObjets.Select(c => $"{c.Type}={c.Value}")) + "}", expirationTime);
			}
			return new AuthorizationData(authToken, expirationTime);
		}
	}
}
