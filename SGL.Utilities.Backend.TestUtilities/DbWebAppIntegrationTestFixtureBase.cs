using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace SGL.Utilities.Backend.TestUtilities {
	/// <summary>
	/// Provides a base class for test fixtures for ASP.Net Core web applications that use a database using Entity Framework Core.
	/// It hosts the application as specified by its Startup class in <see cref="WebApplicationFactory{TEntryPoint}"/>,
	/// but replaces the database context with an in-memory one using <see cref="TestDatabase{TContext}"/>.
	/// </summary>
	/// <typeparam name="TContext"></typeparam>
	/// <typeparam name="TStartup"></typeparam>
	public class DbWebAppIntegrationTestFixtureBase<TContext, TStartup> : WebApplicationFactory<TStartup> where TContext : DbContext where TStartup : class {
		private readonly TestDatabase<TContext> db;

		/// <summary>
		/// Creates a new object that uses an anonymous in-memory database.
		/// Only one context <typeparamref name="TContext"/> object can access the database at a time, as they use a shared connection.
		/// </summary>
		protected DbWebAppIntegrationTestFixtureBase() {
			db = new TestDatabase<TContext>();
		}

		/// <summary>
		/// Creates a new object that uses an in-memory database with the given name.
		/// Contrary to <see cref="DbWebAppIntegrationTestFixtureBase()"/>, each <typeparamref name="TContext"/> object uses its own connection.
		/// Calling code should use a unique name for each testing scenario to ensure isolation.
		/// </summary>
		/// <param name="dataSourceName"></param>
		protected DbWebAppIntegrationTestFixtureBase(string dataSourceName) {
			db = new TestDatabase<TContext>(dataSourceName);
		}


		/// <summary>
		/// Disposes both, the underlying <see cref="WebApplicationFactory{TEntryPoint}"/> and the <see cref="TestDatabase{TContext}"/>.
		/// </summary>
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			db.Dispose();
		}

		/// <summary>
		/// Hooks into the web host configuration to perform the changes required.
		/// </summary>
		protected override void ConfigureWebHost(IWebHostBuilder builder) {

			builder.ConfigureTestServices(services => {
				var dbContextDescriptor = services.SingleOrDefault(sd => sd.ServiceType == typeof(TContext));
				if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);
				var dbContextOptionsDescriptor = services.SingleOrDefault(sd => sd.ServiceType == typeof(DbContextOptions<TContext>));
				if (dbContextOptionsDescriptor != null) services.Remove(dbContextOptionsDescriptor);
				services.AddDbContext<TContext>(options => {
					db.ApplyDbContextOptions(options, o => {
						o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
						OverrideOptions(o);
					});
					OverrideOptions(options);
				});
				OverrideConfig(services);

				using (var context = Activator.CreateInstance(typeof(TContext), db.ContextOptions) as TContext ?? throw new InvalidOperationException()) {
					SeedDatabase(context);
				}
			});
		}

		/// <summary>
		/// Provides a hook method for derived classes to adapt the DbContext options.
		/// </summary>
		/// <param name="options">The options being configured.</param>
		protected virtual void OverrideOptions(DbContextOptionsBuilder options) { }

		/// <summary>
		/// Provides a hook method for derived classes to adapt the SqliteDbContext options.
		/// </summary>
		/// <param name="options">The options being configured.</param>
		protected virtual void OverrideOptions(SqliteDbContextOptionsBuilder options) { }

		/// <summary>
		/// Provides a hook method for derived classes to adapt the service configuration after the database context was replaced.
		/// </summary>
		/// <param name="services">The service collection that is being configured.</param>
		protected virtual void OverrideConfig(IServiceCollection services) { }

		/// <summary>
		/// Provides a hook method for derived classes to seed test data into the in-memory testing database.
		/// </summary>
		/// <param name="context">The database context to which the data need to be written.</param>
		protected virtual void SeedDatabase(TContext context) { }
	}
}
