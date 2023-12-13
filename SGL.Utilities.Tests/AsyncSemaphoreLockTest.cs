using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Tests {
	public class AsyncSemaphoreLockTest {
		[Fact]
		public async Task LockCanBeTakenAndCorrectlyReleased() {
			var lck = new AsyncSemaphoreLock();
			{
				using var handle = await lck.WaitAsyncWithScopedRelease();
			}
			{
				using var handle = await lck.WaitAsyncWithScopedRelease();
			}
		}
		[Fact]
		public async Task LockCanBeRecursivelyTakenAndCorrectlyReleased() {
			var lck = new AsyncSemaphoreLock();
			{
				using var handle1 = await lck.WaitAsyncWithScopedRelease();
				{
					using var handle2 = await lck.WaitAsyncWithScopedRelease();
					{
						using var handle3 = await lck.WaitAsyncWithScopedRelease();
					}
				}
			}
			{
				using var handle1 = await lck.WaitAsyncWithScopedRelease();
				{
					using var handle2 = await lck.WaitAsyncWithScopedRelease();
				}
			}
		}
		[Fact]
		public async Task LockProvidesMutalExclusionForCriticalSection() {
			var lck = new AsyncSemaphoreLock();
			var operationOrder = new StringBuilder();

			static async Task testOperation(AsyncSemaphoreLock l, StringBuilder opOrder, char opId, bool waitBefore, int intenalDelay) {
				if (waitBefore) {
					await Task.Delay(100);
				}
				using (var handle = await l.WaitAsyncWithScopedRelease()) {
					opOrder.Append(opId);
					await Task.Delay(intenalDelay);
					opOrder.Append(opId);
				}
			}
			{
				var taskA = testOperation(lck, operationOrder, 'A', true, 100);
				var ec = ExecutionContext.Capture();
				var taskB = testOperation(lck, operationOrder, 'B', true, 25);
				await taskA;
				await taskB;
				Assert.True(operationOrder.ToString() is "AABB" or "BBAA");
			}
			operationOrder.Clear();
			{
				var taskA = testOperation(lck, operationOrder, 'A', false, 100);
				var ec = ExecutionContext.Capture();
				var taskB = testOperation(lck, operationOrder, 'B', false, 25);
				await taskA;
				await taskB;
				Assert.True(operationOrder.ToString() is "AABB" or "BBAA");
			}
		}
		[Fact]
		public async Task LockProvidesMutalExclusionForCriticalSectionWithRecursion() {
			var lck = new AsyncSemaphoreLock();
			var operationOrder = new StringBuilder();
			async Task testOperation(AsyncSemaphoreLock l, StringBuilder opOrder, char opId, bool waitBefore, int intenalDelay, int recursionCounter = 3) {
				if (waitBefore) {
					await Task.Delay(5);
				}
				using (var handle = await lck.WaitAsyncWithScopedRelease()) {
					operationOrder.Append(opId);
					await Task.Delay(intenalDelay);
					if (recursionCounter > 0) {
						await testOperation(l, opOrder, opId, waitBefore, intenalDelay, recursionCounter - 1);
					}
					await Task.Delay(intenalDelay);
					operationOrder.Append(opId);
				}
			}
			{
				var taskA = testOperation(lck, operationOrder, 'A', true, 10);
				var ec = ExecutionContext.Capture();
				var taskB = testOperation(lck, operationOrder, 'B', true, 2);
				await taskA;
				await taskB;
				Assert.True(operationOrder.ToString() is "AAAAAAAABBBBBBBB" or "BBBBBBBBAAAAAAAA");
			}
			operationOrder.Clear();
			{
				var taskA = testOperation(lck, operationOrder, 'A', false, 10);
				var ec = ExecutionContext.Capture();
				var taskB = testOperation(lck, operationOrder, 'B', false, 2);
				await taskA;
				await taskB;
				Assert.True(operationOrder.ToString() is "AAAAAAAABBBBBBBB" or "BBBBBBBBAAAAAAAA");
			}
		}
	}
}
