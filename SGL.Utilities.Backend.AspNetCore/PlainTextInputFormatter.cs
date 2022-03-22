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
	public class PlainTextInputFormatter : TextInputFormatter {
		public PlainTextInputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/plain"));
			SupportedEncodings.Add(Encoding.UTF8);
			SupportedEncodings.Add(Encoding.Unicode);
			SupportedEncodings.Add(Encoding.ASCII);
		}

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

		protected override bool CanReadType(Type type) {
			return type == typeof(string);
		}
	}
}
