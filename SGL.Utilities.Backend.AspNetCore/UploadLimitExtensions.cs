using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Encapsulates configuration options for the middleware added by <see cref="UploadLimitExtensions"/>.
	/// </summary>
	public class UploadLimitOptions {
		/// <summary>
		/// Specifies the size limit for uploads in bytes. Defaults to 200 MiB.
		/// </summary>
		public long UploadSizeLimit { get; set; } = 200 * 1024 * 1024;
	}

	/// <summary>
	/// Specifies that the middleware added by <see cref="UploadLimitExtensions"/> applies to the controller method marked with this attribute.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = true)]
	public class UseConfigurableUploadLimitAttribute : Attribute { }

	/// <summary>
	/// Provides extension methods used to add and configure services and a middleware required for the configurable size limit for uploads.
	/// </summary>
	public static class UploadLimitExtensions {
		/// <summary>
		/// Adds the <see cref="IOptions{UploadLimitOptions}"/> config object to the dependency injection container.
		/// </summary>
		/// <param name="services">The service collection for the DI container.</param>
		/// <param name="config">The root config object too lookup the config section under.</param>
		/// <param name="configSection">The config section key to read from <paramref name="config"/>. This section is used to obtain the values for the config options contained in <see cref="UploadLimitOptions"/>.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseConfigurableUploadLimit(this IServiceCollection services, IConfiguration config, string configSection) {
			services.Configure<UploadLimitOptions>(config.GetSection(configSection));
			return services;
		}

		/// <summary>
		/// Adds a conditional middleware that sets the configured upload size limit for requests that are handled by controller methods marked with <see cref="UseConfigurableUploadLimitAttribute"/>.
		/// Important: This middleware must be added between <see cref="EndpointRoutingApplicationBuilderExtensions.UseRouting(IApplicationBuilder)"/> and
		/// <see cref="EndpointRoutingApplicationBuilderExtensions.UseEndpoints(IApplicationBuilder, Action{Microsoft.AspNetCore.Routing.IEndpointRouteBuilder})"/> to work correctly.
		/// </summary>
		/// <param name="app">The builder object for the app to configure.</param>
		/// <returns>A reference to <paramref name="app"/> for chaining.</returns>
		public static IApplicationBuilder UseConfigurableUploadLimit(this IApplicationBuilder app) {
			app.UseWhen(context => {
				var endpoint = context.GetEndpoint();
				if (endpoint == null) return false;
				var cad = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
				if (cad == null) return false;
				return cad.MethodInfo.GetCustomAttributes(typeof(UseConfigurableUploadLimitAttribute), true).Any();
			},
			appBuild => {
				appBuild.Use((context, next) => {
					var options = context.RequestServices.GetRequiredService<IOptions<UploadLimitOptions>>();
					var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
					if (bodySizeFeature != null) {
						bodySizeFeature.MaxRequestBodySize = options.Value.UploadSizeLimit;
					}
					return next();
				});
			});
			return app;
		}
	}
}
