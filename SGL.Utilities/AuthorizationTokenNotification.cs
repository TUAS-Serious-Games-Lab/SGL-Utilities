using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// The arguments for an event that is triggered after a successful user authentication to allow other components to use the newly obtained session <see cref="AuthorizationToken"/>.
	/// </summary>
	public class UserAuthenticatedEventArgs : EventArgs {
		/// <summary>
		/// The authorization token data for the authenticated user during the current session.
		/// </summary>
		public AuthorizationData Authorization { get; init; }
		/// <summary>
		/// The id of the authenticated user.
		/// </summary>
		public Guid AuthenticatedUserId { get; init; }

	}

	/// <summary>
	/// An empty argument object for an event that is triggered when a request finds the current authorization token to be expired.
	/// </summary>
	public class AuthorizationExpiredEventArgs : EventArgs { }

	/// <summary>
	/// An exception that is thrown when there is a problem with the current authorization token.
	/// </summary>
	public class AuthorizationTokenException : Exception {
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public AuthorizationTokenException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
