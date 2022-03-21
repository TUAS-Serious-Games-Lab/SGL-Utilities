﻿using Microsoft.AspNetCore.Mvc.Formatters;
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
	public class PemInputFormatter : TextInputFormatter {
		private static Type[] supportedTypes = new[] { typeof(string), typeof(IEnumerable<string>), typeof(Certificate), typeof(IEnumerable<Certificate>), typeof(PublicKey), typeof(IEnumerable<PublicKey>) };

		public PemInputFormatter() {
			SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x-pem-file"));
			SupportedEncodings.Add(Encoding.UTF8);
			SupportedEncodings.Add(Encoding.Unicode);
		}

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
