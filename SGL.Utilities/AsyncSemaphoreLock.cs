using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a lock-like mechanism for asynchronous code that can be used to implement mutual exclusion of asynchronous operations.
	/// An asynchronous operation wishing to enter the critical section needs to <see langword="await"/> <see cref="WaitAsyncWithScopedRelease(CancellationToken)"/>
	/// and hold the returned handle object in a variable with <see langword="using"/>, the lifetime of which spans the critical section,
	/// to ensure that the lock is released again. After the handle is disposed, the lock is released and other operations can enter.
	/// </summary>
	public class AsyncSemaphoreLock : IDisposable {
		private SemaphoreSlim semaphore = new SemaphoreSlim(1);

		/// <summary>
		/// Asynchronously waits for the lock to be available, aquires it and returns a disposable object that releases the lock when disposed.
		/// It is important that the caller holds on to the handle for the whole critical section and disposes the handle after leaving the critical section.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the waiting.</param>
		/// <returns>The handle that must be disposed when leaving the critical section.</returns>
		public async Task<IDisposable> WaitAsyncWithScopedRelease(CancellationToken ct = default) {
			var handle = new LockHandle(semaphore);
			await semaphore.WaitAsync(ct);
			return handle;
		}

		internal class LockHandle : IDisposable {
			private SemaphoreSlim? semaphore;

			internal LockHandle(SemaphoreSlim semaphore) {
				this.semaphore = semaphore;
			}

			public void Dispose() {
				semaphore?.Release();
				semaphore = null;
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
