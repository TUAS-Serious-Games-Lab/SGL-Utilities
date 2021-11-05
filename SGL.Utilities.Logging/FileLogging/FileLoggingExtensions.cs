using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System;

namespace SGL.Utilities.Logging.FileLogging {
	/// <summary>
	/// Provides extension methods to add an <see cref="FileLoggingProvider"/> to an <see cref="ILoggingBuilder"/>.
	/// </summary>
	public static class FileLoggingExtensions {

		/// <summary>
		/// Adds the <see cref="FileLoggingProvider"/> along with its config options, without customizing it using an <see cref="IFileLoggingProviderBuilder"/>.
		/// </summary>
		/// <param name="builder">The builder for the logging system to which the provider should be added.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
		public static ILoggingBuilder AddFile(this ILoggingBuilder builder) {
			return builder.AddFile(builder => { });
		}

		/// <summary>
		/// Adds the <see cref="FileLoggingProvider"/> along with its config options, using the given <c>providerBuilder</c> to allow modifying the provider by adding custom placeholders.
		/// </summary>
		/// <param name="builder">The builder for the logging system to which the provider should be added.</param>
		/// <param name="providerBuilder">A builder delegate that can be used to add custom placeholders to the provider.</param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
		public static ILoggingBuilder AddFile(this ILoggingBuilder builder, Action<IFileLoggingProviderBuilder> providerBuilder) {
			builder.AddConfiguration();
			builder.Services.AddSingleton(providerBuilder);
			builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggingProvider>());
			LoggerProviderOptions.RegisterProviderOptions<FileLoggingProviderOptions, FileLoggingProvider>(builder.Services);
			return builder;
		}
	}
}
