using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Provides support for reading a request body of context type <c>text/plain</c> into a model of type <see cref="string"/>.
	/// </summary>
	public class PlainTextInputFormatter : TextInputFormatter {
		/// <summary>
		/// Initializes the formatter.
		/// </summary>
		public PlainTextInputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/plain"));
			SupportedEncodings.Add(Encoding.UTF8);
			SupportedEncodings.Add(Encoding.Unicode);
			SupportedEncodings.Add(Encoding.ASCII);
		}

		/// <summary>
		/// Asynchronously reads a <see cref="string"/> model value from the request body of <paramref name="context"/>.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <param name="encoding">The text encoding to use.</param>
		/// <returns>
		/// A task object representing the asynchronous operation, wrapping the following:
		/// <list type="bullet">
		/// <item><term><see cref="InputFormatterResult.Success(object)"/></term><description>
		/// If the requested object was successfully read. Contains the value read from the body.
		/// </description></item>
		/// <item><term><see cref="InputFormatterResult.Failure"/></term><description>Otherwise, i.e. when the body contained invalid data.</description></item>
		/// </list>
		/// </returns>
		public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding) {
			try {
				using var reader = context.ReaderFactory(context.HttpContext.Request.Body, encoding);
				var content = await reader.ReadToEndAsync();
				return InputFormatterResult.Success(content);
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<PlainTextInputFormatter>>();
				logger.LogError(ex, "Reading request body failed due to exception.");
				return InputFormatterResult.Failure();
			}
		}

		/// <summary>
		/// Returns a bool indicating whether the given type can be formatted by this formatter.
		/// As this formatter only handles <see cref="string"/>s, this method checks whether <paramref name="type"/> is <c>typeof(string)</c>.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>A bool indicating whether the given type can be formatted by this formatter.</returns>
		protected override bool CanReadType(Type type) {
			return type == typeof(string);
		}
	}
}
