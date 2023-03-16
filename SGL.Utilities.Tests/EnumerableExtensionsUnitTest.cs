using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Tests {
	public class EnumerableExtensionsUnitTest {
		private ITestOutputHelper testLog;

		public EnumerableExtensionsUnitTest(ITestOutputHelper testLog) {
			this.testLog = testLog;
		}

		[Fact]
		public async Task MapBufferedAsyncCorrectlyDrivesTasksWithTheSpecifiedConcurrency() {
			int concurrencyCounter = 0;
			var output = Enumerable.Range(0, 8).MapBufferedAsync(4, async idx => {
				var concurrency = Interlocked.Increment(ref concurrencyCounter);
				await Task.Delay(500);
				Interlocked.Decrement(ref concurrencyCounter);
				return (idx, concurrency);
			});
			int idxExpected = 0;
			var concurrencyValues = new List<int>();
			await foreach (var (idxActual, concurrency) in output) {
				testLog.WriteLine($"{idxActual}: {concurrency} {concurrencyCounter}");
				Assert.InRange(concurrency, 1, 4);
				Assert.Equal(idxExpected++, idxActual);
				concurrencyValues.Add(concurrency);
			}
			Assert.Contains(concurrencyValues, c => c == 4);
		}
		[Fact]
		public async Task MapBufferedAsyncKeepsOrderingUnderVaryingTaskRuntimes() {
			var output = Enumerable.Range(0, 8).MapBufferedAsync(4, async idx => {
				await Task.Delay(800 - 100 * idx);
				testLog.WriteLine($"{idx} completed");
				return idx;
			});
			int idxExpected = 0;
			await foreach (var idxActual in output) {
				Assert.Equal(idxExpected++, idxActual);
			}
		}
		[Fact]
		public async Task MapBufferedAsyncTerminatesCleanlyOnAsyncExceptions() {
			int pendingCounter = 0;
			var taskStarted = new bool[16];
			var output = Enumerable.Range(0, 16).MapBufferedAsync(4, async idx => {
				taskStarted[idx] = true;
				if (idx == 6) {
					await Task.Delay(300);
					throw new Exception("Oops");
				}
				else {
					Interlocked.Increment(ref pendingCounter);
					await Task.Delay(800);
					Interlocked.Decrement(ref pendingCounter);
					return idx;
				}
			});
			var outputList = new List<int>();
			Exception? caughtException = null;
			try {
				await foreach (var idx in output) {
					testLog.WriteLine($"{idx} read");
					outputList.Add(idx);
				}
			}
			catch (Exception ex) {
				testLog.WriteLine(ex.ToString());
				caughtException = ex;
			}
			Assert.Equal(0, pendingCounter);
			Assert.Equal(Enumerable.Range(0, 6), outputList);
			Assert.NotNull(caughtException);
			Assert.Equal("Oops", caughtException.Message);
			Assert.False(taskStarted.Last());
			testLog.WriteLine("Started tasks: " + string.Join("", taskStarted.Select(s => s ? "Y" : "N")));
		}
		[Fact]
		public async Task MapBufferedAsyncTerminatesCleanlyOnSyncExceptions() {
			int pendingCounter = 0;
			var taskStarted = new bool[16];
			var output = Enumerable.Range(0, 16).MapBufferedAsync(4, idx => {
				taskStarted[idx] = true;
				if (idx == 6) {
					throw new Exception("Oops");
				}
				else {
					Interlocked.Increment(ref pendingCounter);
					return Task.Run(async () => {
						await Task.Delay(800);
						Interlocked.Decrement(ref pendingCounter);
						return idx;
					});
				}
			});
			var outputList = new List<int>();
			Exception? caughtException = null;
			try {
				await foreach (var idx in output) {
					testLog.WriteLine($"{idx} read");
					outputList.Add(idx);
				}
			}
			catch (Exception ex) {
				testLog.WriteLine(ex.ToString());
				caughtException = ex;
			}
			Assert.Equal(0, pendingCounter);
			Assert.Equal(Enumerable.Range(0, 6), outputList);
			Assert.NotNull(caughtException);
			Assert.Equal("Oops", caughtException.Message);
			Assert.False(taskStarted.Last());
			testLog.WriteLine("Started tasks: " + string.Join("", taskStarted.Select(s => s ? "Y" : "N")));
		}

		private class TestRefType { }

		[Fact]
		public void ToNullableRefsWorksWithReferenceTypes() {
			var origList = new List<TestRefType> { new TestRefType(), new TestRefType(), new TestRefType(), new TestRefType() };
			var nullableList = origList.ToNullableRefs().ToList();
			Assert.Equal(4, nullableList.Count);
			Assert.Same(origList[0], nullableList[0]);
			Assert.Same(origList[1], nullableList[1]);
			Assert.Same(origList[2], nullableList[2]);
			Assert.Same(origList[3], nullableList[3]);
		}

		private struct TestValType {
			public int Value;

			public TestValType(int value) {
				Value = value;
			}
		}

		[Fact]
		public void ToNullablesWorksWithStructs() {
			var origList = new List<TestValType> { new TestValType(123), new TestValType(234), new TestValType(345), new TestValType(456) };
			var nullableList = origList.ToNullables().ToList();
			Assert.Equal(4, nullableList.Count);
			Assert.True(nullableList[0].HasValue);
			Assert.Equal(origList[0].Value, nullableList[0]!.Value.Value);
			Assert.True(nullableList[1].HasValue);
			Assert.Equal(origList[1].Value, nullableList[1]!.Value.Value);
			Assert.True(nullableList[2].HasValue);
			Assert.Equal(origList[2].Value, nullableList[2]!.Value.Value);
			Assert.True(nullableList[3].HasValue);
			Assert.Equal(origList[3].Value, nullableList[3]!.Value.Value);
		}
		[Fact]
		public void ToNullablesWorksWithTuples() {
			var origList = new List<(int a, string b)> { (123, "123"), (234, "234"), (345, "345"), (456, "456"), };
			var nullableList = origList.ToNullables().ToList();
			Assert.Equal(4, nullableList.Count);
			Assert.True(nullableList[0].HasValue);
			Assert.Equal(origList[0], nullableList[0]!.Value);
			Assert.True(nullableList[1].HasValue);
			Assert.Equal(origList[1], nullableList[1]!.Value);
			Assert.True(nullableList[2].HasValue);
			Assert.Equal(origList[2], nullableList[2]!.Value);
			Assert.True(nullableList[3].HasValue);
			Assert.Equal(origList[3], nullableList[3]!.Value);
		}
	}
}
