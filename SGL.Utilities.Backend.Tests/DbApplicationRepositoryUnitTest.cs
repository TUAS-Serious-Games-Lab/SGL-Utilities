using Microsoft.EntityFrameworkCore;
using SGL.Utilities;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Backend.Tests {
	internal class TestApplication : IApplication {
		public Guid Id { get; set; }
		public string Name { get; set; }
		public string ApiToken { get; set; }

		public TestApplication(Guid id, string name, string apiToken) {
			Id = id;
			Name = name;
			ApiToken = apiToken;
		}
	}

	internal class TestApplicationContext : DbContext {
		public TestApplicationContext(DbContextOptions options) : base(options) { }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			var application = modelBuilder.Entity<TestApplication>();
			application.Property(a => a.Name).HasMaxLength(128);
			application.HasIndex(a => a.Name).IsUnique();
			application.Property(a => a.ApiToken).HasMaxLength(64);
		}

		public DbSet<TestApplication> Applications => Set<TestApplication>();
	}

	public class DbApplicationRepositoryUnitTest : IDisposable {
		readonly TestDatabase<TestApplicationContext> testDb = new();

		public void Dispose() => testDb.Dispose();

		private TestApplicationContext CreateContext() {
			return new TestApplicationContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task ApplicationsCanBeCreatedAndThenRetrivedByName() {
			var app1 = new TestApplication(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_1", StringGenerator.GenerateRandomWord(32));
			var app2 = new TestApplication(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_2", StringGenerator.GenerateRandomWord(32));
			var app3 = new TestApplication(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_3", StringGenerator.GenerateRandomWord(32));
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				await repo.AddApplicationAsync(app1);
				app2 = await repo.AddApplicationAsync(app2);
				await repo.AddApplicationAsync(app3);
			}
			TestApplication? appRead;
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				appRead = await repo.GetApplicationByNameAsync("DbApplicationRepositoryUnitTest_2");
			}
			Assert.NotNull(appRead);
			Assert.Equal(app2.Id, appRead?.Id);
			Assert.Equal(app2.Name, appRead?.Name);
			Assert.Equal(app2.ApiToken, appRead?.ApiToken);
		}

		[Fact]
		public async Task RequestForNonExistentApplicationReturnsNull() {
			TestApplication? appRead;
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				appRead = await repo.GetApplicationByNameAsync("DoesNotExist");
			}
			Assert.Null(appRead);
		}

		[Fact]
		public async Task AttemptingToCreateApplicationWithDuplicateNameThrowsCorrectException() {
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				await repo.AddApplicationAsync(new TestApplication(Guid.NewGuid(), "DbApplicationRepositoryUnitTest", StringGenerator.GenerateRandomWord(32)));
			}
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				Assert.Equal("Name", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.AddApplicationAsync(new TestApplication(Guid.NewGuid(), "DbApplicationRepositoryUnitTest", StringGenerator.GenerateRandomWord(32))))).ConflictingPropertyName);
			}
		}

		[Fact]
		public async Task AttemptingToCreateApplicationWithDuplicateIdThrowsCorrectException() {
			var id = Guid.NewGuid();
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				await repo.AddApplicationAsync(new TestApplication(id, "DbApplicationRepositoryUnitTest_1", StringGenerator.GenerateRandomWord(32)));
			}
			await using (var context = CreateContext()) {
				var repo = new DbApplicationRepository<TestApplication, NullQueryOption, TestApplicationContext>(context);
				Assert.Equal("Id", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.AddApplicationAsync(new TestApplication(id, "DbApplicationRepositoryUnitTest_2", StringGenerator.GenerateRandomWord(32))))).ConflictingPropertyName);
			}
		}
	}
}
