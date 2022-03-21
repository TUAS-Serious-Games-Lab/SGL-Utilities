using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Disables the ASP.Net Core form value model binding to allow other components to read the body stream directly,
	/// as specified here: https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-5.0#upload-large-files-with-streaming
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter {
		/// <inheritdoc/>
		public void OnResourceExecuting(ResourceExecutingContext context) {
			var factories = context.ValueProviderFactories;
			factories.RemoveType<FormValueProviderFactory>();
			factories.RemoveType<FormFileValueProviderFactory>();
			factories.RemoveType<JQueryFormValueProviderFactory>();
		}

		/// <inheritdoc/>
		public void OnResourceExecuted(ResourceExecutedContext context) { }
	}
}
