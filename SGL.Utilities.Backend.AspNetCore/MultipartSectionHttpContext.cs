using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	internal class MultipartSectionHttpRequest : HttpRequest {
		private readonly HttpRequest parent;
		private readonly MultipartSection section;
		private readonly HttpContext httpContext;
		private readonly IHeaderDictionary headers;
		private IFormCollection form = new FormCollection(new());
		long? contentLength = null;
		string contentType;

		public MultipartSectionHttpRequest(HttpRequest parent, MultipartSection section, HttpContext httpContext) {
			this.parent = parent;
			this.section = section;
			this.httpContext = httpContext;
			contentType = section.ContentType ?? "";
			if (section.Headers != null) {
				Dictionary<string, StringValues> headerMergeDict = new Dictionary<string, StringValues>();
				foreach (var entry in parent.Headers) {
					headerMergeDict[entry.Key] = entry.Value;
				}
				foreach (var entry in section.Headers) {
					headerMergeDict[entry.Key] = entry.Value;
				}
				headers = new HeaderDictionary(headerMergeDict);
			}
			else {
				headers = parent.Headers;
			}
		}

		public override HttpContext HttpContext => httpContext;
		public override IHeaderDictionary Headers => headers;
		public override long? ContentLength { get => contentLength; set => contentLength = value; }
		public override string ContentType { get => contentType; set => contentType = value; }
		public override bool HasFormContentType => false;
		public override IFormCollection Form { get => form; set => form = value; }
		public override Stream Body { get => section.Body; set => section.Body = value; }

		public override string Method { get => parent.Method; set => parent.Method = value; }
		public override string Scheme { get => parent.Scheme; set => parent.Scheme = value; }
		public override bool IsHttps { get => parent.IsHttps; set => parent.IsHttps = value; }
		public override HostString Host { get => parent.Host; set => parent.Host = value; }
		public override PathString PathBase { get => parent.PathBase; set => parent.PathBase = value; }
		public override PathString Path { get => parent.Path; set => parent.Path = value; }
		public override QueryString QueryString { get => parent.QueryString; set => parent.QueryString = value; }
		public override IQueryCollection Query { get => parent.Query; set => parent.Query = value; }
		public override string Protocol { get => parent.Protocol; set => parent.Protocol = value; }

		public override IRequestCookieCollection Cookies { get => parent.Cookies; set => parent.Cookies = value; }

		public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default) {
			return parent.ReadFormAsync(cancellationToken);
		}
	}

	internal class MultipartSectionHttpContext : HttpContext {
		private HttpContext parent;
		private HttpRequest request;

		public MultipartSectionHttpContext(HttpContext parent, MultipartSection section) {
			this.parent = parent;
			request = new MultipartSectionHttpRequest(parent.Request, section, this);
		}

		public override IFeatureCollection Features => parent.Features;
		public override HttpRequest Request => request;
		public override HttpResponse Response => parent.Response;
		public override ConnectionInfo Connection => parent.Connection;
		public override WebSocketManager WebSockets => parent.WebSockets;
		public override ClaimsPrincipal User { get => parent.User; set => parent.User = value; }
		public override IDictionary<object, object?> Items { get => parent.Items; set => parent.Items = value; }
		public override IServiceProvider RequestServices { get => parent.RequestServices; set => parent.RequestServices = value; }
		public override CancellationToken RequestAborted { get => parent.RequestAborted; set => parent.RequestAborted = value; }
		public override string TraceIdentifier { get => parent.TraceIdentifier; set => parent.TraceIdentifier = value; }
		public override ISession Session { get => parent.Session; set => parent.Session = value; }
		public override void Abort() => parent.Abort();
	}

}
