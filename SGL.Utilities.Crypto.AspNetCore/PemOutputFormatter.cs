using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.AspNetCore {

	/// <summary>
	/// Provides output formatting for the <c>application/x-pem-file</c> content type.
	/// This formatter can either serve as a simple pass-through formatter, where the object coming from the controller is already a PEM string or an <see cref="IEnumerable{T}"/> of such strings.
	/// Or it can actually format higher-level objects from SGL.Utility.Crypto, by calling their <c>StoreToPem</c> method. This latter mode supports <see cref="Certificate"/>s, <see cref="PublicKey"/>s, and <see cref="IEnumerable{T}"/>s of these types.
	/// Formatting <see cref="PrivateKey"/>s or <see cref="KeyPair"/>s is not supported, as private keys should usually not be served through an API route for security reasons and also there is no reasonable way to provide an encryption password for the formatted response.
	/// </summary>
	public class PemOutputFormatter : TextOutputFormatter {
		private static Type[] supportedTypes = new[] { typeof(string), typeof(IEnumerable<string>), typeof(Certificate), typeof(IEnumerable<Certificate>), typeof(PublicKey), typeof(IEnumerable<PublicKey>) };

		/// <summary>
		/// Initializes a PemOutputFormatter.
		/// </summary>
		public PemOutputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x-pem-file"));
			SupportedEncodings.Add(Encoding.UTF8);
			SupportedEncodings.Add(Encoding.Unicode);
		}

		/// <summary>
		/// Asynchronously writes the <see cref="OutputFormatterCanWriteContext.Object"/> from <paramref name="context"/> to the response body of <see cref="OutputFormatterCanWriteContext.HttpContext"/> in <paramref name="context"/> as (a) PEM object(s) in the encoding specified by <paramref name="selectedEncoding"/>.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <param name="selectedEncoding">The text encoding to use.</param>
		/// <returns>A task object representing the asynchronous operation.</returns>
		public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding) {
			var ct = context.HttpContext.RequestAborted;
			var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<PemOutputFormatter>>();
			await using var directWriter = new StreamWriter(context.HttpContext.Response.Body, selectedEncoding);
			await using var buffer = new MemoryStream();
			await using var bufferWriter = new StreamWriter(buffer, selectedEncoding, leaveOpen: true);
			switch (context.Object) {
				case string strvalue:
					await directWriter.WriteLineAsync(strvalue.AsMemory(), ct);
					return;
				case IEnumerable<string> strvalues:
					foreach (var value in strvalues) {
						await directWriter.WriteLineAsync(value.AsMemory(), ct);
						await directWriter.WriteLineAsync(ReadOnlyMemory<char>.Empty, ct);
					}
					return;
				case Certificate certVal:
					certVal.StoreToPem(bufferWriter);
					break;
				case IEnumerable<Certificate> certVals:
					foreach (var certVal in certVals) {
						certVal.StoreToPem(bufferWriter);
						await bufferWriter.WriteLineAsync(ReadOnlyMemory<char>.Empty, ct);
					}
					break;
				case PublicKey pubKeyVal:
					pubKeyVal.StoreToPem(bufferWriter);
					break;
				case IEnumerable<PublicKey> pubKeyVals:
					foreach (var pubKeyVal in pubKeyVals) {
						pubKeyVal.StoreToPem(bufferWriter);
						await bufferWriter.WriteLineAsync(ReadOnlyMemory<char>.Empty, ct);
					}
					break;
				default:
					logger.LogError("Attempt to write unsupported object type '{type}'.", context.ObjectType?.FullName ?? "null");
					throw new ArgumentException("Unsupported object type", nameof(context));
			}
			await bufferWriter.FlushAsync();
			bufferWriter.Close();
			buffer.Position = 0;
			await buffer.CopyToAsync(context.HttpContext.Response.Body, ct);
		}

		/// <summary>
		/// Returns a bool indicating whether the given type can be formatted by this formatter.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>A bool indicating whether the given type can be formatted by this formatter.</returns>
		protected override bool CanWriteType(Type type) => supportedTypes.Any(t => t.IsAssignableFrom(type));
	}
}
