using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System;
using System.Data.Common;

namespace SGL.Utilities.Backend.TestUtilities {
	/// <summary>
	/// A test utility class that provides a connection and DB context options for an in-memory Sqlite database for a given DbContext type.
	/// </summary>
	/// <typeparam name="TContext">The DbContext type to use for the database schema.</typeparam>
	public class TestDatabase<TContext> : IDisposable where TContext : DbContext {
		/// <summary>
		/// The database connection to the in-memory database.
		/// It can be passed to <see cref="SqliteDbContextOptionsBuilderExtensions.UseSqlite{TContext}(DbContextOptionsBuilder{TContext}, DbConnection, Action{Microsoft.EntityFrameworkCore.Infrastructure.SqliteDbContextOptionsBuilder})"/> to use it.
		/// </summary>
		public DbConnection Connection { get; }
		/// <summary>
		/// DB context options containing <see cref="Connection"/>.
		/// Can be passed to the database context constructor to use it.
		/// </summary>
		public DbContextOptions<TContext> ContextOptions { get; }

		private readonly string? dataSourceName = null;
		private readonly ILoggerFactory? loggerFactory = null;

		/// <summary>
		/// Creates an sqlite in-memory database and applies the schema specified by the database context type to it.
		/// This overload creates an anonymous in-memory database, held by the connection opened here.
		/// Thus there is only a single connection for the contexts to use and it is not possible to use multiple context objects on it concurrently.
		/// </summary>
		/// <param name="loggerFactory">Optionally specifies a logger factory to use for logging in the database contexts created using <see cref="ContextOptions"/>.</param>
		public TestDatabase(ILoggerFactory? loggerFactory = null) {
			this.loggerFactory = loggerFactory;
			Connection = new SqliteConnection("Filename=:memory:");
			Connection.Open();
			var optionsBuilder = new DbContextOptionsBuilder<TContext>();
			ApplyDbContextOptions(optionsBuilder);
			ContextOptions = optionsBuilder.Options;
			using (var context = Activator.CreateInstance(typeof(TContext), ContextOptions) as TContext ?? throw new InvalidOperationException()) {
				context.Database.EnsureCreated();
			}
		}

		/// <summary>
		/// Creates an sqlite in-memory database with the given data source name and applies the schema specified by the database context type to it.
		/// This overload creates a database on which multiple connections can be active at a time.
		/// </summary>
		/// <param name="dataSourceName">The name of the in-memory database.</param>
		/// <param name="loggerFactory">An optional logger factory to configure into the <see cref="DbContextOptions{TContext}"/>.</param>
		public TestDatabase(string dataSourceName, ILoggerFactory? loggerFactory = null) {
			this.loggerFactory = loggerFactory;
			this.dataSourceName = dataSourceName;
			Connection = new SqliteConnection(MakeConnectionStringShared(dataSourceName));
			Connection.Open();
			var optionsBuilder = new DbContextOptionsBuilder<TContext>();
			ApplyDbContextOptions(optionsBuilder);
			ContextOptions = optionsBuilder.Options;
			using (var context = Activator.CreateInstance(typeof(TContext), ContextOptions) as TContext ?? throw new InvalidOperationException()) {
				context.Database.EnsureCreated();
			}
		}

		private static string MakeConnectionStringShared(string dataSourceName) {
			return $"Data Source={dataSourceName};Mode=Memory;Cache=Shared";
		}

		/// <summary>
		/// Applies the DbContext options to use this <see cref="TestDatabase{TContext}"/> to the given <paramref name="builder"/>.
		/// </summary>
		/// <param name="builder">The builder to apply the options to.</param>
		/// <param name="sqliteOptions">An optional delegate with options to configure on
		/// <see cref="SqliteDbContextOptionsBuilderExtensions.UseSqlite(DbContextOptionsBuilder, string, Action{SqliteDbContextOptionsBuilder})"/> or
		/// <see cref="SqliteDbContextOptionsBuilderExtensions.UseSqlite(DbContextOptionsBuilder, DbConnection, Action{SqliteDbContextOptionsBuilder})"/>.
		/// </param>
		/// <returns>A reference to <paramref name="builder"/> for chaining.</returns>
		public DbContextOptionsBuilder ApplyDbContextOptions(DbContextOptionsBuilder builder, Action<SqliteDbContextOptionsBuilder>? sqliteOptions = null) {
			if (dataSourceName != null) {
				builder.UseSqlite(MakeConnectionStringShared(dataSourceName), sqliteOptions);
				if (loggerFactory != null) {
					builder.UseLoggerFactory(loggerFactory);
				}
			}
			else {
				builder.UseSqlite(Connection, sqliteOptions);
				if (loggerFactory != null) {
					builder.UseLoggerFactory(loggerFactory);
				}
			}
			return builder;
		}

		/// <summary>
		/// Disposes the connection object, also closing and destroying the in-memory database.
		/// </summary>
		public void Dispose() => Connection.Dispose();

	}
}
