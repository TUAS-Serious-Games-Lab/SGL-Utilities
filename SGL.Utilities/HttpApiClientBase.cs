using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides common functionality for REST HTTP API clients.
	/// </summary>
	public class HttpApiClientBase : IApiClient {
		/// <summary>
		/// The underlying <see cref="HttpClient"/> to use for requests.
		/// </summary>
		protected HttpClient HttpClient { get; }

		/// <inheritdoc/>
		public AuthorizationData? Authorization { get; set; }

		/// <inheritdoc/>
		public event AsyncEventHandler<AuthorizationExpiredEventArgs>? AuthorizationExpired;

		/// <summary>
		/// The URI path under which this client operates with <see cref="SendRequest"/>.
		/// It can be relative to the <see cref="HttpClient.BaseAddress"/> of <see cref="HttpClient"/>.
		/// The relative path in <see cref="SendRequest"/> is appended under this.
		/// </summary>
		protected string PrefixUriPath { get; }

		/// <summary>
		/// Constructs the object with the given <see cref="HttpClient"/>, initializing <see cref="Authorization"/> with the given token.
		/// </summary>
		public HttpApiClientBase(HttpClient httpClient, AuthorizationData? authorization, string prefixUriPath) {
			HttpClient = httpClient;
			Authorization = authorization;
			PrefixUriPath = prefixUriPath;
		}

		/// <summary>
		/// Gets an <see cref="AuthenticationHeaderValue"/> object from <see cref="Authorization"/> for a request.
		/// If <see cref="Authorization"/> is expired, <see cref="AuthorizationExpired"/> is triggered to allow refreshing the token.
		/// </summary>
		/// <returns>The <see cref="AuthenticationHeaderValue"/> for the request.</returns>
		/// <exception cref="AuthorizationTokenException">If <see cref="Authorization"/> is null or is expired and <see cref="AuthorizationExpired"/> didn't provide a remediation.</exception>
		protected async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync() {
			if (!Authorization.HasValue) {
				throw new AuthorizationTokenException("No authenticated session.");
			}
			if (!Authorization.Value.Valid) {
				await (AuthorizationExpired?.InvokeAllAsync(this, new AuthorizationExpiredEventArgs { }) ?? Task.CompletedTask);
			}
			if (!Authorization.Value.Valid) {
				throw new AuthorizationTokenException("Authorization token expired.");
			}
			return Authorization.Value.Token.ToHttpHeaderValue();
		}

		/// <summary>
		/// Asynchronously sends a request with the specified parameters to the backend and return the response.
		/// </summary>
		/// <param name="httpMethod">The HTTP method / verb of the request.</param>
		/// <param name="relativeUriPath">The path of the request, relative to <see cref="PrefixUriPath"/>.</param>
		/// <param name="requestContent">The request body content, or null if the request should have not body.</param>
		/// <param name="prepareRequest">A delegate to apply to the request before sending, e.g. to set additional headers.</param>
		/// <param name="accept">The mediatype for the Accept header to set, or null if no Accept header should be set or it is set through <paramref name="prepareRequest"/>.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> that allows cancelling the operation.</param>
		/// <param name="authenticated">If true, the request will have its Authorization header set to the result of <see cref="GetAuthenticationHeaderAsync"/>.</param>
		/// <param name="statusCodeExceptionMapping">
		/// If true, <see cref="MapExceptionForError(HttpResponseMessage)"/> will be called if the response has <see cref="HttpResponseMessage.IsSuccessStatusCode"/> false.
		/// If false, it is the callers responsibility to check the status code.
		/// </param>
		/// <returns>A task object representing the operation, providing a <see cref="HttpResponseMessage"/> as its result.</returns>
		protected async Task<HttpResponseMessage> SendRequest(HttpMethod httpMethod, string relativeUriPath, HttpContent? requestContent, Action<HttpRequestMessage> prepareRequest,
				MediaTypeWithQualityHeaderValue? accept = null, CancellationToken ct = default, bool authenticated = true, bool statusCodeExceptionMapping = true) {
			var request = new HttpRequestMessage(httpMethod, PrefixUriPath + (PrefixUriPath.EndsWith('/') && relativeUriPath.Length != 0 ? "" : "/") + relativeUriPath);
			if (requestContent != null) {
				request.Content = requestContent;
			}
			if (authenticated) {
				request.Headers.Authorization = await GetAuthenticationHeaderAsync();
			}
			if (accept != null) {
				request.Headers.Accept.Add(accept);
			}
			prepareRequest(request);
			var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
			if (statusCodeExceptionMapping && !response.IsSuccessStatusCode) {
				MapExceptionForError(response);
			}
			return response;
		}

		/// <summary>
		/// Called by <see cref="SendRequest"/> if it is insructed to handle errors and a response has <see cref="HttpResponseMessage.IsSuccessStatusCode"/> false.
		/// Can be overriden to customize error handling.
		/// The default calls <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.
		/// </summary>
		/// <param name="response">The response with a non-success error code.</param>
		protected virtual void MapExceptionForError(HttpResponseMessage response) {
			response.EnsureSuccessStatusCode();
		}
	}
}
