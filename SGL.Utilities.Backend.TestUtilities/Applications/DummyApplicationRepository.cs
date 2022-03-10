using SGL.Utilities.Backend.Applications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.TestUtilities.Applications {
	/// <summary>
	/// An in-memory dummy implementation of <see cref="IApplicationRepository{TApp, TQueryOptions}"/> to use in test code.
	/// </summary>
	/// <typeparam name="TApp">The entity class that represents application descriptions to manage.</typeparam>
	/// <typeparam name="TQueryOptions">A class that encapsulates options for querying methods.</typeparam>
	public class DummyApplicationRepository<TApp, TQueryOptions> : IApplicationRepository<TApp, TQueryOptions> where TApp : class, IApplication where TQueryOptions : class {
		private readonly Dictionary<string, TApp> apps = new();

		/// <inheritdoc/>
		public async Task<TApp> AddApplicationAsync(TApp app, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name", app.Name);
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			if (apps.Values.Any(a => a.Id == app.Id)) throw new EntityUniquenessConflictException("Application", "Id", app.Id);
			ct.ThrowIfCancellationRequested();
			apps.Add(app.Name, app);
			return app;
		}

		/// <inheritdoc/>
		public async Task<TApp?> GetApplicationByNameAsync(string appName, TQueryOptions? queryOptions = null, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		/// <inheritdoc/>
		public async Task<IList<TApp>> ListApplicationsAsync(TQueryOptions? queryOptions = null, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			return apps.Values.ToList();
		}

		/// <inheritdoc/>
		public async Task<TApp> UpdateApplicationAsync(TApp app, CancellationToken ct = default) {
			await Task.CompletedTask;
			Debug.Assert(apps.ContainsKey(app.Name));
			ct.ThrowIfCancellationRequested();
			apps[app.Name] = app;
			return app;
		}
	}
}
