using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Security {
	/// <summary>
	/// Provides the <see cref="UseJwtLoginService(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class JwtLoginServiceExtensions {
		/// <summary>
		/// Adds the <see cref="JwtLoginService"/> as the implementation for <see cref="ILoginService"/>, along with its configuration in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root configuration obejct to obtain configuration options from.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseJwtLoginService(this IServiceCollection services, IConfiguration config) {
			services.Configure<JwtOptions>(config.GetSection(JwtOptions.Jwt));
			services.AddScoped<ILoginService, JwtLoginService>();
			return services;
		}
	}

	/// <summary>
	/// An implementation of <see cref="ILoginService"/> that issues JWT bearer tokens upon successful authentication.
	/// </summary>
	public class JwtLoginService : ILoginService {
		private ILogger<JwtLoginService> logger;
		private JwtOptions options;
		private SecurityKey signingKey;
		private SigningCredentials signingCredentials;

		/// <summary>
		/// Instantiates a <see cref="JwtLoginService"/> with the given logger and configuration options.
		/// </summary>
		public JwtLoginService(ILogger<JwtLoginService> logger, IOptions<JwtOptions> options) {
			this.logger = logger;
			this.options = options.Value;
			if (this.options.SymmetricKey != null) {
				signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SymmetricKey));
				signingCredentials = new SigningCredentials(signingKey, this.options.LoginService.SigningAlgorithm);
			}
			else {
				throw new InvalidOperationException("No signing key given for JwtOptions.");
			}
		}

		/// <summary>
		/// Starts a fixed delay timer with the duration configured in <see cref="JwtLoginServiceOptions.FailureDelay"/> and with the given cancellation token.
		/// </summary>
		/// <returns>An <see cref="ILoginService.IDelayHandle"/> encapsulating the wait timer.</returns>
		public ILoginService.IDelayHandle StartFixedFailureDelay(CancellationToken ct = default) {
			// On failure, always wait for this fixed delay starting here.
			// This should mitigate timing attacks for detecting whether the failure is
			// due to non-existent user or due to incorrect secret.
			// This also slows down brute-force attacks.
			// The task is created here and passed into LoginAsync to optionally allow callers
			// to capture it and await it to also delay related failures outside the login service's responsibility.
			return new ILoginService.DelayHandle(Task.Delay(options.LoginService.FailureDelay, ct));
		}

		/// <summary>
		/// Asynchronously performs a login attemp with the given credentials, using the given delegates to access the user management as described in <see cref="ILoginService"/>,
		/// and upon success, issues a JWT bearer token with a <c>userid</c> claim for the user's id, as well as the additional claims specified in <c>additionalClaims</c>.
		/// </summary>
		/// <returns>A task representing the operation with a result of <see langword="null"/> for failed attempts and with a string containing a JWT bearer authorization token for a successful login.</returns>
		public async Task<string?> LoginAsync<TUserId, TUser>(TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			ILoginService.IDelayHandle fixedFailureDelay, CancellationToken ct = default,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {

			bool secretCorrect = false;
			bool rehashed = false;
			TUser? user;
			string? hashedSecret = null;
			try {
				if (userId == null) {
					logger.LogError("Login failed because no userId was given.");
					await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
					return null;
				}
				user = await lookupUserAsync(userId).ConfigureAwait(false);
				if (user == null) {
					logger.LogError("Login failed because the user with id {userId} was not found.", userId);
					await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
					return null;
				}
				hashedSecret = getHashedSecret(user);
				(secretCorrect, rehashed) = SecretHashing.VerifyHashedSecret(ref hashedSecret, providedPlainSecret);
				if (!secretCorrect) {
					logger.LogError("Login failed because the given secret didn't match the hashed secret of the given user with id {userId}.", userId);
					await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
					return null;
				}
			}
			catch (Exception ex) {
				logger.LogError(ex, "Login failed due to unexpected exception.", userId);
				await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
				return null;
			}
			if (rehashed) {
				await updateHashedSecretAsync(user, hashedSecret).ConfigureAwait(false);
			}
			var claims = additionalClaims.Select(cg => new Claim(cg.ClaimType, cg.GetClaimValue(user)))
				.Prepend(new Claim("userid", userId.ToString() ?? "")).ToArray();
			var token = new JwtSecurityToken(
				issuer: options.Issuer,
				audience: options.Audience,
				claims: claims,
				expires: DateTime.UtcNow.Add(options.LoginService.ExpirationTime),
				signingCredentials: signingCredentials
			);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			logger.LogInformation("Login succeeded for user {userId}.", userId);
			return tokenString;
		}
	}
}
