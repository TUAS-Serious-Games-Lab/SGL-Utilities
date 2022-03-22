using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	public static class MvcOptionsRawFormattersExtensions {
		public static MvcOptions AddRawBytesFormatters(this MvcOptions options, int outputFormatterBatchSize = 1024 * 1024) {
			options.InputFormatters.Insert(0, new RawBytesInputFormatter());
			options.OutputFormatters.Insert(0, new RawBytesOutputFormatter() { BatchSize = outputFormatterBatchSize });
			return options;
		}
		public static MvcOptions AddPlainTextInputFormatter(this MvcOptions options) {
			options.InputFormatters.Insert(0, new PlainTextInputFormatter());
			return options;
		}
	}
}
