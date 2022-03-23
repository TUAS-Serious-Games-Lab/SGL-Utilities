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
	/// <summary>
	/// A helper utility to simplify handling a streaming upload request with multiple multipart request body sections.
	/// In the constructor, the request is taken in, the content type and boundary are parsed and a <see cref="MultipartReader"/> is constructed.
	/// Failures during the construction are reported using callbacks and if one of them is triggered, <see cref="InitError"/> is set after the constructor has finished.
	/// Thus, <see cref="InitError"/> needs to be checked after creation, as it being non-null means the <see cref="MultipartReader"/> could not be constructed and the request should be aborted with the indicated error result.
	/// After successful initialization, the other method can be used to find the relevant sections in the request body and to skip unexpected / unneeded sections.
	/// For this helper to be able to read through the request body, it must not be consumed by another component. Thus, the controller action method using this class needs to be marked with <see cref="DisableFormValueModelBindingAttribute"/>.
	/// </summary>
	public class MultipartStreamingHelper {
		/// <summary>
		/// The boundary string specified in the request's content type that is used to mark the separation between the sections.
		/// </summary>
		public string Boundary { get; }
		private MultipartReader reader = null!;
		/// <summary>
		/// The current section, or null if the current state is not inside a section. The latter is the case when no section was read yet or when the last section was read.
		/// </summary>
		public MultipartSection? Section { get; private set; } = null;
		private ContentDispositionHeaderValue? contentDisposition = null;
		/// <summary>
		/// The the parsed content disposition of the current section, or null if the current section does not have a valid content disposition or if the current state is not inside a section. The latter is the case when no section was read yet or when the last section was read.
		/// </summary>
		public ContentDispositionHeaderValue? ContentDisposition => contentDisposition;
		/// <summary>
		/// If non-null, indicates that an error happened during initialization.
		/// The <see cref="ActionResult"/> contains the error result returned by the triggered error callback.
		/// If the result is present, the request should be terminated, returning the result object.
		/// </summary>
		public ActionResult? InitError { get; } = null;

		/// <summary>
		/// Allows changing (or retrieving) the callback that is invoked when a section is skipped because its name and / or content type was not expected.
		/// Changing this after initialization, allows using code to, e.g. change logging messages depending on the phase of the request processing.
		/// </summary>
		public Action<string, string?> SkippedUnexpectedSectionNameContentTypeCallback { get; set; }

		/// <summary>
		/// Allows changing (or retrieving) the callback that is invoked when a section is skipped because it had no valid content disposition.
		/// Changing this after initialization, allows using code to, e.g. change logging messages depending on the phase of the request processing.
		/// </summary>
		public Action<string?> SkippedSectionWithoutValidContentDispositionCallback { get; set; }

		/// <summary>
		/// Initializes the <see cref="MultipartStreamingHelper"/>, parsing the content type header and the boundary contained in it.
		/// Errors during this initialization are handled using the callbacks that return <see cref="ActionResult"/>s.
		/// These can be used for handling like logging and should return an appropriate result object to terminate the request handling with.
		/// If an error was triggered, the result object returned by the callback is provided in <see cref="InitError"/>.
		/// </summary>
		/// <param name="request">The request that is being handled.</param>
		/// <param name="invalidContentTypeCallback">
		/// A callback that is invoked when the content type of <paramref name="request"/> is not a valid multipart content type.
		/// It should return an appropriate error result object to use when erroring out of the request.
		/// The string parameter given is the actual content type in the request.
		/// </param>
		/// <param name="noBoundaryCallback">
		/// A callback that is invoked when the content type of <paramref name="request"/> does not contain a valid boundary.
		/// It should return an appropriate error result object to use when erroring out of the request.
		/// </param>
		/// <param name="boundaryTooLongCallback">
		/// A callback that is invoked when the boundary specified in the content type of <paramref name="request"/> is longer than <paramref name="boundaryLengthLimit"/>.
		/// It should return an appropriate error result object to use when erroring out of the request.
		/// </param>
		/// <param name="skippedUnexpectedSectionNameContentTypeCallback">
		/// An optional callback that is invoked during normal operation (i.e. after construction), when a section with an unexpected name and / or content type is skipped. It is intended for logging.
		/// The callback given here is stored in <see cref="SkippedUnexpectedSectionNameContentTypeCallback"/>. If the callback is not given, <see cref="SkippedUnexpectedSectionNameContentTypeCallback"/> is set to a no-op callback.
		/// </param>
		/// <param name="skippedSectionWithoutValidContentDispositionCallback">
		/// An optional callback that is invoked during normal operation (i.e. after construction), when a section without a valid content disposition is skipped. It is intended for logging.
		/// The callback given here is stored in <see cref="SkippedSectionWithoutValidContentDispositionCallback"/>. If the callback is not given, <see cref="SkippedSectionWithoutValidContentDispositionCallback"/> is set to a no-op callback.
		/// </param>
		/// <param name="boundaryLengthLimit">The maximum allowed length for the parsed <see cref="Boundary"/>.</param>
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

		private bool matchesSelector(string? name, string? contentTypePrefix) =>
			// false if we we aren't in a section with a valid content disposition:
			Section != null && contentDisposition != null &&
			// if name is given, must match name:
			(name == null || contentDisposition.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) &&
			// if contentType is given, must match contentType:
			(contentTypePrefix == null || (Section.ContentType?.StartsWith(contentTypePrefix, StringComparison.OrdinalIgnoreCase) ?? false));

		/// <summary>
		/// Asynchronously reads through the sections of the request, until the current <see cref="Section"/> satisfies any of the given <paramref name="selectors"/>.
		/// If the next section read doesn't satisfy the <paramref name="selectors"/> and thus one or more section(s) are skipped,
		/// the <see cref="SkippedUnexpectedSectionNameContentTypeCallback"/> is invoked for each skipped section with their actual name and content type.
		/// If a section doesn't have a valid content disposition, it is skipped and <see cref="SkippedSectionWithoutValidContentDispositionCallback"/> is invoked with its content type.
		/// </summary>
		/// <param name="ct">A <see cref="CancellationToken"/> that allows cancelling the operation.</param>
		/// <param name="selectors">
		/// A collection of selectors that are used to select the next expected section.
		/// Each selector is represented as a tuple of two nullable <see cref="string"/>s, where the first one represents a section <c>name</c> and the second one represents a <c>contentTypePrefix</c>.
		/// For a section to qualify as a valid section to exit this method, it must satisfy at least one of the selectors.
		/// A selector is satisfied all these conditions are met:
		/// <list type="bullet">
		/// <item><term><c>name</c></term><description>is either null, or matches the name specified in the section's content disposition (case-insensitive).</description></item>
		/// <item><term><c>contentTypePrefix</c></term><description>is either null, or the content type of the section starts with this string (case-insensitive).</description></item>
		/// </list>
		/// Thus, the non-null values in the selectors must match the section's values.
		/// Therefore, to read any section, a <c>(null,null)</c> selector must be given.
		/// </param>
		/// <returns>
		/// Returns a task that represents the operation, wrapping a <see cref="bool"/> result with the following meaning:
		/// <list type="bullet">
		/// <item><term><see langword="true"/></term><description>
		/// The beginning of a section, satisfying the <paramref name="selectors"/> was read and is available in <see cref="Section"/>
		/// (and its parsed content disposition is available in <see cref="ContentDisposition"/>).
		/// </description></item>
		/// <item><term><see langword="false"/></term><description>
		/// None of the sections that were left in the request body satisfied the <paramref name="selectors"/> and the reader is now at the end of the request body and has consumed it.
		/// This usually means that the request must fail beacuse not all expected multipart sections were present, unless the remaining sections that were specified in the <paramref name="selectors"/> are optional.
		/// </description></item>
		/// </list>
		/// </returns>
		/// <exception cref="ArgumentException">If no selectors are given.</exception>
		/// <remarks>
		/// After reading until a section matching ANY of the selectors is current, use <see cref="IsCurrentSection(string?, string?)"/> to check WHICH selector was matched and thus how to handle the current section.
		///
		/// The pattern here is to read-until with the yet expected sections, and then check for the actually read section kind using a <see cref="IsCurrentSection(string?, string?)"/> for each yet expected section.
		/// After handling the section, the cycle repeats with the (potentially fewer) remaining section kinds.
		/// </remarks>
		public async Task<bool> ReadUntilSection(CancellationToken ct, params (string? name, string? contentTypePrefix)[] selectors) {
			if (selectors.Length == 0) {
				throw new ArgumentException("No selectors given.", nameof(selectors));
			}
			Section = await reader.ReadNextSectionAsync(ct);
			while (Section != null) {
				if (ContentDispositionHeaderValue.TryParse(Section.ContentDisposition, out contentDisposition)) {
					if (selectors.Any(s => matchesSelector(s.name, s.contentTypePrefix))) {
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

		/// <summary>
		/// Checks if the current section is a section that satisfies the given selector values.
		/// Like with the selector list used for reading, if a parameter is null it implicitly matches.
		/// Thus, to only check for the <paramref name="name"/>, <paramref name="contentTypePrefix"/> can simply be null and vice-versa.
		/// If both parameters are null, this method just checks if there is a current section at all (with a valid content disposition).
		/// Both checks are also done case insensitively as is the case with the selectors when reading.
		/// </summary>
		/// <param name="name">The name to match with the one specified in the current section's content disposition.</param>
		/// <param name="contentTypePrefix">The prefix for which to check the content type of the current section.</param>
		/// <returns>Returns a <see cref="bool"/> indicating whether the current section satisfies the given selector values.</returns>
		public bool IsCurrentSection(string? name, string? contentTypePrefix) => matchesSelector(name, contentTypePrefix);
	}
}
