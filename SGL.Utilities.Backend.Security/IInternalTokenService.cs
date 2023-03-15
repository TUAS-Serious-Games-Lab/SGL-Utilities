using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Security {
	public interface IInternalTokenService {
		AuthorizationData ObtainInternalServiceAuthenticationToken();
	}
}
