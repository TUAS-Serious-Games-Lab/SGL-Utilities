using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend {
	/// <summary>
	/// Provides extension methods for <see cref="IServiceCollection"/> that enable lazy injection of services by having a <see cref="Lazy{T}"/>
	/// requested in the consuming class which resolves the wrapped service type on first use.
	/// </summary>
	public static class LazyServiceCollectionExtensions {
		/// <summary>
		/// Adds lazy injection support for <typeparamref name="TService"/> into <paramref name="services"/>.
		/// The service type <typeparamref name="TService"/> must be registered in <paramref name="services"/> with <see cref="ServiceLifetime.Singleton"/>.
		/// With this, services can request a <see cref="Lazy{TService}"/> for injection and will get an object that
		/// resolves <typeparamref name="TService"/> on first use.
		/// </summary>
		/// <typeparam name="TService">The actual service type to be wrapped in <see cref="Lazy{TService}"/>.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to which to add the <see cref="Lazy{TService}"/> wrapper service.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection AddLazySingleton<TService>(this IServiceCollection services) where TService : notnull {
			return services.AddSingleton(sp => new Lazy<TService>(() => sp.GetRequiredService<TService>(), LazyThreadSafetyMode.PublicationOnly));
		}
		/// <summary>
		/// Adds lazy injection support for <typeparamref name="TService"/> into <paramref name="services"/>.
		/// The service type <typeparamref name="TService"/> must be registered in <paramref name="services"/> with <see cref="ServiceLifetime.Scoped"/>.
		/// With this, services can request a <see cref="Lazy{TService}"/> for injection and will get an object that
		/// resolves <typeparamref name="TService"/> on first use.
		/// </summary>
		/// <typeparam name="TService">The actual service type to be wrapped in <see cref="Lazy{TService}"/>.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to which to add the <see cref="Lazy{TService}"/> wrapper service.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection AddLazyScoped<TService>(this IServiceCollection services) where TService : notnull {
			return services.AddScoped(sp => new Lazy<TService>(() => sp.GetRequiredService<TService>(), LazyThreadSafetyMode.PublicationOnly));
		}
		/// <summary>
		/// Adds lazy injection support for <typeparamref name="TService"/> into <paramref name="services"/>.
		/// The service type <typeparamref name="TService"/> must be registered in <paramref name="services"/> with <see cref="ServiceLifetime.Transient"/>.
		/// With this, services can request a <see cref="Lazy{TService}"/> for injection and will get an object that
		/// resolves <typeparamref name="TService"/> on first use.
		/// </summary>
		/// <typeparam name="TService">The actual service type to be wrapped in <see cref="Lazy{TService}"/>.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to which to add the <see cref="Lazy{TService}"/> wrapper service.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection AddLazyTransient<TService>(this IServiceCollection services) where TService : notnull {
			return services.AddTransient(sp => new Lazy<TService>(() => sp.GetRequiredService<TService>(), LazyThreadSafetyMode.ExecutionAndPublication));
		}
	}
}
