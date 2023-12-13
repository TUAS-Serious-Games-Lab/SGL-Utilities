using Microsoft.Extensions.Logging;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Tests {
	public class FileDataMapTest {
		private const string TestDir = "./FileDataMapTest";
		private ILoggerFactory loggerFactory;

		public FileDataMapTest(ITestOutputHelper output) {
			if (Directory.Exists(TestDir)) {
				Directory.Delete(TestDir, recursive: true);
			}
			loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(() => output).SetMinimumLevel(LogLevel.Trace));
		}

		public class TestData {
			public string Name { get; set; }
			public int Number { get; set; }
			public List<string> List { get; set; }

			public static async Task<TestData> DeserializeAsync(Stream stream, CancellationToken ct) {
				return await JsonSerializer.DeserializeAsync<TestData>(stream, cancellationToken: ct) ?? throw new Exception("Read null value");
			}
			public static Task SerializeAsync(Stream stream, TestData data, CancellationToken ct) {
				return JsonSerializer.SerializeAsync(stream, data, cancellationToken: ct);
			}
		}

		public class TestKey {
			public string Level1 { get; set; }
			public string Level2 { get; set; }

			public override string? ToString() {
				return $"{Level1}/{Level2}";
			}
		}

		private static string MapKey(TestKey key) {
			return $"{key.Level1}/{key.Level2}.json";
		}

		[Fact]
		public async Task StoredValuesIsPresentAndCanBeReadBack() {
			var fdm = new FileDataMap<TestKey, TestData>(TestDir, TestData.DeserializeAsync, TestData.SerializeAsync, MapKey);
			fdm.Logger = loggerFactory.CreateLogger("Test");
			var key1 = new TestKey { Level1 = "abc", Level2 = "bcd" };
			var key2 = new TestKey { Level1 = "xyz", Level2 = "wxy" };

			Assert.False(await fdm.IsPresentAsync(key1));
			Assert.False(await fdm.IsPresentAsync(key2));
			await fdm.StoreValueAsync(key1, new TestData { Name = "Alice", Number = 12345, List = new List<string> { "Hello", "World" } });
			await fdm.StoreValueAsync(key2, new TestData { Name = "Bob", Number = 23456, List = new List<string> { "This", "is", "a", "test" } });
			Assert.True(await fdm.IsPresentAsync(key1));
			Assert.True(await fdm.IsPresentAsync(key2));
			var readValue1 = await fdm.GetValueAsync(key1);
			var readValue2 = await fdm.GetValueAsync(key2);
			Assert.NotNull(readValue1);
			Assert.NotNull(readValue2);
			Assert.Equal("Alice", readValue1.Name);
			Assert.Equal("Bob", readValue2.Name);
			Assert.Equal(12345, readValue1.Number);
			Assert.Equal(23456, readValue2.Number);
			Assert.Equal(new[] { "Hello", "World" }, readValue1.List);
			Assert.Equal(new[] { "This", "is", "a", "test" }, readValue2.List);
		}
		[Fact]
		public async Task RemovingValuesMakesThemNotPresentAndPreventsReadingBack() {
			var fdm = new FileDataMap<TestKey, TestData>(TestDir, TestData.DeserializeAsync, TestData.SerializeAsync, MapKey);
			fdm.Logger = loggerFactory.CreateLogger("Test");
			var key1 = new TestKey { Level1 = "abc", Level2 = "bcd" };
			var key2 = new TestKey { Level1 = "xyz", Level2 = "wxy" };

			await fdm.StoreValueAsync(key1, new TestData { Name = "Alice", Number = 12345, List = new List<string> { "Hello", "World" } });
			await fdm.StoreValueAsync(key2, new TestData { Name = "Bob", Number = 23456, List = new List<string> { "This", "is", "a", "test" } });
			Assert.True(await fdm.IsPresentAsync(key1));
			Assert.True(await fdm.IsPresentAsync(key2));
			await fdm.RemoveAsync(key1);
			Assert.False(await fdm.IsPresentAsync(key1));
			var readValue1 = await fdm.GetValueAsync(key1);
			Assert.Null(readValue1);
			Assert.True(await fdm.IsPresentAsync(key2));
			await fdm.RemoveAsync(key2);
			Assert.False(await fdm.IsPresentAsync(key2));
			var readValue2 = await fdm.GetValueAsync(key2);
			Assert.Null(readValue2);
		}
		[Fact]
		public async Task StoredValuesCanBeOverwritten() {
			var fdm = new FileDataMap<TestKey, TestData>(TestDir, TestData.DeserializeAsync, TestData.SerializeAsync, MapKey);
			fdm.Logger = loggerFactory.CreateLogger("Test");
			var key1 = new TestKey { Level1 = "abc", Level2 = "bcd" };
			var key2 = new TestKey { Level1 = "xyz", Level2 = "wxy" };

			Assert.False(await fdm.IsPresentAsync(key1));
			Assert.False(await fdm.IsPresentAsync(key2));
			await fdm.StoreValueAsync(key1, new TestData { Name = "Alice", Number = 12345, List = new List<string> { "Hello", "World" } });
			await fdm.StoreValueAsync(key2, new TestData { Name = "Bob", Number = 23456, List = new List<string> { "This", "is", "a", "test" } });
			Assert.True(await fdm.IsPresentAsync(key1));
			Assert.True(await fdm.IsPresentAsync(key2));
			await fdm.StoreValueAsync(key1, new TestData { Name = "Carol", Number = 34567, List = new List<string> { "Test", "Test" } });
			await fdm.StoreValueAsync(key2, new TestData { Name = "Dave", Number = 45678, List = new List<string> { "Hello World!" } });
			var readValue1 = await fdm.GetValueAsync(key1);
			var readValue2 = await fdm.GetValueAsync(key2);
			Assert.NotNull(readValue1);
			Assert.NotNull(readValue2);
			Assert.Equal("Carol", readValue1.Name);
			Assert.Equal("Dave", readValue2.Name);
			Assert.Equal(34567, readValue1.Number);
			Assert.Equal(45678, readValue2.Number);
			Assert.Equal(new[] { "Test", "Test" }, readValue1.List);
			Assert.Equal(new[] { "Hello World!" }, readValue2.List);
		}
		[Fact]
		public async Task StoredValueCanBeUpdated() {
			var fdm = new FileDataMap<TestKey, TestData>(TestDir, TestData.DeserializeAsync, TestData.SerializeAsync, MapKey);
			fdm.Logger = loggerFactory.CreateLogger("Test");
			var key1 = new TestKey { Level1 = "abc", Level2 = "bcd" };
			var key2 = new TestKey { Level1 = "xyz", Level2 = "wxy" };

			Assert.False(await fdm.IsPresentAsync(key1));
			Assert.False(await fdm.IsPresentAsync(key2));
			await fdm.StoreValueAsync(key1, new TestData { Name = "Alice", Number = 12345, List = new List<string> { "Hello", "World" } });
			await fdm.StoreValueAsync(key2, new TestData { Name = "Bob", Number = 23456, List = new List<string> { "This", "is", "a", "test" } });
			Assert.True(await fdm.IsPresentAsync(key1));
			Assert.True(await fdm.IsPresentAsync(key2));
			await fdm.UpdateValueAsync(key1, data => {
				data.Name = new string(data.Name.Reverse().ToArray());
				data.Number++;
				data.List.Add("!!");
			});
			await fdm.UpdateValueAsync(key2, data => {
				data.Name = "Dave";
				data.List.Add("!!!");
			});
			var readValue1 = await fdm.GetValueAsync(key1);
			var readValue2 = await fdm.GetValueAsync(key2);
			Assert.NotNull(readValue1);
			Assert.NotNull(readValue2);
			Assert.Equal("ecilA", readValue1.Name);
			Assert.Equal("Dave", readValue2.Name);
			Assert.Equal(12346, readValue1.Number);
			Assert.Equal(23456, readValue2.Number);
			Assert.Equal(new[] { "Hello", "World", "!!" }, readValue1.List);
			Assert.Equal(new[] { "This", "is", "a", "test", "!!!" }, readValue2.List);
		}
		[Fact]
		public async Task FailedStoreLeavesOriginalValueIntact() {
			bool fail = false;
			var fdm = new FileDataMap<TestKey, TestData>(TestDir, TestData.DeserializeAsync, async (stream, val, ct) => {
				await TestData.SerializeAsync(stream, val, ct);
				if (fail) {
					throw new Exception("Whoops,something went wrong!");
				}
			}, MapKey);
			fdm.Logger = loggerFactory.CreateLogger("Test");
			var key = new TestKey { Level1 = "abc", Level2 = "bcd" };

			Assert.False(await fdm.IsPresentAsync(key));
			await fdm.StoreValueAsync(key, new TestData { Name = "Alice", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fdm.IsPresentAsync(key));
			fail = true;
			await Assert.ThrowsAnyAsync<Exception>(async () => await fdm.StoreValueAsync(key, new TestData { Name = "Carol", Number = 34567, List = new List<string> { "Test", "Test" } }));
			var readValue1 = await fdm.GetValueAsync(key);
			Assert.NotNull(readValue1);
			Assert.Equal("Alice", readValue1.Name);
			Assert.Equal(12345, readValue1.Number);
			Assert.Equal(new[] { "Hello", "World" }, readValue1.List);
		}
		[Fact]
		public async Task FailedUpdateLeavesOriginalValueIntact() {
			bool fail = false;
			var fdm = new FileDataMap<TestKey, TestData>(TestDir, TestData.DeserializeAsync, async (stream, val, ct) => {
				await TestData.SerializeAsync(stream, val, ct);
				if (fail) {
					throw new Exception("Whoops,something went wrong!");
				}
			}, MapKey);
			fdm.Logger = loggerFactory.CreateLogger("Test");
			var key = new TestKey { Level1 = "abc", Level2 = "bcd" };

			Assert.False(await fdm.IsPresentAsync(key));
			await fdm.StoreValueAsync(key, new TestData { Name = "Alice", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fdm.IsPresentAsync(key));
			fail = true;
			await Assert.ThrowsAnyAsync<Exception>(async () => await fdm.UpdateValueAsync(key, data => {
				data.Name = new string(data.Name.Reverse().ToArray());
				data.Number++;
				data.List.Add("!!");
			}));
			var readValue1 = await fdm.GetValueAsync(key);
			Assert.NotNull(readValue1);
			Assert.Equal("Alice", readValue1.Name);
			Assert.Equal(12345, readValue1.Number);
			Assert.Equal(new[] { "Hello", "World" }, readValue1.List);
		}

	}
}
