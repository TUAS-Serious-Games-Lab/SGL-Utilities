using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Security {
	/// <summary>
	/// Specifies the interface for services that provide authentication tokens for other components that
	/// need to explicitly issue such tokens after performing their own authentication checks.
	/// </summary>
	public interface IExplicitTokenService {
		/// <summary>
		/// Generates an authentication token using the protocol of the implementation, containing the provided claims.
		/// </summary>
		/// <param name="claims">Claims to put into the issued token.</param>
		/// <returns>The authentication token, wrapped in an <see cref="AuthorizationData"/> struct.</returns>
		AuthorizationData IssueAuthenticationToken(params (string Type, string Value)[] claims) => IssueAuthenticationToken(null, claims);
		/// <summary>
		/// Generates an authentication token using the protocol of the implementation, containing the provided claims.
		/// </summary>
		/// <param name="latestExpirationTime">
		/// Timestamp until which the token shall be valid at the longest.
		/// If provided, the actual expiration time of the token is the earlier timestamp out of this and <see cref="JwtExplicitTokenServiceOptions.ExpirationTime"/> after time of issuing.
		/// </param>
		/// <param name="claims">Claims to put into the issued token.</param>
		/// <returns>The authentication token, wrapped in an <see cref="AuthorizationData"/> struct.</returns>
		AuthorizationData IssueAuthenticationToken(DateTime? latestExpirationTime, params (string Type, string Value)[] claims);
	}
}
