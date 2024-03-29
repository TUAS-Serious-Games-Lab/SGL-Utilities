using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Applications {
	/// <summary>
	/// Provides a persistent implementation of <see cref="IApplicationRepository{TApp, TQueryOptions}"/> using Entity Framework Core to map the objects into a relational database.
	/// </summary>
	/// <typeparam name="TApp">The entity class that represents application descriptions to manage.</typeparam>
	/// <typeparam name="TQueryOptions">A class that encapsulates options for querying methods, e.g. whether related entities should be fetched.</typeparam>
	/// <typeparam name="TContext">The database context class by which the application entity objects shall be managed.</typeparam>
	public class DbApplicationRepository<TApp, TQueryOptions, TContext> : IApplicationRepository<TApp, TQueryOptions> where TApp : class, IApplication where TQueryOptions : class where TContext : DbContext {
		private readonly TContext context;
		private readonly DbSet<TApp> appsSet;

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(TContext context) {
			this.context = context;
			appsSet = GetAppsSet(context);
		}

		/// <summary>
		/// Used by the constructor to obtain the <see cref="DbSet{TApp}"/> that is used to query and store the application objects from the <see cref="DbContext"/> subclass given for <c>TContext</c>.
		/// It can be overridden by deriving classes to change the lookup behavior.
		/// The default implementation provided here obtains the set by searching for a public non-static property of the appropriate type (<see cref="DbSet{TApp}"/>) in context class.
		/// It expects there to be only one such property. This property's value is then returned to be used by the other methods.
		/// </summary>
		/// <param name="context">The context class object, as passed to the constructor.</param>
		/// <returns>The <see cref="DbSet{TApp}"/> object that the other methods will use for their database operations.</returns>
		/// <exception cref="ArgumentException">When the given context class doesn't have an appropriate property or has multiple <see cref="DbSet{TApp}"/>-typed properties,
		/// or when the property returned a null value.</exception>
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

		/// <summary>
		/// Called by querying methods (<see cref="GetApplicationByNameAsync(string, TQueryOptions?, CancellationToken)"/>, <see cref="ListApplicationsAsync(TQueryOptions?, CancellationToken)"/>)
		/// before they execute their query. The default implementation just passes the query through.
		/// Deriving classes can override this to manipulate the query, usually based on the query options.
		/// The intended use case for this is chaining in <see cref="EntityFrameworkQueryableExtensions.Include{TEntity, TProperty}(IQueryable{TEntity}, System.Linq.Expressions.Expression{Func{TEntity, TProperty}})"/> calls
		/// to also load related entity objects with the query result, if requested so by the query options.
		/// </summary>
		/// <param name="query">The query object coming from a query method.</param>
		/// <param name="options">An object encapsulating options that shall be applied to the query, as passed to the query method.</param>
		/// <returns>An <see cref="IQueryable{TApp}"/> built around <paramref name="query"/>. The default implementation returns <paramref name="query"/> unchanged.</returns>
		protected virtual IQueryable<TApp> OnPrepareQuery(IQueryable<TApp> query, TQueryOptions? options) {
			return query;
		}

		/// <inheritdoc/>
		public Task<TApp?> GetApplicationByNameAsync(string appName, TQueryOptions? queryOptions = null, CancellationToken ct = default) {
			IQueryable<TApp> query = appsSet.Where(a => a.Name == appName);
			query = OnPrepareQuery(query, queryOptions);
			return query.OrderBy(a => a.Name).SingleOrDefaultAsync<TApp?>(ct);
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
