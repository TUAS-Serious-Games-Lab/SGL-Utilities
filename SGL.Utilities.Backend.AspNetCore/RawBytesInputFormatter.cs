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
	public class RawBytesInputFormatter : InputFormatter {
		public override bool CanRead(InputFormatterContext context) {
			return context.ModelType.IsAssignableFrom(typeof(byte[]));
		}

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
