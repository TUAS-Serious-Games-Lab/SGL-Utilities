using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
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
	/// <summary>
	/// Implements ASP.Net Core model binding for scenarios where a controller method parameter should consume a whole <c>multipart/formdata</c> request body section in a manner similar to how
	/// parameters can normally consume the request body, e.g. as a JSON object.
	/// The parameter that should consume a request body section need to be marked with <see cref="FromMultipartSectionAttribute"/>.
	/// Additionally, as the <see cref="MultipartSectionModelBinder"/> needs to read through the reuqest body, it is incompatible with the model binders for form values.
	/// Thus these need to be disabled using <see cref="DisableFormValueModelBindingAttribute"/> for the method.
	/// Each parameter is bound to a section with a name that matches the <see cref="ModelBinderAttribute.Name"/> of its <see cref="FromMultipartSectionAttribute"/>, if that is set.
	/// Otherwise, the name of the parameter itself is used. In both cases the match is done case-insensitive.
	/// Obtaining the value from the body content is done using the <see cref="IInputFormatter"/>s registered in <see cref="MvcOptions"/>.
	/// </summary>
	public class MultipartSectionModelBinder : IModelBinder {
		private readonly IList<IInputFormatter> formatters;
		private readonly IHttpRequestStreamReaderFactory readerFactory;

		/// <summary>
		/// Initializes the model binder, injecting the taken dependencies.
		/// </summary>
		public MultipartSectionModelBinder(IOptions<MvcOptions> options, IHttpRequestStreamReaderFactory readerFactory) {
			this.formatters = options.Value.InputFormatters;
			this.readerFactory = readerFactory;
		}

		/// <summary>
		/// Asynchronously attempts to bind the model to the matching section of the request body.
		/// </summary>
		public async Task BindModelAsync(ModelBindingContext bindingContext) {
			const int boundaryLengthLimit = 100;
			var request = bindingContext.HttpContext.Request;
			var logger = bindingContext.HttpContext.RequestServices.GetRequiredService<ILogger<MultipartSectionModelBinder>>();
			var ct = bindingContext.HttpContext.RequestAborted;
			var fromMultipartSectionAttribute = bindingContext.ModelMetadata.AdditionalValues.GetValueOrDefault(typeof(FromMultipartSectionAttribute)) as FromMultipartSectionAttribute;
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
							if (fromMultipartSectionAttribute?.ContentType == null || (section.ContentType?.StartsWith(fromMultipartSectionAttribute.ContentType, StringComparison.OrdinalIgnoreCase) ?? false)) {
								await BindSectionForModelAsync(bindingContext, modelName, section, contentDisposition);
								return;
							}
							else {
								logger.LogWarning("When binding model '{modelName}', the request contains a multipart section with the expected name '{name}', but its content type '{actualContentType}' " +
									"does not match the expected content type '{expectedContentType}'. This section will be ignored for binding. If another section has the same name and the correct content type, it may still be bound.",
									modelName, contentDisposition.Name, section.ContentType, fromMultipartSectionAttribute.ContentType);
							}
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

	/// <summary>
	/// Marks a model component for model binding to a matching multipart request body section using <see cref="MultipartSectionModelBinder"/>.
	/// </summary>
	public class FromMultipartSectionAttribute : ModelBinderAttribute {
		/// <summary>
		/// Initializes the attribute object.
		/// </summary>
		public FromMultipartSectionAttribute() : base(typeof(MultipartSectionModelBinder)) { }

		public string? ContentType { get; init; } = null;
	}

	public class FromMultipartSectionMetadataProvider : IDisplayMetadataProvider {
		public void CreateDisplayMetadata(DisplayMetadataProviderContext context) {
			var attribute = context.Attributes.OfType<FromMultipartSectionAttribute>().FirstOrDefault();
			if (attribute != null) {
				context.DisplayMetadata.AdditionalValues.Add(typeof(FromMultipartSectionAttribute), attribute);
			}
		}
	}

	public static class MvcOptionsMultipartSectionMetadataExtensions {
		public static MvcOptions AddMultipartSectionMetadata(this MvcOptions options) {
			options.ModelMetadataDetailsProviders.Add(new FromMultipartSectionMetadataProvider());
			return options;
		}
	}
}
