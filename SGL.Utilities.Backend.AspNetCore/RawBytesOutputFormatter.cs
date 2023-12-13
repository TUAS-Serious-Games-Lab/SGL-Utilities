using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Provides support for writing model values that are <see cref="IEnumerable{T}"/>s of bytes to the response body as raw byte sequences.
	/// </summary>
	public class RawBytesOutputFormatter : OutputFormatter {
		/// <summary>
		/// The size of the batches in which the model value is enumerated and transformed into the output buffer.
		/// This does not apply if the model value is a plain byte array, as those are written directly.
		/// </summary>
		public int BatchSize { get; init; } = 1024 * 1024;

		/// <summary>
		/// Returns a bool indicating whether the model type of <paramref name="context"/> can be formatted by this formatter.
		/// As this formatter only supports <see cref="IEnumerable{T}"/>s of bytes, this method simply checks if the model type is a type that implements <see cref="IEnumerable{T}"/> of bytes.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <returns>A bool indicating whether the model type of <paramref name="context"/> can be formatted by this formatter.</returns>
		public override bool CanWriteResult(OutputFormatterCanWriteContext context) {
			return context.ObjectType == typeof(byte[]) && (context.ObjectType?.IsAssignableTo(typeof(IEnumerable<byte>)) ?? false);
		}

		/// <summary>
		/// Asynchronously writes the <see cref="OutputFormatterCanWriteContext.Object"/> from <paramref name="context"/> to the response body of <see cref="OutputFormatterCanWriteContext.HttpContext"/> in <paramref name="context"/> as a raw byte sequence.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <returns>A task object representing the asynchronous operation.</returns>
		public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context) {
			try {
				var ct = context.HttpContext.RequestAborted;
				var body = context.HttpContext.Response.Body;

				if (context.Object is not IEnumerable<byte> value) {
					var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RawBytesOutputFormatter>>();
					logger.LogError("The value to write was not a valid IEnumerable<byte>.");
					throw new ArgumentException("The Object to write was not a valid IEnumerable<byte>.", nameof(context));
				}

				if (value is byte[] arr) {
					await body.WriteAsync(arr.AsMemory(), ct);
				}
				else {
					foreach (var buffer in value.AsArrayBatches(BatchSize)) {
						await body.WriteAsync(buffer.AsMemory(), ct);
					}
				}
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RawBytesOutputFormatter>>();
				logger.LogError(ex, "Writing response body failed due to exception.");
			}
		}
	}
}
