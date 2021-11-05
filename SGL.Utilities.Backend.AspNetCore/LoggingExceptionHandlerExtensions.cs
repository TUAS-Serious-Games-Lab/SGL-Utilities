﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
	}
}
