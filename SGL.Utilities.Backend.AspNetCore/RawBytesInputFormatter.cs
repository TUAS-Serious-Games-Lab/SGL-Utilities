using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Povides support for reading the raw request body into a model of byte array type.
	/// Besides an actual byte array, the model type can also be any type to which a byte array can be assigned.
	/// Thus, models declared as, e.g. <see cref="IEnumerable{T}"/>s of bytes are also supported.
	/// </summary>
	public class RawBytesInputFormatter : InputFormatter {
		/// <summary>
		/// Returns a bool indicating whether the model type of <paramref name="context"/> can be formatted by this formatter.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <returns>A bool indicating whether the model type can be formatted by this formatter.</returns>
		public override bool CanRead(InputFormatterContext context) {
			return context.ModelType.IsAssignableFrom(typeof(byte[]));
		}

		/// <summary>
		/// Asynchronously reads a byte array value from the request body of <paramref name="context"/> and provides it as the model value.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <returns>
		/// A task object representing the asynchronous operation, wrapping the following:
		/// <list type="bullet">
		/// <item><term><see cref="InputFormatterResult.Success(object)"/></term><description>
		/// If the request body was successfully read. Contains the value read from the body.
		/// </description></item>
		/// <item><term><see cref="InputFormatterResult.Failure"/></term><description>Otherwise, i.e. when the body could not be read successfully.</description></item>
		/// </list>
		/// </returns>
		public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context) {
			try {
				using var buffer = new MemoryStream();
				await context.HttpContext.Request.Body.CopyToAsync(buffer, context.HttpContext.RequestAborted);
				return InputFormatterResult.Success(buffer.ToArray());
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RawBytesInputFormatter>>();
				logger.LogError(ex, "Reading request body failed due to exception.");
				return InputFormatterResult.Failure();
			}
		}
	}
}
