﻿using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	public class RawBytesOutputFormatter : OutputFormatter {
		public int BatchSize { get; init; } = 1024 * 1024;

		public override bool CanWriteResult(OutputFormatterCanWriteContext context) {
			return context.ObjectType == typeof(byte[]) && (context.ObjectType?.IsAssignableTo(typeof(IEnumerable<byte>)) ?? false);
		}

		public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context) {
			try {
				var ct = context.HttpContext.RequestAborted;
				var value = context.Object as IEnumerable<byte>;
				var body = context.HttpContext.Response.Body;

				if (value == null) {
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
