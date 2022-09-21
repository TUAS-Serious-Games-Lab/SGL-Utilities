using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.AspNetCore {
	internal class KeyIdModelBinderProvider : IModelBinderProvider {
		public IModelBinder? GetBinder(ModelBinderProviderContext context) {
			if (context == null) {
				throw new ArgumentNullException(nameof(context));
			}
			if (context.Metadata.ModelType == typeof(KeyId)) {
				return new BinderTypeModelBinder(typeof(KeyIdModelBinder));
			}
			return null;
		}
	}
	internal class KeyIdModelBinder : IModelBinder {
		public Task BindModelAsync(ModelBindingContext bindingContext) {
			if (bindingContext.ModelType != typeof(KeyId)) {
				return Task.CompletedTask;
			}
			string modelName = bindingContext.ModelName;
			var value = bindingContext.ValueProvider.GetValue(modelName).FirstValue;
			if (value != null) {
				if (KeyId.TryParse(value, out var keyId)) {
					bindingContext.Result = ModelBindingResult.Success(keyId);
				}
				else {
					bindingContext.ModelState.AddModelError(modelName, "KeyId value could not be parsed.");
					bindingContext.Result = ModelBindingResult.Failed();
				}
			}
			else {
				bindingContext.Result = ModelBindingResult.Success(null);
			}
			return Task.CompletedTask;
		}
	}
	/// <summary>
	/// Provides the <see cref="AddKeyIdModelBinding(MvcOptions)"/> extension method.
	/// </summary>
	public static class MvcOptionsKeyIdModelBindingExtensions {
		/// <summary>
		/// Adds model binding for <see cref="KeyId"/>-typed parameters in <paramref name="options"/>.
		/// This allows e.g. controller methods to have parameters of type <see cref="KeyId"/>, bound from query strings or routes.
		/// </summary>
		/// <param name="options">The option object where to activate the model binder.</param>
		/// <returns>A reference to <paramref name="options"/> for chaining.</returns>
		public static MvcOptions AddKeyIdModelBinding(this MvcOptions options) {
			options.ModelBinderProviders.Insert(0, new KeyIdModelBinderProvider());
			return options;
		}
	}
}
