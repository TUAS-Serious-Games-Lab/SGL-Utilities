using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a lock-like mechanism for asynchronous code that can be used to implement mutual exclusion of asynchronous operations.
	/// An asynchronous operation wishing to enter the critical section needs to <see langword="await"/> <see cref="WaitAsyncWithScopedRelease(CancellationToken)"/>
	/// and hold the returned handle object in a variable with <see langword="using"/>, the lifetime of which spans the critical section,
	/// to ensure that the lock is released again. After the handle is disposed, the lock is released and other operations can enter.
	///
	/// Recursive use is supported. I.e. an asynchronous operation can call <see cref="WaitAsyncWithScopedRelease(CancellationToken)"/> again while it already holds the lock
	/// without deadlocking by waiting for itself. If an operations does this, all returned handles must be disposed to correctly release the lock again.
	/// However, note that when an outer async operation obtains the lock and then forks multiple async operations concurrently that use the same lock,
	/// those operations are NOT mutally excluded against eachother because they are already inside the critical section.
	/// Thus, the inner operations would need their own synchronization mechanism.
	/// </summary>
	public class AsyncSemaphoreLock : IDisposable {
		private SemaphoreSlim semaphore = new SemaphoreSlim(1);
		private AsyncLocal<Context> currentOperationHoldsLock = new AsyncLocal<Context>(); // Context with counter instead of bool to support recursive use

		private class Context {
			internal int RecursionLevel { get; set; } = 0;
		}

		private Context GetContext() {
			return currentOperationHoldsLock.Value ??= new Context();
		}

		/// <summary>
		/// Asynchronously waits for the lock to be available, aquires it and returns a disposable object that releases the lock when disposed.
		/// It is important that the caller holds on to the handle for the whole critical section and disposes the handle after leaving the critical section.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the waiting.</param>
		/// <returns>The handle that must be disposed when leaving the critical section.</returns>
		public Task<IDisposable> WaitAsyncWithScopedRelease(CancellationToken ct = default) {
			// Ensure there is a context installed here
			// This needs to happen in this synchronous wrapper around the actual async method,
			// because entering the async method copies the ExecutionContext and thus a context installed
			// inside the async method would not be present outside. Because the context needs to be present
			// when the LockHandle is disposed, it must however be installed in the callers context and therefore
			// this wrapper is needed.
			// If the lock is used recursively, on the second level, there is already a context installed here and
			// only the counter is manipulated.
			var ctx = GetContext();
			return WaitAsyncWithScopedReleaseInner(ctx, ct);
		}
		private async Task<IDisposable> WaitAsyncWithScopedReleaseInner(Context ctx, CancellationToken ct = default) {
			var handle = new LockHandle(this);
			if (ctx.RecursionLevel > 0) {
				ctx.RecursionLevel++;
			}
			else {
				await semaphore.WaitAsync(ct);
				ctx.RecursionLevel = 1;
			}
			return handle;
		}

		internal class LockHandle : IDisposable {
			private AsyncSemaphoreLock? heldLock;

			internal LockHandle(AsyncSemaphoreLock heldLock) {
				this.heldLock = heldLock;
			}

			public void Dispose() {
				heldLock?.Release();
				heldLock = null;
			}
		}

		private void Release() {
			var ctx = GetContext();
			if (ctx.RecursionLevel > 1) {
				ctx.RecursionLevel--;
			}
			else if (ctx.RecursionLevel == 1) {
				ctx.RecursionLevel--;
				semaphore.Release();
			}
			else {
				throw new InvalidOperationException("Can't release a lock that the current operation doesn't hold.");
			}
		}

		/// <summary>
		/// Disposes the underlying semaphore object.
		/// </summary>
		public void Dispose() {
			semaphore.Dispose();
		}
	}
}
