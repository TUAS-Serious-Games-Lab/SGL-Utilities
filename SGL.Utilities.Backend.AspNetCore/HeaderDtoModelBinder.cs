using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	internal class HeaderDtoModelBinder : IModelBinder {
		public Task BindModelAsync(ModelBindingContext bindingContext) {
			if (bindingContext == null) throw new ArgumentNullException(nameof(bindingContext));
			var modelName = bindingContext.FieldName;
			var prefix = "";
			if (!string.IsNullOrEmpty(bindingContext.BinderModelName)) {
				modelName = bindingContext.BinderModelName;
				prefix = $"{bindingContext.BinderModelName}.";
			}
			var valid = true;
			var constructor = bindingContext.ModelType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(ci => ci.GetParameters().Length).First();
			var ctorArgs = new object?[constructor.GetParameters().Length];
			var ctorParams = constructor.GetParameters();
			for (int i = 0; i < ctorParams.Length; ++i) {
				var headerName = $"{prefix}{ctorParams[i].Name}";
				if (bindingContext.HttpContext.Request.Headers.TryGetValue(headerName, out var values) && values.Count > 0) {
					var strVal = values.First();
					var converter = GetConverter(ctorParams[i].ParameterType);
					var convertedVal = converter(strVal);
					if (convertedVal is not null) {
						ctorArgs[i] = convertedVal;
					}
					else {
						valid = false;
						bindingContext.ModelState.TryAddModelError(modelName, $"The header {headerName} could not be parsed as the expected type.");
					}
				}
				else {
					valid = false;
					bindingContext.ModelState.TryAddModelError(modelName, $"The header {headerName} is required.");
				}
			}
			if (valid) {
				bindingContext.Model = constructor.Invoke(ctorArgs);
				bindingContext.Result = ModelBindingResult.Success(bindingContext.Model);
			}
			else {
				bindingContext.Result = ModelBindingResult.Failed();
			}
			return Task.CompletedTask;
		}

		private Func<string, object?> GetConverter(Type type) => type switch {
			Type _ when type == typeof(DateTime) => source => DateTime.TryParse(source, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null,
			Type _ when type == typeof(string) => source => source,
			Type _ when type == typeof(Guid) => source => Guid.TryParse(source, out var guid) ? guid : null,//
			{ IsEnum: true } => source => Enum.TryParse(type, source, out var e) ? e : null,
			Type _ when type == typeof(short) => source => short.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
			Type _ when type == typeof(int) => source => int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
			Type _ when type == typeof(long) => source => long.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
			Type _ when type == typeof(double) => source => double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out var fp) ? fp : null,
			Type _ when type == typeof(decimal) => source => decimal.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out var fp) ? fp : null,
			Type _ when type == typeof(bool) => source => bool.TryParse(source, out var b) ? b : null,
			_ => source => null,
		};
	}

	/// <summary>
	/// Marks a data transfer object (DTO) of a POD type to have its properties bound from http(s) request headers.
	/// </summary>
	public class DtoFromHeaderModelBinderAttribute : ModelBinderAttribute {
		/// <summary>
		/// Constructs a <see cref="DtoFromHeaderModelBinderAttribute"/>.
		/// </summary>
		public DtoFromHeaderModelBinderAttribute() : base(typeof(HeaderDtoModelBinder)) { }
		/// <summary>
		/// Specifies the bindung source as <see cref="BindingSource.Header"/>.
		/// </summary>
		public override BindingSource BindingSource => BindingSource.Header;
	}
}
