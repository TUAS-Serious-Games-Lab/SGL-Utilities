using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Encapsulates an <see cref="AuthorizationToken"/> along with its expiry time.
	/// </summary>
	public readonly struct AuthorizationData {
		/// <summary>
		/// The actual authorization token to use for making API requests.
		/// </summary>
		public AuthorizationToken Token { get; }
		/// <summary>
		/// The time (in UTC) when <see cref="Token"/> expires and needs to be renewed.
		/// </summary>
		public DateTime Expiry { get; }

		/// <summary>
		/// Created a new <see cref="AuthorizationData"/> object with the given values.
		/// </summary>
		/// <param name="token"></param>
		/// <param name="expiry"></param>
		public AuthorizationData(AuthorizationToken token, DateTime expiry) {
			Token = token;
			Expiry = expiry;
		}

		/// <summary>
		/// Returns true if <see cref="Token"/> has not expired yet, and false otherwise.
		/// </summary>
		public bool Valid => DateTime.UtcNow < Expiry;
	}

	/// <summary>
	/// Specifies a general interface for API clients to access the common authorization token aspects.
	/// </summary>
	public interface IApiClient {
		/// <summary>
		/// Gets or sets the authorization token to use for making API requests, combined with its expiry time.
		/// Most clients will have this set at construction or updated later when a token has expired.
		/// Clients that are responsible for the user authentication will update this after the relevant API calls for authentication succeed and usually have it initially empty.
		/// </summary>
		AuthorizationData? Authorization { get; set; }

		/// <summary>
		/// Updates <see cref="Authorization"/> under an asynchronous lock to ensure safe access if it is used concurrently between multiple operations.
		/// </summary>
		/// <param name="value">The new value to assign to <see cref="Authorization"/>.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> that allows cancelling the waiting for the lock.</param>
		/// <returns>A task object representing the operation.</returns>
		Task SetAuthorizationLockedAsync(AuthorizationData? value, CancellationToken ct = default);

		/// <summary>
		/// An event triggered when a request method finds <see cref="Authorization"/> to be expired (or close to expiring).
		/// It allows other components to remedy the expiry by obtaining a new token and updating <see cref="Authorization"/> with it.
		/// If no remediation is made, the original request will fail with an <see cref="AuthorizationTokenException"/>.
		/// </summary>
		event AsyncEventHandler<AuthorizationExpiredEventArgs>? AuthorizationExpired;
	}
}
