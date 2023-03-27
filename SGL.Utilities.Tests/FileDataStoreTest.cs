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
	public class FileDataStoreTest {
		private const string TestFile = "FileDataStoreTest.json";
		private ILoggerFactory loggerFactory;

		public FileDataStoreTest(ITestOutputHelper output) {
			File.Delete(TestFile);
			loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(() => output).SetMinimumLevel(LogLevel.Trace));
		}

		public class TestData {
			public string Name { get; set; }
			public int Number { get; set; }
			public List<string> List { get; set; }

			public static async Task<TestData> DeserializeAsync(Stream stream, CancellationToken ct) {
				return await JsonSerializer.DeserializeAsync<TestData>(stream) ?? throw new Exception("Read null value");
			}
			public static Task SerializeAsync(Stream stream, TestData data, CancellationToken ct) {
				return JsonSerializer.SerializeAsync(stream, data);
			}
		}

		[Fact]
		public async Task StoredValueIsPresentAndCanBeReadBack() {
			var fds = new FileDataStore<TestData>(TestFile, TestData.DeserializeAsync, TestData.SerializeAsync);
			fds.Logger = loggerFactory.CreateLogger("Test");
			Assert.False(await fds.IsPresentAsync());
			await fds.StoreValueAsync(new TestData { Name = "John Doe", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fds.IsPresentAsync());
			var readValue = await fds.GetValueAsync();
			Assert.NotNull(readValue);
			Assert.Equal("John Doe", readValue.Name);
			Assert.Equal(12345, readValue.Number);
			Assert.Equal(new[] { "Hello", "World" }, readValue.List);
		}
		[Fact]
		public async Task ClearingValueMakesItNotPresentAndPreventsReading() {
			var fds = new FileDataStore<TestData>(TestFile, TestData.DeserializeAsync, TestData.SerializeAsync);
			fds.Logger = loggerFactory.CreateLogger("Test");
			await fds.StoreValueAsync(new TestData { Name = "John Doe", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fds.IsPresentAsync());
			await fds.ClearAsync();
			Assert.False(await fds.IsPresentAsync());
			var readValue = await fds.GetValueAsync();
			Assert.Null(readValue);
		}
		[Fact]
		public async Task StoredValueCanBeOverwritten() {
			var fds = new FileDataStore<TestData>(TestFile, TestData.DeserializeAsync, TestData.SerializeAsync);
			fds.Logger = loggerFactory.CreateLogger("Test");
			await fds.StoreValueAsync(new TestData { Name = "John Doe", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fds.IsPresentAsync());
			await fds.StoreValueAsync(new TestData { Name = "Jane Doe", Number = 54321, List = new List<string> { "This", "is", "a", "Test" } });
			var readValue = await fds.GetValueAsync();
			Assert.NotNull(readValue);
			Assert.Equal("Jane Doe", readValue.Name);
			Assert.Equal(54321, readValue.Number);
			Assert.Equal(new[] { "This", "is", "a", "Test" }, readValue.List);
		}
		[Fact]
		public async Task StoredValueCanBeUpdated() {
			var fds = new FileDataStore<TestData>(TestFile, TestData.DeserializeAsync, TestData.SerializeAsync);
			fds.Logger = loggerFactory.CreateLogger("Test");
			await fds.StoreValueAsync(new TestData { Name = "John Doe", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fds.IsPresentAsync());
			await fds.UpdateValueAsync(val => {
				val.Name += " Jr.";
				val.Number++;
				val.List.Add("!!");
			});
			var readValue = await fds.GetValueAsync();
			Assert.NotNull(readValue);
			Assert.Equal("John Doe Jr.", readValue.Name);
			Assert.Equal(12346, readValue.Number);
			Assert.Equal(new[] { "Hello", "World", "!!" }, readValue.List);
		}
		[Fact]
		public async Task FailedStoreLeavesOriginalValueIntact() {
			bool fail = false;
			var fds = new FileDataStore<TestData>(TestFile, TestData.DeserializeAsync, async (stream, val, ct) => {
				await TestData.SerializeAsync(stream, val, ct);
				if (fail) {
					throw new Exception("Whoops,something went wrong!");
				}
			});
			fds.Logger = loggerFactory.CreateLogger("Test");
			await fds.StoreValueAsync(new TestData { Name = "John Doe", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fds.IsPresentAsync());
			fail = true;
			await Assert.ThrowsAnyAsync<Exception>(async () => await fds.StoreValueAsync(new TestData { Name = "Jane Doe", Number = 54321, List = new List<string> { "This", "is", "a", "Test" } }));
			var readValue = await fds.GetValueAsync();
			Assert.NotNull(readValue);
			Assert.Equal("John Doe", readValue.Name);
			Assert.Equal(12345, readValue.Number);
			Assert.Equal(new[] { "Hello", "World" }, readValue.List);
		}
		[Fact]
		public async Task FailedUpdateLeavesOriginalValueIntact() {
			bool fail = false;
			var fds = new FileDataStore<TestData>(TestFile, TestData.DeserializeAsync, async (stream, val, ct) => {
				await TestData.SerializeAsync(stream, val, ct);
				if (fail) {
					throw new Exception("Whoops,something went wrong!");
				}
			});
			fds.Logger = loggerFactory.CreateLogger("Test");
			await fds.StoreValueAsync(new TestData { Name = "John Doe", Number = 12345, List = new List<string> { "Hello", "World" } });
			Assert.True(await fds.IsPresentAsync());
			fail = true;
			await Assert.ThrowsAnyAsync<Exception>(async () => await fds.UpdateValueAsync(val => {
				val.Name += " Jr.";
				val.Number++;
				val.List.Add("!!");
			}));
			var readValue = await fds.GetValueAsync();
			Assert.NotNull(readValue);
			Assert.Equal("John Doe", readValue.Name);
			Assert.Equal(12345, readValue.Number);
			Assert.Equal(new[] { "Hello", "World" }, readValue.List);
		}

	}
}
