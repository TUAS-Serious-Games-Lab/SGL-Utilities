using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.AspNetCore {
	/// <summary>
	/// Provides the <see cref="AddPemFormatters(MvcOptions)"/> extension method.
	/// </summary>
	public static class MvcOptionsPemFormatterExtensions {
		/// <summary>
		/// Adds ASP.Net Core formatters (input and output) for the <c>application/x-pem-file</c> content type.
		/// </summary>
		/// <param name="options">The <see cref="MvcOptions"/> to add the formatters to.</param>
		/// <returns>A reference to <paramref name="options"/> for chaining.</returns>
		public static MvcOptions AddPemFormatters(this MvcOptions options) {
			options.InputFormatters.Insert(0, new PemInputFormatter());
			options.OutputFormatters.Insert(0, new PemOutputFormatter());
			return options;
		}
	}
}
