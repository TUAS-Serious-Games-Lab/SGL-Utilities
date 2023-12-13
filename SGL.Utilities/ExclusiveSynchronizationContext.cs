using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a <see cref="SynchronizationContext"/> implementation that executes the callbacks one at a time.
	/// To run the <see cref="Post"/>ed callbacks, a backing <see cref="SynchronizationContext"/> is used.
	/// This implementation <see cref="SynchronizationContext.Post"/>s callbacks to this context that drive the exectuion of the callbacks in this implementation.
	/// They do so, by acquiring a lock and then processing all available callbacks one after the other.
	/// <see cref="Send"/> also acquires the lock and then first executes all pending callbacks before executing the given one.
	/// Thus, callbacks are also executed in insertion order (disregarding ordering of concurrent <see cref="Send"/> and <see cref="Post"/> operations).
	/// </summary>
	public class ExclusiveSynchronizationContext : SynchronizationContext {
		private readonly SynchronizationContext backingContext;
		private readonly object lockObj = new();
		private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();

		/// <summary>
		/// Creates a new exclusive synchronization context, that uses <see cref="SynchronizationContext.Current"/> as the backing context.
		/// If not current <see cref="SynchronizationContext"/> is set, a new instance of the <see cref="SynchronizationContext"/> base class is constructed,
		/// dispatching the backing callbacks to the thread pool.
		/// </summary>
		public ExclusiveSynchronizationContext() {
			backingContext = Current ?? new SynchronizationContext();
		}

		private ExclusiveSynchronizationContext(SynchronizationContext backingContext, object lockObj, ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue) {
			this.backingContext = backingContext;
			this.lockObj = lockObj;
			this.queue = queue;
		}

		/// <summary>
		/// Creates a flat copy of the context, that shares the queue and lock.
		/// Thus callbacks in the original and the copy are not executed concurrently and form a common ordering.
		/// </summary>
		/// <returns>The flat copy of the context.</returns>
		public override SynchronizationContext CreateCopy() {
			return new ExclusiveSynchronizationContext(backingContext.CreateCopy(), lockObj, queue);
		}

		/// <summary>
		/// Dispatches a synchronous callback to this synchronization context and waits for its execution.
		/// To preserve ordering, before running the given <paramref name="callback"/>, all pending other callbacks are executed within this call.
		/// </summary>
		/// <param name="callback">The callback to execute.</param>
		/// <param name="state">The state object to pass to the callback.</param>
		public override void Send(SendOrPostCallback callback, object? state) {
			lock (lockObj) {
				queue.Enqueue((callback, state));
				while (queue.TryDequeue(out var element)) {
					element.Callback(element.State);
				}
			}
		}

		/// <summary>
		/// Dispatches an asynchronous callback to this synchronization context.
		/// </summary>
		/// <param name="callback">The callback to queue and execute.</param>
		/// <param name="state">The state object to pass to the callback.</param>
		public override void Post(SendOrPostCallback callback, object? state) {
			queue.Enqueue((callback, state));
			backingContext.Post(s => (s as ExclusiveSynchronizationContext)?.Pump(), this);
		}

		private void Pump() {
			lock (lockObj) {
				while (queue.TryDequeue(out var element)) {
					element.Callback(element.State);
				}
			}
		}
	}
}
