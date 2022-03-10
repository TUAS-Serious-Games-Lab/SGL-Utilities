using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Applications {
	public class NullQueryOption { }

	/// <summary>
	/// Provides a persistent implementation of <see cref="IApplicationRepository{TApp, TQueryOptions}"/> using Entity Framework Core to map the objects into a relational database.
	/// </summary>
	public class DbApplicationRepository<TApp, TQueryOptions, TContext> : IApplicationRepository<TApp, TQueryOptions> where TApp : class, IApplication where TQueryOptions : class where TContext : DbContext {
		private TContext context;
		private DbSet<TApp> appsSet;

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(TContext context) {
			this.context = context;
			appsSet = GetAppsSet(context);
		}

		protected virtual DbSet<TApp> GetAppsSet(TContext context) {
			try {
				var properties = context.GetType().GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);
				var appsSetProp = properties.Single(prop => prop.PropertyType == typeof(DbSet<TApp>));
				return (appsSetProp.GetValue(context) as DbSet<TApp>) ?? throw new ArgumentException($"The given context didn't provide a valid DbSet<{typeof(TApp).Name}>.", nameof(context));
			}
			catch (InvalidOperationException ex) {
				throw new ArgumentException($"Couldn't find a property of type DbSet<{typeof(TApp).Name}> in the given context.", nameof(context), ex);
			}
		}

		protected virtual IQueryable<TApp> OnPrepareQuery(IQueryable<TApp> query, TQueryOptions? options) {
			return query;
		}

		/// <inheritdoc/>
		public Task<TApp?> GetApplicationByNameAsync(string appName, TQueryOptions? queryOptions = null, CancellationToken ct = default) {
			IQueryable<TApp> query = appsSet.Where(a => a.Name == appName);
			query = OnPrepareQuery(query, queryOptions);
			return query.SingleOrDefaultAsync<TApp?>(ct);
		}

		/// <inheritdoc/>
		public async Task<TApp> AddApplicationAsync(TApp app, CancellationToken ct = default) {
			appsSet.Add(app);
			try {
				await context.SaveChangesAsync(ct);
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await appsSet.CountAsync(a => a.Name == app.Name, ct) > 0) {
					throw new EntityUniquenessConflictException("Application", "Name", app.Name, ex);
				}
				else if (await appsSet.CountAsync(a => a.Id == app.Id, ct) > 0) {
					throw new EntityUniquenessConflictException("Application", "Id", app.Id, ex);
				}
				else throw;
			}
			return app;
		}

		/// <inheritdoc/>
		public async Task<TApp> UpdateApplicationAsync(TApp app, CancellationToken ct = default) {
			Debug.Assert(context.Entry(app).State is EntityState.Modified or EntityState.Unchanged);
			await context.SaveChangesAsync(ct);
			return app;
		}

		/// <inheritdoc/>
		public async Task<IList<TApp>> ListApplicationsAsync(TQueryOptions? queryOptions = null, CancellationToken ct = default) {
			IQueryable<TApp> query = appsSet;
			query = OnPrepareQuery(query, queryOptions);
			return await query.ToListAsync(ct);
		}
	}
}
