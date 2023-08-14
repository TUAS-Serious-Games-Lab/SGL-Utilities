using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Security {
	/// <summary>
	/// Specifies the interface for services that provide authentication tokens for internal inter-service communication.
	/// </summary>
	public interface IInternalTokenService {
		/// <summary>
		/// Generates an authentication token using the protocol and claims specified in the implementation.
		/// </summary>
		/// <returns>The authentication token, wrapped in an <see cref="AuthorizationData"/> struct.</returns>
		AuthorizationData ObtainInternalServiceAuthenticationToken();
	}
}
