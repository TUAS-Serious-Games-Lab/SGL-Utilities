using Microsoft.EntityFrameworkCore;
using SGL.Utilities.Backend.KeyValueProperties;
using SGL.Utilities.Backend.Tests.PropTest;
using SGL.Utilities.Backend.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Backend.Tests {

	namespace PropTest {
		public class Aggregate {
			public Guid Id { get; set; }
			public string Label { get; set; } = "";
			public ICollection<Part> Parts { get; set; } = null!;
			public ICollection<PropDef> Properties { get; set; } = null!;

			public PropDef AddProperty(string name, PropertyType type, bool required = false) {
				if (Properties.Any(p => p.Name == name)) {
					throw new ConflictingPropertyNameException(name);
				}
				var prop = PropDef.Create<PropDef>(this, name, type, required);
				Properties.Add(prop);
				return prop;
			}

			public static Aggregate Create(string label) => new Aggregate { Id = Guid.NewGuid(), Label = label, Parts = new List<Part>(), Properties = new List<PropDef>() };
		}

		public class Part {
			public Guid Id { get; set; }
			public string Label { get; set; } = "";
			public Aggregate Aggregate { get; set; } = null!;
			public ICollection<PropInst> Properties { get; set; } = null!;

			public void ValidateProperties() {
				PropertiesUtility.ValidateProperties(this, io => io.Properties, io => io.Aggregate.Properties);
			}

			public static Part Create(Aggregate aggregate, string label) => new Part { Id = Guid.NewGuid(), Label = label, Aggregate = aggregate, Properties = new List<PropInst>() };

			public PropInst SetProperty(string name, object? value) => PropertiesUtility.SetKeyValueProperty(this, name, value,
					io => io.Properties, io => io.Aggregate.Properties, (def, owner) => PropInst.Create<PropInst>(def, owner));
			public PropInst SetProperty(PropDef propDef, object? value) => PropertiesUtility.SetKeyValueProperty(this, propDef, value,
					io => io.Properties, (def, owner) => PropInst.Create<PropInst>(def, owner));
			public object? GetProperty(PropDef propDef) => PropertiesUtility.GetKeyValueProperty(this, propDef, io => io.Properties);
			public object? GetProperty(string name) => PropertiesUtility.GetKeyValueProperty(this, name, io => io.Properties, p => p.Definition);

			public void SetProperties(IDictionary<string, object?> dict) =>
				PropertiesUtility.SetKeyValuePropertiesFromDictionary(this, dict, io => io.Properties, io => io.Aggregate.Properties, (def, owner) => PropInst.Create<PropInst>(def, owner));

			public IDictionary<string, object?> GetProperties() => PropertiesUtility.ConvertKeyValuePropertiesToDictionary(this, io => io.Properties, pi => pi.Definition);
		}

		public class PropDef : PropertyDefinitionBase<Aggregate> { }
		public class PropInst : PropertyInstanceBase<Part, PropDef> { }
	}

	public class KeyValuePropertiesTestContext : DbContext {
		public KeyValuePropertiesTestContext(DbContextOptions options) : base(options) { }

		protected override void OnModelCreating(ModelBuilder modelBuilder) {
			var aggregate = modelBuilder.Entity<Aggregate>();
			var part = modelBuilder.Entity<Part>();
			modelBuilder.KeyValuePropertiesBetween(aggregate, part, a => a.Properties, p => p.Properties);
		}

		public DbSet<Aggregate> Aggregates => Set<Aggregate>();
		public DbSet<Part> Parts => Set<Part>();
	}

	public class DbKeyValuePropertiesTest : IDisposable {
		TestDatabase<KeyValuePropertiesTestContext> testDb = new();

		public void Dispose() => testDb.Dispose();

		private KeyValuePropertiesTestContext createContext() {
			return new KeyValuePropertiesTestContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task EntitiesWithPropertiesCanBeStoredAndReadBackCorrectly() {
			var agg = Aggregate.Create("test");
			agg.AddProperty("Number", PropertyType.Integer);
			var strDef = agg.AddProperty("String", PropertyType.String);
			var dateDef = agg.AddProperty("Date", PropertyType.DateTime);
			agg.AddProperty("Mapping", PropertyType.Json);

			var date = DateTime.Today;
			var mapping = new Dictionary<string, object?>() {
				["A"] = 42,
				["B"] = "Test"
			};
			var part = Part.Create(agg, "Part 1");
			part.SetProperty("Number", 1234);
			part.SetProperty(strDef, "Hello World");
			part.SetProperty(dateDef, date);
			part.SetProperty("Mapping", mapping);

			part.ValidateProperties();

			using (var context = createContext()) {
				context.Add(agg);
				context.Add(part);
				await context.SaveChangesAsync();
			}

			using (var context = createContext()) {
				var readAgg = await context.Aggregates.FindAsync(agg.Id);
				Assert.NotNull(readAgg);
				Assert.Equal(agg.Id, readAgg.Id);
				Assert.Equal(agg.Label, readAgg.Label);
				var readPart = await context.Parts.FindAsync(part.Id);
				Assert.NotNull(readPart);
				Assert.Equal(part.Id, readPart.Id);
				Assert.Equal(part.Label, readPart.Label);
				await context.Entry(readAgg).Collection(a => a.Properties).LoadAsync();
				await context.Entry(readPart).Collection(p => p.Properties).LoadAsync();
				var numDef = readAgg.Properties.Single(p => p.Name == "Number");
				Assert.Equal(1234, part.GetProperty(numDef));
				Assert.Equal("Hello World", part.GetProperty("String"));
				Assert.Equal(date.ToUniversalTime(), (part.GetProperty("Date") as DateTime?)?.ToUniversalTime());
				var mappingRead = Assert.IsAssignableFrom<IDictionary<string, object?>>(part.GetProperty("Mapping"));
				Assert.All(mapping, kvp => Assert.Equal(kvp.Value, Assert.Contains(kvp.Key, mappingRead)));
			}
		}

		[Fact]
		public void AttemptToSetNonExistentPropertyThrowsCorrectException() {
			var agg = Aggregate.Create("test");
			agg.AddProperty("Number", PropertyType.Integer);

			var part = Part.Create(agg, "Part 1");
			part.SetProperty("Number", 1234);

			Assert.Equal("DoesNotExist", (Assert.Throws<UndefinedPropertyException>(() => part.SetProperty("DoesNotExist", "Hello World"))).UndefinedPropertyName);
		}

		[Fact]
		public void AttemptToSetPropertyOfIncorrectTypeThrowsCorrectException() {
			var agg = Aggregate.Create("test");
			agg.AddProperty("Message", PropertyType.String);
			var part = Part.Create(agg, "Part 1");
			Assert.Equal("Message", (Assert.Throws<PropertyTypeDoesntMatchDefinitionException>(() => part.SetProperty("Message", 42))).InvalidPropertyName);
		}

		[Fact]
		public async Task MissingRequiredPropertyThrowsCorrectExceptionOnValidation() {
			var agg = Aggregate.Create("test");
			agg.AddProperty("Number", PropertyType.Integer, true);
			agg.AddProperty("GreetingMessage", PropertyType.String, true);
			var part = Part.Create(agg, "Part 1");
			part.SetProperty("Number", 42);
			// Note: No GreetingMessage
			Assert.Equal("GreetingMessage", (Assert.Throws<RequiredPropertyMissingException>(() => part.ValidateProperties())).MissingPropertyName);
		}

		[Fact]
		public async Task RequiredPropertyNullThrowsCorrectExceptionOnValidation() {
			var agg = Aggregate.Create("test");
			agg.AddProperty("Number", PropertyType.Integer, true);
			agg.AddProperty("GreetingMessage", PropertyType.String, true);
			var part = Part.Create(agg, "Part 1");
			part.SetProperty("Number", 42);
			Assert.Equal("GreetingMessage", (Assert.Throws<RequiredPropertyNullException>(() => part.SetProperty("GreetingMessage", null))).InvalidPropertyName);
		}

		[Fact]
		public async Task PropertiesWithJsonTypeCorrectlyRoundTripForOtherTypes() {
			var agg = Aggregate.Create("test");
			agg.AddProperty("Number", PropertyType.Json);
			agg.AddProperty("String", PropertyType.Json);
			agg.AddProperty("Date", PropertyType.Json);
			agg.AddProperty("Guid", PropertyType.Json);

			var date = DateTime.Today;
			var guid = Guid.NewGuid();
			var part = Part.Create(agg, "Part 1");
			part.SetProperty("Number", 1234);
			part.SetProperty("String", "Hello World");
			part.SetProperty("Date", date);
			part.SetProperty("Guid", guid);

			using (var context = createContext()) {
				context.Aggregates.Add(agg);
				context.Parts.Add(part);
				await context.SaveChangesAsync();
			}
			using (var context = createContext()) {
				var readPart = await context.Parts.Where(p => p.Label == "Part 1").Include(p => p.Properties).SingleOrDefaultAsync();
				Assert.Equal(1234, readPart.GetProperty("Number"));
				Assert.Equal("Hello World", readPart.GetProperty("String"));
				Assert.Equal(date.ToUniversalTime(), (readPart.GetProperty("Date") as DateTime?)?.ToUniversalTime());
				Assert.Equal(guid, readPart.GetProperty("Guid") as Guid?);
			}
		}

	}
}
