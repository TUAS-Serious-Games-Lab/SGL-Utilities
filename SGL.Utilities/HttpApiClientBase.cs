using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides common functionality for REST HTTP API clients.
	/// </summary>
	public class HttpApiClientBase : IApiClient, IDisposable {
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

		private AsyncSemaphoreLock authorizationLock = new AsyncSemaphoreLock();

		/// <summary>
		/// Constructs the object with the given <see cref="HttpClient"/>, initializing <see cref="Authorization"/> with the given token.
		/// </summary>
		public HttpApiClientBase(HttpClient httpClient, AuthorizationData? authorization, string prefixUriPath) {
			HttpClient = httpClient;
			Authorization = authorization;
			PrefixUriPath = prefixUriPath;
		}

		/// <inheritdoc/>
		public void Dispose() {
			authorizationLock.Dispose();
		}

		/// <summary>
		/// Updates <see cref="Authorization"/> under an asynchronous lock to ensure safe access if the property is used concurrently between multiple operations.
		/// This method asynchronously waits to acquire the lock, updates the variable and then immediately drops the lock.
		/// </summary>
		/// <param name="value">The new value to assign to <see cref="Authorization"/>.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> that allows cancelling the waiting for the lock.</param>
		/// <returns>A task object representing the operation.</returns>
		public async Task SetAuthorizationLockedAsync(AuthorizationData? value, CancellationToken ct = default) {
			using (var lockHandle = await authorizationLock.WaitAsyncWithScopedRelease(ct)) {
				Authorization = value;
			}
		}

		/// <summary>
		/// Gets an <see cref="AuthenticationHeaderValue"/> object from <see cref="Authorization"/> for a request.
		/// If <see cref="Authorization"/> is expired, <see cref="AuthorizationExpired"/> is triggered to allow refreshing the token.
		/// </summary>
		/// <returns>The <see cref="AuthenticationHeaderValue"/> for the request.</returns>
		/// <exception cref="AuthorizationTokenException">If <see cref="Authorization"/> is null or is expired and <see cref="AuthorizationExpired"/> didn't provide a remediation.</exception>
		protected async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken ct = default) {
			using var lockHandle = await authorizationLock.WaitAsyncWithScopedRelease(ct);
			if (!Authorization.HasValue) {
				throw new AuthorizationTokenException("No authenticated session.");
			}
			if (!Authorization.Value.Valid) {
				await (AuthorizationExpired?.InvokeAllAsync(this, new AuthorizationExpiredEventArgs { }, ct) ?? Task.CompletedTask);
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
		protected Task<HttpResponseMessage> SendRequest(HttpMethod httpMethod, string relativeUriPath, HttpContent? requestContent, Action<HttpRequestMessage> prepareRequest,
				MediaTypeWithQualityHeaderValue? accept = null, CancellationToken ct = default, bool authenticated = true, bool statusCodeExceptionMapping = true) {
			return SendRequest(httpMethod, relativeUriPath, Enumerable.Empty<KeyValuePair<string, string>>(), requestContent, prepareRequest, accept, ct, authenticated, statusCodeExceptionMapping);
		}
		/// <summary>
		/// Asynchronously sends a request with the specified parameters to the backend and return the response.
		/// </summary>
		/// <param name="httpMethod">The HTTP method / verb of the request.</param>
		/// <param name="relativeUriPath">
		/// The path of the request, relative to <see cref="PrefixUriPath"/>,
		/// or an absolute URL if <see cref="PrefixUriPath"/> is empty.
		/// </param>
		/// <param name="queryParameters">The query parameters to append to the request URI as key-value-pairs.</param>
		/// <param name="requestContent">The request body content, or null if the request should have not body.</param>
		/// <param name="prepareRequest">A delegate to apply to the request before sending, e.g. to set additional headers.</param>
		/// <param name="accept">The mediatype for the Accept header to set, or null if no Accept header should be set or it is set through <paramref name="prepareRequest"/>.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> that allows cancelling the operation.</param>
		/// <param name="authenticated">If true, the request will have its Authorization header set to the result of <see cref="GetAuthenticationHeaderAsync"/>.</param>
		/// <param name="statusCodeExceptionMapping">
		/// If true, <see cref="MapExceptionForError(HttpRequestMessage, HttpResponseMessage)"/> will be called if the response has <see cref="HttpResponseMessage.IsSuccessStatusCode"/> false.
		/// If false, it is the callers responsibility to check the status code.
		/// </param>
		/// <returns>A task object representing the operation, providing a <see cref="HttpResponseMessage"/> as its result.</returns>
		protected async Task<HttpResponseMessage> SendRequest(HttpMethod httpMethod, string relativeUriPath, IEnumerable<KeyValuePair<string, string>> queryParameters, HttpContent? requestContent, Action<HttpRequestMessage> prepareRequest,
				MediaTypeWithQualityHeaderValue? accept = null, CancellationToken ct = default, bool authenticated = true, bool statusCodeExceptionMapping = true) {
			string requestUriPath = PrefixUriPath +
				((!string.IsNullOrWhiteSpace(PrefixUriPath) && !PrefixUriPath.EndsWith('/') && relativeUriPath.Length != 0) ? "/" : "") + relativeUriPath;
			requestUriPath += buildQueryString(queryParameters);
			using var request = new HttpRequestMessage(httpMethod, requestUriPath);
			if (requestContent != null) {
				request.Content = requestContent;
			}
			if (authenticated) {
				request.Headers.Authorization = await GetAuthenticationHeaderAsync(ct);
			}
			if (accept != null) {
				request.Headers.Accept.Add(accept);
			}
			prepareRequest(request);
			HttpResponseMessage response;
			try {
				response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
			}
			catch (HttpRequestException ex) {
				throw MapExceptionForError(ex, request);
			}
			catch (Exception) {
				throw;
			}
			await PreprocessResponseAsync(request, response);
			if (statusCodeExceptionMapping && !response.IsSuccessStatusCode) {
				MapExceptionForError(request, response);
			}
			return response;
		}

		/// <summary>
		/// Is called in <see cref="SendRequest"/> when a <paramref name="response"/> was received from the server for a given <paramref name="request"/> 
		/// to allow deriving classes to override preprocessing of the response object.
		/// It is called before status code exception mapping and before returning the response object.
		/// The default implementation calls <see cref="HttpContent.LoadIntoBufferAsync()"/> on the response content for non-success responses.
		/// This is intended to ensure that the response body is read from the socket, even if the body provided in the exception is not touched.
		/// Doing this is relevant on some <see cref="System.Net.Http.HttpClient"/> implementations to avoid clogging the socket, preventing it from being used for further requests.
		/// </summary>
		/// <param name="request">The request object to which the response belongs.</param>
		/// <param name="response">The response object to perform preprocessing on.</param>
		/// <returns>A task representing the asynchronous preprocessing operation.</returns>
		protected virtual async Task PreprocessResponseAsync(HttpRequestMessage request, HttpResponseMessage response) {
			if (!response.IsSuccessStatusCode) {
				await response.Content.LoadIntoBufferAsync();
			}
		}

		private static string buildQueryString(IEnumerable<KeyValuePair<string, string>> queryParameters) {
			string paramsString = string.Join('&', queryParameters.Select(param => string.Format("{0}={1}", Uri.EscapeDataString(param.Key), Uri.EscapeDataString(param.Value))));
			return string.IsNullOrEmpty(paramsString) ? string.Empty : "?" + paramsString;
		}

		/// <summary>
		/// Called by <see cref="SendRequest"/> if it is insructed to handle errors and a response has <see cref="HttpResponseMessage.IsSuccessStatusCode"/> false.
		/// Can be overriden to customize error handling.
		/// The default calls <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> and wraps the thrown exception in <see cref="HttpApiResponseException"/>
		/// with context information from <paramref name="request"/> and <paramref name="response"/>.
		/// </summary>
		/// <param name="request">The request that resulted in the error code response.</param>
		/// <param name="response">The response with a non-success error code.</param>
		protected virtual void MapExceptionForError(HttpRequestMessage request, HttpResponseMessage response) {
			try {
				response.EnsureSuccessStatusCode();
			}
			catch (Exception ex) {
				throw new HttpApiResponseException("Web API request failed due to error response from backend.",
					request.Version, request.Method, request.RequestUri, response!.Version, response!.StatusCode, response, ex);
			}
		}

		/// <summary>
		/// Called by <see cref="SendRequest"/> if the request couldn't be sent to the server. This could be due to network, DNS or TLS issues.
		/// The returned exception is then thrown by <see cref="SendRequest"/> to indicate the error.
		/// The default wraps <paramref name="ex"/> in <see cref="HttpApiRequestFailedException"/> with context information from <paramref name="request"/>.
		/// </summary>
		/// <param name="ex">The exception thrown by <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>.</param>
		/// <param name="request">The request that couldn't be sent.</param>
		/// <returns>The exception to throw.</returns>
		protected virtual Exception MapExceptionForError(HttpRequestException ex, HttpRequestMessage request) {
			return new HttpApiRequestFailedException("Web API request failed due to connectivity problems.", request.Version, request.Method, request.RequestUri, ex);
		}
	}

	/// <summary>
	/// An exception that indicates an error with a web API operation made using <see cref="HttpApiClientBase"/>.
	/// </summary>
	public class HttpApiException : Exception {
		/// <summary>
		/// Constructs an exception object with the given data.
		/// </summary>
		public HttpApiException(string message, Version requestVersion, HttpMethod method, Uri? requestUri, Exception? innerException) : base(message, innerException) {
			RequestVersion = requestVersion;
			Method = method;
			RequestUri = requestUri;
		}

		/// <summary>
		/// The HTTP version with which the request was attempted.
		/// </summary>
		public Version RequestVersion { get; }
		/// <summary>
		/// The HTTP method used by the request.
		/// </summary>
		public HttpMethod Method { get; }
		/// <summary>
		/// The URI of the request.
		/// </summary>
		public Uri? RequestUri { get; }
	}

	/// <summary>
	/// An exception that indicates an error response to a web API request made using <see cref="HttpApiClientBase"/>.
	/// </summary>
	public class HttpApiResponseException : HttpApiException {
		/// <summary>
		/// Constructs an exception object with the given data.
		/// </summary>
		public HttpApiResponseException(string message, Version version, HttpMethod method, Uri? requestUri, Version responseVersion, HttpStatusCode statusCode, HttpResponseMessage? responseObject, Exception? innerException) :
			base(message, version, method, requestUri, innerException) {
			ResponseVersion = responseVersion;
			StatusCode = statusCode;
			ResponseObject = responseObject;
		}
		/// <summary>
		/// The HTTP version used by server for the response.
		/// </summary>
		public Version ResponseVersion { get; }
		/// <summary>
		/// The HTTP status code of the response.
		/// </summary>
		public HttpStatusCode StatusCode { get; }
		/// <summary>
		/// The full response object.
		/// </summary>
		public HttpResponseMessage? ResponseObject { get; }
	}

	/// <summary>
	/// An exception that indicates that a web API request failed, because it could not be transmitted to the server.
	/// This can happen due to network or DNS issues or because the TLS handshake and certificate validations couldn't be completed correctly.
	/// </summary>
	public class HttpApiRequestFailedException : HttpApiException {
		/// <summary>
		/// Constructs an exception object with the given data.
		/// </summary>
		public HttpApiRequestFailedException(string message, Version version, HttpMethod method, Uri? requestUri, Exception? innerException) :
			base(message, version, method, requestUri, innerException) { }
	}
}
