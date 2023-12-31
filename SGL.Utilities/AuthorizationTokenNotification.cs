﻿using System;
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
#if NETSTANDARD
		public AuthorizationData Authorization { get; }
#else
		public AuthorizationData Authorization { get; init; }
#endif
		/// <summary>
		/// The id of the authenticated user.
		/// </summary>
#if NETSTANDARD
		public Guid AuthenticatedUserId { get; }
#else
		public Guid AuthenticatedUserId { get; init; }
#endif
		/// <summary>
		/// Creates an <see cref="UserAuthenticatedEventArgs"/> with the given <see cref="AuthorizationData"/> and user id.
		/// </summary>
		public UserAuthenticatedEventArgs(AuthorizationData authorization, Guid authenticatedUserId) {
			Authorization = authorization;
			AuthenticatedUserId = authenticatedUserId;
		}
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
