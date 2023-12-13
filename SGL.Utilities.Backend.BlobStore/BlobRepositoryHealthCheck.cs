using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.BlobStore {
	/// <summary>
	/// Implements a health check for <see cref="IBlobRepository"/> implementations to allow an ASP.NET Core application to include their health status in its reported health.
	/// </summary>
	public class BlobRepositoryHealthCheck : IHealthCheck {
		private readonly IBlobRepository blobRepo;

		/// <summary>
		/// Constructs a health check object, injecting the active configured <see cref="IBlobRepository"/> implementation.
		/// </summary>
		public BlobRepositoryHealthCheck(IBlobRepository logRepo) {
			this.blobRepo = logRepo;
		}

		/// <summary>
		/// Asynchronously performs the health check by calling <see cref="IBlobRepository.CheckHealthAsync(CancellationToken)"/>, returning
		/// <see cref="HealthCheckResult.Healthy(string?, IReadOnlyDictionary{string, object}?)"/> if the call completes without exceptions and returning
		/// <see cref="HealthCheckResult.Unhealthy(string?, Exception?, IReadOnlyDictionary{string, object}?)"/> if an exception is thrown from the call.
		/// </summary>
		/// <param name="context">A context object, not used by this implementation.</param>
		/// <param name="cancellationToken">A cancellation token, allowing the cancellation of the health check operation.</param>
		/// <returns>A result object with the corresponding status.</returns>
		public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
			try {
				await blobRepo.CheckHealthAsync(cancellationToken);
				return HealthCheckResult.Healthy("The blob repository is fully operational.");
			}
			catch (Exception ex) {
				return HealthCheckResult.Unhealthy("The blob repository is not operational.", ex);
			}
		}
	}
}
