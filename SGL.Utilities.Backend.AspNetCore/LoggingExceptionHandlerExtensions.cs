using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;

namespace SGL.Utilities.Backend.AspNetCore {

	/// <summary>
	/// Provides the <see cref="UseLoggingExceptionHandler{TStartup}(IApplicationBuilder)"/> extension method.
	/// </summary>
	public static class LoggingExceptionHandlerExtensions {
		/// <summary>
		/// Installs a simple exception handler in the <c>app</c> that logs the exception to an <c><![CDATA[ILogger<TStartup>]]></c> and responds with a generic plain text error message <c>"Internal server error."</c>.
		/// </summary>
		/// <typeparam name="TStartup">The Startup class of the webapp, only used as the logging category.</typeparam>
		/// <param name="app">The builder for the application in which the handler shall be installed.</param>
		/// <returns>A reference to <paramref name="app"/> for chaining.</returns>
		public static IApplicationBuilder UseLoggingExceptionHandler<TStartup>(this IApplicationBuilder app) {
			app.UseExceptionHandler(errorHandler => {
				errorHandler.Run(async context => {
					context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
					context.Response.ContentType = "text/plain";
					var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
					var logger = context.RequestServices.GetRequiredService<ILogger<TStartup>>();
					logger.LogError(exceptionHandlerPathFeature.Error, "Uncaught exception from request for path {path}.", exceptionHandlerPathFeature.Path);
					await context.Response.WriteAsync("Internal server error.");
					await context.Response.CompleteAsync();
				});
			});
			return app;
		}

		private const string ModelStateErrorCategoryName = "ModelStateValidation";

		/// <summary>
		/// Configures <see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/> to use a warpper that logs encountered request errors and then calls the original factory to generate the actual repsonse.
		/// The errors are logged in one combined message per failed request.
		/// Additionally, the wrapper also invokes <paramref name="errorCallback"/> for each encountered error.
		/// This can be used, e.g. for collecting error metrics.
		/// </summary>
		/// <param name="services">The service collection where to configure the options.</param>
		/// <param name="errorCallback">An action to be called for each error encountered.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		/// <remarks>Based on https://github.com/dotnet/AspNetCore.Docs/issues/12157#issuecomment-487756787 </remarks>
		public static IServiceCollection AddModelStateValidationErrorLogging(this IServiceCollection services, Action<ModelError> errorCallback) {
			services.PostConfigure<ApiBehaviorOptions>(options => {
				var builtInFactory = options.InvalidModelStateResponseFactory;

				options.InvalidModelStateResponseFactory = context => {
					var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
					var logger = loggerFactory.CreateLogger(ModelStateErrorCategoryName);
					var errors = context.ModelState.SelectMany(msePair => msePair.Value.Errors);
					logger.LogError("Request failed due to the following model state validation errors: {errors}", String.Join(", ", errors.Select(e => $"\"{e.ErrorMessage}\"")));
					foreach (var error in errors) {
						errorCallback(error);
					}
					return builtInFactory(context);
				};
			});
			return services;
		}

		/// <summary>
		/// Configures <see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/> to use a warpper that logs encountered request errors and then calls the original factory to generate the actual repsonse.
		/// The errors are logged in one combined message per failed request.
		/// </summary>
		/// <param name="services">The service collection where to configure the options.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		/// <remarks>Based on https://github.com/dotnet/AspNetCore.Docs/issues/12157#issuecomment-487756787 </remarks>
		public static IServiceCollection AddModelStateValidationErrorLogging(this IServiceCollection services) {
			services.PostConfigure<ApiBehaviorOptions>(options => {
				var builtInFactory = options.InvalidModelStateResponseFactory;

				options.InvalidModelStateResponseFactory = context => {
					var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
					var logger = loggerFactory.CreateLogger(ModelStateErrorCategoryName);
					var errors = context.ModelState.SelectMany(msePair => msePair.Value.Errors);
					logger.LogError("Request failed due to the following model state validation errors: {errors}", String.Join(", ", errors.Select(e => $"\"{e.ErrorMessage}\"")));
					return builtInFactory(context);
				};
			});
			return services;
		}
	}
}
