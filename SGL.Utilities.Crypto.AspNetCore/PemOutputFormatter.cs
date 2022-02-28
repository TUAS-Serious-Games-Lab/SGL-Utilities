using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
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
	public class PemOutputFormatter : TextOutputFormatter {
		private static Type[] supportedTypes = new[] { typeof(string), typeof(IEnumerable<string>), typeof(Certificate), typeof(IEnumerable<Certificate>), typeof(PublicKey), typeof(IEnumerable<PublicKey>) };

		public PemOutputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x-pem-file"));
			SupportedEncodings.Add(Encoding.UTF8);
			SupportedEncodings.Add(Encoding.Unicode);
		}

		public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding) {
			await using var directWriter = new StreamWriter(context.HttpContext.Response.Body, selectedEncoding);
			await using var buffer = new MemoryStream();
			await using var bufferWriter = new StreamWriter(buffer, selectedEncoding, leaveOpen: true);
			switch (context.Object) {
				case string strvalue:
					await directWriter.WriteLineAsync(strvalue);
					return;
				case IEnumerable<string> strvalues:
					foreach (var value in strvalues) {
						await directWriter.WriteLineAsync(value);
						await directWriter.WriteLineAsync();
					}
					return;
				case Certificate certVal:
					certVal.StoreToPem(bufferWriter);
					break;
				case IEnumerable<Certificate> certVals:
					foreach (var certVal in certVals) {
						certVal.StoreToPem(bufferWriter);
						await bufferWriter.WriteLineAsync();
					}
					break;
				case PublicKey pubKeyVal:
					pubKeyVal.StoreToPem(bufferWriter);
					break;
				case IEnumerable<PublicKey> pubKeyVals:
					foreach (var pubKeyVal in pubKeyVals) {
						pubKeyVal.StoreToPem(bufferWriter);
						await bufferWriter.WriteLineAsync();
					}
					break;
				default:
					throw new ArgumentException("Unsupported Object type", nameof(context));
			}
			await bufferWriter.FlushAsync();
			bufferWriter.Close();
			buffer.Position = 0;
			await buffer.CopyToAsync(context.HttpContext.Response.Body);
		}

		protected override bool CanWriteType(Type type) => supportedTypes.Any(t => t.IsAssignableFrom(type));
	}
}
