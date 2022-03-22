using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	public class MultipartStreamingHelper {
		public string Boundary { get; }
		private MultipartReader reader = null!;
		public MultipartSection? Section { get; private set; } = null;
		private ContentDispositionHeaderValue? contentDisposition = null;
		public ContentDispositionHeaderValue? ContentDisposition => contentDisposition;
		public ActionResult? InitError { get; } = null;

		public Action<string, string?> SkippedUnexpectedSectionNameContentTypeCallback { get; set; }
		public Action<string?> SkippedSectionWithoutValidContentDispositionCallback { get; set; }

		public MultipartStreamingHelper(HttpRequest request, Func<string, ActionResult> invalidContentTypeCallback, Func<ActionResult> noBoundaryCallback,
				Func<ActionResult> boundaryTooLongCallback, Action<string, string?>? skippedUnexpectedSectionNameContentTypeCallback = null,
				Action<string?>? skippedSectionWithoutValidContentDispositionCallback = null, int boundaryLengthLimit = 100) {
			if (string.IsNullOrEmpty(request.ContentType) || !request.ContentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase)) {
				InitError = invalidContentTypeCallback(request.ContentType);
			}
			var parsedContentType = MediaTypeHeaderValue.Parse(request.ContentType);
			Boundary = HeaderUtilities.RemoveQuotes(parsedContentType.Boundary).Value;
			if (string.IsNullOrEmpty(Boundary)) {
				InitError = noBoundaryCallback();
			}
			if (Boundary.Length > boundaryLengthLimit) {
				InitError = boundaryTooLongCallback();
			}

			reader = new MultipartReader(Boundary, request.Body);
			if (skippedUnexpectedSectionNameContentTypeCallback == null) {
				skippedUnexpectedSectionNameContentTypeCallback = (name, contentType) => { };
			}
			SkippedUnexpectedSectionNameContentTypeCallback = skippedUnexpectedSectionNameContentTypeCallback;
			if (skippedSectionWithoutValidContentDispositionCallback == null) {
				skippedSectionWithoutValidContentDispositionCallback = (contentType) => { };
			}
			SkippedSectionWithoutValidContentDispositionCallback = skippedSectionWithoutValidContentDispositionCallback;
		}

		private bool matchesSelector(string? name, string? contentType) =>
			// false if we we aren't in a section with a valid content disposition:
			Section != null && contentDisposition != null &&
			// if name is given, must match name:
			(name == null || contentDisposition.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) &&
			// if contentType is given, must match contentType:
			(contentType == null || (Section.ContentType?.Equals(contentType, StringComparison.OrdinalIgnoreCase) ?? false));

		public async Task<bool> SkipUntilSection(CancellationToken ct, params (string? name, string? contentType)[] selectors) {
			Section = await reader.ReadNextSectionAsync(ct);
			while (Section != null) {
				if (ContentDispositionHeaderValue.TryParse(Section.ContentDisposition, out contentDisposition)) {
					if (selectors.Any(s => matchesSelector(s.name, s.contentType))) {
						return true;
					}
					SkippedUnexpectedSectionNameContentTypeCallback(contentDisposition.Name.ToString(), Section.ContentType);
				}
				else {
					SkippedSectionWithoutValidContentDispositionCallback(Section.ContentType);
				}
				Section = await reader.ReadNextSectionAsync(ct);
			}
			return false;
		}

		public bool IsCurrentSection(string? name, string? contentType) => matchesSelector(name, contentType);
	}
}
