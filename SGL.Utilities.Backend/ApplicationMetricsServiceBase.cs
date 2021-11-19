using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend {
	/// <summary>
	/// Encapsulates the configuration options for <see cref="ApplicationMetricsServiceBase"/>.
	/// </summary>
	public class ApplicationMetricsServiceOptions {
		/// <summary>
		/// The config key under which the options are looked up, <c>ApplicationMetrics</c>.
		/// </summary>
		public const string OptionsName = "ApplicationMetrics";
		/// <summary>
		/// The interval in which <see cref="ApplicationMetricsServiceBase.UpdateMetrics(CancellationToken)"/> shall run.
		/// </summary>
		public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMinutes(1);
	}
	/// <summary>
	/// Provides the <see cref="UseApplicationMetricsService{TServiceImpl}(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class ApplicationMetricsServiceExtensions {
		/// <summary>
		/// Adds the given <see cref="ApplicationMetricsServiceBase"/> implementation as the service to update application-level metrics and
		/// the configuration for <see cref="ApplicationMetricsServiceBase"/>.
		/// </summary>
		/// <typeparam name="TServiceImpl">The type, implementing the logic for <see cref="ApplicationMetricsServiceBase.UpdateMetrics(CancellationToken)"/>.</typeparam>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The config root to use.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseApplicationMetricsService<TServiceImpl>(this IServiceCollection services, IConfiguration config)
																where TServiceImpl : ApplicationMetricsServiceBase {
			services.Configure<ApplicationMetricsServiceOptions>(config.GetSection(ApplicationMetricsServiceOptions.OptionsName));
			services.AddScopedBackgroundService<TServiceImpl>();
			return services;
		}
	}

	/// <summary>
	/// Provides a base class for service classes that update service-/application-specific metrics.
	/// Derived classes implement <see cref="UpdateMetrics(CancellationToken)"/> with the logic to obtain the metrics values and to provide them to the metrics library used.
	/// The class itself is derived from <see cref="IScopedBackgroundService"/> to allow <see cref="UpdateMetrics(CancellationToken)"/> to access scoped dependencies from the DI container,
	/// as such metrics values are often fetched through such service dependencies, e.g. a <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
	/// </summary>
	public abstract class ApplicationMetricsServiceBase : IScopedBackgroundService {
		private ApplicationMetricsServiceOptions options;
		private ILogger logger;

		/// <summary>
		/// Construct the base class object, taking the required dependencies, that the derived class should obtain through dependency injection and pass through to the base constructor.
		/// </summary>
		/// <param name="options">The options object, configuring the behavior for the base class logic.</param>
		/// <param name="logger">
		/// The logger object the base class should use.
		/// The derived class should inject <see cref="ILogger{TCategoryName}"/> with the derived class in the category parameter,
		/// instead of using the parameter-less <see cref="ILogger"/>.
		/// </param>
		protected ApplicationMetricsServiceBase(IOptions<ApplicationMetricsServiceOptions> options, ILogger logger) {
			this.options = options.Value;
			this.logger = logger;
		}

		/// <summary>
		/// Asynchronously runs the service main logic by polling <see cref="UpdateMetrics(CancellationToken)"/> with an interval of <see cref="ApplicationMetricsServiceOptions.UpdateInterval"/>.
		/// If <see cref="UpdateMetrics(CancellationToken)"/> throws an exception, it is logged and otherwise ignored, i.e. the next iteration will run regardless.
		/// </summary>
		/// <param name="stoppingToken">A cancellation token, triggered by the host environment when it shuts down.</param>
		/// <returns>A task representing the service's operation.</returns>
		public async Task ExecuteAsync(CancellationToken stoppingToken) {
			logger.LogDebug("Starting ApplicationMetricsService ...");
			try {
				while (!stoppingToken.IsCancellationRequested) {
					try {
						await UpdateMetrics(stoppingToken);
					}
					catch (OperationCanceledException) {
						break;
					}
					catch (Exception ex) {
						logger.LogWarning(ex, "Caught error during metrics update.");
					}
					await Task.Delay(options.UpdateInterval, stoppingToken);
				}
			}
			finally {
				logger.LogDebug("ApplicationMetricsService is shutting down.");
			}
		}

		/// <summary>
		/// The method to be implemented by the derived class to provide the logic for asynchronously obtaining current metrics values and passing them to the metrics collection system.
		/// </summary>
		/// <param name="ct">A cancellation token to cancel the update when the service is shut down during the update.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		protected abstract Task UpdateMetrics(CancellationToken ct);
	}
}
