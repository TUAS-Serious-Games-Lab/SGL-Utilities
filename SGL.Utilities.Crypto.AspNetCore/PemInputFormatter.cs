using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
	/// <summary>
	/// Provides input formatting for the <c>application/x-pem-file</c> content type.
	/// This formatter can either serve as a simple pass-through formatter, where the controller takes a PEM string or an <see cref="IEnumerable{T}"/> of such strings.
	/// If the controller takes a single PEM string, the entire request body is provided in the string, after checking that it contains at least one PEM object.
	/// If the controller takes an <see cref="IEnumerable{T}"/> of PEM strings, the request body is split up into string, each of which contains one of the PEM objects in the request body.
	/// In this case, text between the PEM objects is discarded.
	///
	/// Alternatively, this formatter can actually parse higher-level objects from SGL.Utility.Crypto, by calling their <c>TryLoadOneFromPem</c> or <c>LoadAllFromPem</c> methods.
	/// This latter mode supports <see cref="Certificate"/>s, <see cref="PublicKey"/>s, and <see cref="IEnumerable{T}"/>s of these types.
	/// Parsing <see cref="PrivateKey"/>s or <see cref="KeyPair"/>s is not supported, as there is no reasonable way to provide an encryption password for reading the submitted object.
	/// </summary>
	public class PemInputFormatter : TextInputFormatter {
		private static Type[] supportedTypes = new[] { typeof(string), typeof(IEnumerable<string>), typeof(Certificate), typeof(IEnumerable<Certificate>), typeof(PublicKey), typeof(IEnumerable<PublicKey>) };

		/// <summary>
		/// Initializes a PemInputFormatter.
		/// </summary>
		public PemInputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x-pem-file"));
			SupportedEncodings.Add(Encoding.UTF8);
			SupportedEncodings.Add(Encoding.Unicode);
		}

		/// <summary>
		/// Returns a bool indicating whether the given type can be formatted by this formatter.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>A bool indicating whether the given type can be formatted by this formatter.</returns>
		protected override bool CanReadType(Type type) => supportedTypes.Any(t => t.IsAssignableFrom(type));

		private Task<InputFormatterResult> HandleNoValueString(ILogger<PemInputFormatter> logger, InputFormatterContext context, string logMessage) {
			if (context.TreatEmptyInputAsDefaultValue) {
				logger.LogDebug(logMessage);
				return InputFormatterResult.SuccessAsync("");
			}
			else {
				logger.LogError(logMessage);
				return InputFormatterResult.NoValueAsync();
			}
		}

		/// <summary>
		/// Asynchronously reads an object of the type indicated by the <see cref="InputFormatterContext.ModelType"/> of <paramref name="context"/> in PEM format from the body of the HTTP request contained in <paramref name="context"/>.
		/// The given encoding is used to read the characters of the PEM data.
		/// </summary>
		/// <param name="context">The context to operate on.</param>
		/// <param name="encoding">The text encoding to use.</param>
		/// <returns>
		/// <list type="bullet">
		/// <item><term><see cref="InputFormatterResult.Success(object)"/></term><description>
		/// If the requested object was successfully read. Contains the value read from the body.
		/// This is also returned with the default value, if <see cref="InputFormatterContext.TreatEmptyInputAsDefaultValue"/> of <paramref name="context"/> was true.
		/// </description></item>
		/// <item><term><see cref="InputFormatterResult.NoValue"/></term><description>
		/// If the body contained no value and <see cref="InputFormatterContext.TreatEmptyInputAsDefaultValue"/> of <paramref name="context"/> was false.
		/// </description></item>
		/// <item><term><see cref="InputFormatterResult.Failure"/></term><description>Otherwise, i.e. when the body didn't contained invalid data.</description></item>
		/// </list>
		/// </returns>
		public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding) {
			var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<PemInputFormatter>>();
			var type = supportedTypes.FirstOrDefault(t => context.ModelType.IsAssignableFrom(t));
			if (type == null && context.ModelType.IsAssignableTo(typeof(PrivateKey))) {
				logger.LogError("Attempt to read private key, which is unsupported, as the passphrase can not be properly passed. Consume string instead and perform PEM parsing on higher level.");
				return await InputFormatterResult.FailureAsync();
			}
			else if (type == null && context.ModelType.IsAssignableTo(typeof(IEnumerable<PrivateKey>))) {
				logger.LogError("Attempt to read private key list, which is unsupported, as the passphrase can not be properly passed. Consume string instead and perform PEM parsing on higher level.");
				return await InputFormatterResult.FailureAsync();
			}
			else if (type == null) {
				logger.LogError("Attempt to read unsupported object type '{type}'.", context.ModelType?.FullName ?? "null");
				return await InputFormatterResult.FailureAsync();
			}
			else if (type == typeof(string)) {
				using var strReader = context.ReaderFactory(context.HttpContext.Request.Body, encoding);
				var value = await strReader.ReadToEndAsync();
				if (string.IsNullOrWhiteSpace(value)) {
					return await HandleNoValueString(logger, context, "Body contained no PEM data (it was null, empty, or only contained whitespace).");
				}
				var beginPos = value.IndexOf("-----BEGIN", 0);
				var endPos = value.IndexOf("-----END", 0);
				if (beginPos < 0 && endPos < 0) {
					return await HandleNoValueString(logger, context, "Body contained no PEM data (neither a BEGIN nor an END string was found).");
				}
				else if (beginPos < 0) {
					logger.LogError("PEM data in body is missing the BEGIN string.");
					return await InputFormatterResult.FailureAsync();
				}
				else if (endPos < 0) {
					logger.LogError("PEM data in body is missing the END string.");
					return await InputFormatterResult.FailureAsync();
				}
				else if (endPos < beginPos) {
					logger.LogError("PEM data in body has first END string before first BEGIN string.");
					return await InputFormatterResult.FailureAsync();
				}
				else {
					return await InputFormatterResult.SuccessAsync(value);
				}
			}
			else if (type == typeof(IEnumerable<string>)) {
				using var strReader = context.ReaderFactory(context.HttpContext.Request.Body, encoding);
				var values = new List<string>();
				StringBuilder sbBuff = new StringBuilder();
				StringBuilder? sb = null; // The string builder for the current PEM object, if there is one, or null if outside PEM object.
				string? line;
				while ((line = await strReader.ReadLineAsync()) != null) {
					string trimmedLine = line.TrimStart();
					bool beginLine = trimmedLine.StartsWith("-----BEGIN");
					bool endLine = !beginLine && trimmedLine.StartsWith("-----END");
					if (sb == null & beginLine) { // Not current object, found begin of object
						sb = sbBuff.Clear();// Start object with string builder
						sb.AppendLine(line);
					}
					else if (sb != null && endLine) { // Currently in object, found end of it
						sb.AppendLine(line);
						values.Add(sb.ToString());//  Add string with the object to result
						sb = null; // Now outside object
					}
					else if (beginLine) {
						logger.LogError("Body contained BEGIN before matching END for previous BEGIN.");
						return await InputFormatterResult.FailureAsync();
					}
					else if (endLine) {
						logger.LogError("Body contained END before matching BEGIN.");
						return await InputFormatterResult.FailureAsync();
					}
					else if (sb != null) { // data line inside object, just append to current object
						sb.AppendLine(line);
					}
					// else: ignore lines outside BEGIN-END-blocks, i.e. outside PEM object
				}
				return await InputFormatterResult.SuccessAsync(values);
			}
			// Buffer data into memory asynchronously, as the PEM reading methods only support synchronous IO and we want to avoid blocking on IO.
			await using var buffer = new MemoryStream();
			await context.HttpContext.Request.Body.CopyToAsync(buffer);
			buffer.Position = 0;
			using var reader = context.ReaderFactory(buffer, encoding);
			if (type == typeof(Certificate)) {
				try {
					var val = Certificate.TryLoadOneFromPem(reader);
					if (val == null && !context.TreatEmptyInputAsDefaultValue) {
						logger.LogError("The body contained no PEM objects.");
						return await InputFormatterResult.NoValueAsync();
					}
					return await InputFormatterResult.SuccessAsync(val!);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Error while loading certificate from PEM body.");
					return await InputFormatterResult.FailureAsync();
				}
			}
			else if (type == typeof(IEnumerable<Certificate>)) {
				try {
					return await InputFormatterResult.SuccessAsync(Certificate.LoadAllFromPem(reader).ToList());
				}
				catch (Exception ex) {
					logger.LogError(ex, "Error while loading certificates from PEM body.");
					return await InputFormatterResult.FailureAsync();
				}
			}
			else if (type == typeof(PublicKey)) {
				try {
					PublicKey val = PublicKey.LoadOneFromPem(reader);
					if (val == null && !context.TreatEmptyInputAsDefaultValue) {
						logger.LogError("The body contained no PEM objects.");
						return await InputFormatterResult.NoValueAsync();
					}
					return await InputFormatterResult.SuccessAsync(val!);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Error while loading public key from PEM body.");
					return await InputFormatterResult.FailureAsync();
				}
			}
			else if (type == typeof(IEnumerable<PublicKey>)) {
				try {
					return await InputFormatterResult.SuccessAsync(PublicKey.LoadAllFromPem(reader).ToList());
				}
				catch (Exception ex) {
					logger.LogError(ex, "Error while loading public keys from PEM body.");
					return await InputFormatterResult.FailureAsync();
				}
			}
			else {
				logger.LogError("Unexpected type '{type}' requested.", type?.FullName ?? "null");
				return await InputFormatterResult.FailureAsync();
			}
		}
	}
}
