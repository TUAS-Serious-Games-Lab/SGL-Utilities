﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
				services.AddSingleton(this);
				services.AddHostedService<SeedDatabaseService>();
				OverrideConfig(services);
			});
		}

		private class SeedDatabaseService : IHostedService {
			private IServiceProvider serviceProvider;
			private DbWebAppIntegrationTestFixtureBase<TContext, TStartup> fixture;
			private ILogger<SeedDatabaseService> logger;

			public SeedDatabaseService(IServiceProvider serviceProvider, DbWebAppIntegrationTestFixtureBase<TContext, TStartup> fixture,
				ILogger<DbWebAppIntegrationTestFixtureBase<TContext, TStartup>.SeedDatabaseService> logger) {
				this.serviceProvider = serviceProvider;
				this.fixture = fixture;
				this.logger = logger;
			}

			public async Task StartAsync(CancellationToken ct) {
				await using var scope = serviceProvider.CreateAsyncScope();
				using var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
				logger.LogTrace("Starting to seed database.");
				await fixture.SeedDatabaseAsync(dbContext, scope.ServiceProvider, ct);
				logger.LogTrace("Finished seeding database.");
			}

			public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
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
		/// <summary>
		/// Provides a hook method for derived classes to seed test data into the in-memory testing database.
		/// The default implementation invokes <see cref="SeedDatabase(TContext)"/>.
		/// </summary>
		/// <param name="context">The database context to which the data need to be written.</param>
		/// <param name="scopedServiceProvider">The scoped service provider for dependency injection services from which <paramref name="context"/> was resolved.</param>
		protected virtual void SeedDatabase(TContext context, IServiceProvider scopedServiceProvider) {
			SeedDatabase(context);
		}
		/// <summary>
		/// Provides a hook method for derived classes to asynchronously seed test data into the in-memory testing database.
		/// The default implementation invokes <see cref="SeedDatabase(TContext, IServiceProvider)"/> synchronously and then completes.
		/// </summary>
		/// <param name="context">The database context to which the data need to be written.</param>
		/// <param name="scopedServiceProvider">The scoped service provider for dependency injection services from which <paramref name="context"/> was resolved.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		protected virtual Task SeedDatabaseAsync(TContext context, IServiceProvider scopedServiceProvider, CancellationToken ct) {
			SeedDatabase(context, scopedServiceProvider);
			return Task.CompletedTask;
		}
	}
}
