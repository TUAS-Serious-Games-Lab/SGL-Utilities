using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Provides the extension methods <see cref="AddRawBytesFormatters(MvcOptions, int)"/> and <see cref="AddPlainTextInputFormatter(MvcOptions)"/>.
	/// </summary>
	public static class MvcOptionsRawFormattersExtensions {
		/// <summary>
		/// Registers <see cref="RawBytesInputFormatter"/> and <see cref="RawBytesOutputFormatter"/> in <paramref name="options"/>.
		/// </summary>
		/// <param name="options">The options to add the registrations to.</param>
		/// <param name="outputFormatterBatchSize">The <see cref="RawBytesOutputFormatter.BatchSize"/> to set.</param>
		/// <returns>A reference to <paramref name="options"/> for chaining.</returns>
		public static MvcOptions AddRawBytesFormatters(this MvcOptions options, int outputFormatterBatchSize = 1024 * 1024) {
			options.InputFormatters.Insert(0, new RawBytesInputFormatter());
			options.OutputFormatters.Insert(0, new RawBytesOutputFormatter() { BatchSize = outputFormatterBatchSize });
			return options;
		}
		/// <summary>
		/// Registers <see cref="PlainTextInputFormatter"/> in <paramref name="options"/>.
		/// </summary>
		/// <param name="options">The options to add the registration to.</param>
		/// <returns>A reference to <paramref name="options"/> for chaining.</returns>
		public static MvcOptions AddPlainTextInputFormatter(this MvcOptions options) {
			options.InputFormatters.Insert(0, new PlainTextInputFormatter());
			return options;
		}
	}
}
