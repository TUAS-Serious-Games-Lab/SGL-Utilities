using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	public class MultipartSectionModelBinder : IModelBinder {
		private readonly IList<IInputFormatter> formatters;
		private readonly IHttpRequestStreamReaderFactory readerFactory;

		public MultipartSectionModelBinder(IOptions<MvcOptions> options, IHttpRequestStreamReaderFactory readerFactory) {
			this.formatters = options.Value.InputFormatters;
			this.readerFactory = readerFactory;
		}

		public async Task BindModelAsync(ModelBindingContext bindingContext) {
			const int boundaryLengthLimit = 100;
			var request = bindingContext.HttpContext.Request;
			var logger = bindingContext.HttpContext.RequestServices.GetRequiredService<ILogger<MultipartSectionModelBinder>>();
			var ct = bindingContext.HttpContext.RequestAborted;
			request.EnableBuffering();
			request.Body.Position = 0;
			string modelName = bindingContext.ModelName;
			if (string.IsNullOrEmpty(modelName)) {
				modelName = bindingContext.OriginalModelName;
			}
			try {
				if (string.IsNullOrEmpty(request.ContentType) || !request.ContentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase)) {
					bindingContext.ModelState.AddModelError(modelName, "The request is not a multipart request but the model expects a multipart section.");
				}
				var parsedContentType = MediaTypeHeaderValue.Parse(request.ContentType);
				var boundary = HeaderUtilities.RemoveQuotes(parsedContentType.Boundary).Value;
				if (string.IsNullOrEmpty(boundary)) {
					bindingContext.ModelState.AddModelError(modelName, "No multipart boundary found in content type.");
					return;
				}
				if (boundary.Length > boundaryLengthLimit) {
					bindingContext.ModelState.AddModelError(modelName, "Multipart boundary too long.");
					return;
				}
				var reader = new MultipartReader(boundary, request.Body);
				MultipartSection? section = null;
				while ((section = await reader.ReadNextSectionAsync(ct)) != null) {
					if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition)) {
						if (contentDisposition != null && contentDisposition.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase)) {
							await BindSectionForModelAsync(bindingContext, modelName, section, contentDisposition);
							return;
						}
					}
				}
			}
			catch (Exception ex) {
				logger.LogError(ex, "Model binding using multipart section failed due to exception.");
			}
			finally {
				request.Body.Position = 0;
			}
		}

		private async Task BindSectionForModelAsync(ModelBindingContext bindingContext, string modelName, MultipartSection section, ContentDispositionHeaderValue contentDisposition) {
			var httpContext = new MultipartSectionHttpContext(bindingContext.HttpContext, section);
			var formatterContext = new InputFormatterContext(httpContext, modelName,
				bindingContext.ModelState, bindingContext.ModelMetadata, readerFactory.CreateReader, treatEmptyInputAsDefaultValue: false);

			var formatter = formatters.FirstOrDefault(f => f.CanRead(formatterContext));
			if (formatter == null) {
				bindingContext.ModelState.AddModelError(modelName,
					new UnsupportedContentTypeException($"The multipart section '{contentDisposition.Name}' has unsupported Content-Type."), bindingContext.ModelMetadata);
				return;
			}

			var result = await formatter.ReadAsync(formatterContext);

			if (result.HasError) {
				return;
			}
			if (result.IsModelSet) {
				bindingContext.Result = ModelBindingResult.Success(result.Model);
				return;
			}
			else {
				bindingContext.ModelState.AddModelError(modelName, $"The multipart section '{contentDisposition.Name}' contained no valid model value.");
				return;
			}
		}
	}


	public class FromMultipartSectionAttribute : ModelBinderAttribute {
		public FromMultipartSectionAttribute() : base(typeof(MultipartSectionModelBinder)) { }
	}
}
