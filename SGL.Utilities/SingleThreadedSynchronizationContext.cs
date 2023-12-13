using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a <see cref="SynchronizationContext"/> implementation that uses a single designated thread to execute the posted callbacks one at a time and
	/// in <see cref="Post"/>ing order (disregarding ordering of concurrent <see cref="Post"/> operations).
	/// To ensure proper shutdown of the designated worker thread, the context should be disposed.
	/// </summary>
	public class SingleThreadedSynchronizationContext : SynchronizationContext, IDisposable {
		private class State : IDisposable {
			private readonly Action<Exception> uncaughtExceptionCallback;
			private readonly Thread thread;
			private readonly AutoResetEvent resetEvent = new(false);
			private readonly CancellationTokenSource shutdownTokenSource = new();
			private readonly CancellationToken shutdownToken;
			private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new();
			private long refCount = 1;

			internal State(Action<Exception> uncaughtExceptionCallback, string threadName, SingleThreadedSynchronizationContext ctx) {
				this.uncaughtExceptionCallback = uncaughtExceptionCallback;
				shutdownToken = shutdownTokenSource.Token;
				thread = new Thread(() => Pump(ctx)) {
					Name = threadName
				};
				thread.Start();
			}
			internal void Pump(SingleThreadedSynchronizationContext ctx) {
				SetSynchronizationContext(ctx);
				while (!shutdownToken.IsCancellationRequested) {
					resetEvent.WaitOne();
					Process();
				}
			}

			internal void Process() {
				while (queue.TryDequeue(out var element)) {
					Execute(element.Callback, element.State);
				}
			}
			public void Dispose() {
				shutdownTokenSource.Cancel();
				if (Environment.CurrentManagedThreadId == thread.ManagedThreadId) {
					Process();
				}
				else {
					resetEvent.Set();
					thread.Join();
				}
				resetEvent.Dispose();
				shutdownTokenSource.Dispose();
			}

			internal void AddRef() {
				if (Interlocked.Increment(ref refCount) == 1) {
					Interlocked.Decrement(ref refCount);
					throw new ObjectDisposedException("The synchronization context was already disposed or is currently being disposed and can't be copied.");
				}
			}

			internal void DropRef() {
				if (Interlocked.Decrement(ref refCount) == 0) {
					Dispose();
				}
			}

			internal long RefCount {
				get {
					return Interlocked.Read(ref refCount);
				}
			}

			public void Enqueue(SendOrPostCallback callback, object? state) {
				if (RefCount < 1) {
					throw new ObjectDisposedException("The synchronization context was already disposed or is currently being disposed and can't receive new callbacks.");
				}
				queue.Enqueue((callback, state));
				resetEvent.Set();
			}

			public bool IsCurrentThreadWorker => Environment.CurrentManagedThreadId == thread.ManagedThreadId;

			public void Execute(SendOrPostCallback callback, object? state) {
				try {
					callback(state);
				}
				catch (Exception ex) {
					uncaughtExceptionCallback(ex);
				}
			}
		}
		private readonly State internalState;

		/// <summary>
		/// Creates a new <see cref="SingleThreadedSynchronizationContext"/> with its own thread.
		/// </summary>
		/// <param name="uncaughtExceptionCallback">A delegate that is invoked on the worker thread if an exception escapes from a callback.</param>
		/// <param name="threadName">
		/// Allows specifying a custom name for the thread that will run this context's callbacks.
		/// By default, the class name <see cref="SingleThreadedSynchronizationContext"/> is used.
		/// This is mainly relevant as a marker where the current code is runnning, e.g. for debugging purposes.
		/// </param>
		public SingleThreadedSynchronizationContext(Action<Exception> uncaughtExceptionCallback, string threadName = nameof(SingleThreadedSynchronizationContext)) {
			internalState = new State(uncaughtExceptionCallback, threadName, this);
		}

		private SingleThreadedSynchronizationContext(State internalState) {
			internalState.AddRef();
			this.internalState = internalState;
		}

		/// <summary>
		/// Creates a flat copy of the context, that shares the queue and the worker thread.
		/// Thus callbacks in the original and the copy are not executed concurrently and form a common ordering.
		/// </summary>
		/// <returns>The flat copy of the context.</returns>
		public override SynchronizationContext CreateCopy() {
			return new SingleThreadedSynchronizationContext(internalState);
		}

		/// <summary>
		/// If this is the last undisposed context using the worker thread, tells the worker thread to shut down after executing the currently queued callbacks
		/// and then waits for the shutdown to complete.
		/// </summary>
		public void Dispose() {
			internalState.DropRef();
		}

		/// <summary>
		/// Dispatches an asynchronous callback to this synchronization context, that will be executed on the worker thread.
		/// </summary>
		/// <param name="callback">The callback to queue and execute.</param>
		/// <param name="state">The state object to pass to the callback.</param>
		public override void Post(SendOrPostCallback callback, object? state) {
			internalState.Enqueue(callback, state);
		}

		/// <summary>
		/// Dispatches a synchronous callback to this synchronization context and waits for its execution.
		/// This operation respects the ordering of callbacks and thus only executes the given <paramref name="callback"/> when previously enqued ones have completed.
		/// </summary>
		/// <param name="callback">The callback to execute.</param>
		/// <param name="state">The state object to pass to the callback.</param>
		/// <remarks>
		/// Note that this operation is relatively expensive when called from a thread that isn't the worker thread,
		/// as a completion event needs to be allocated and cleaned up, and the callback needs to be wrapped in a delegate that notifies the completion.
		/// </remarks>
		public override void Send(SendOrPostCallback callback, object? state) {
			if (internalState.IsCurrentThreadWorker) {
				internalState.Process();
				internalState.Execute(callback, state);
			}
			else {
				using var finishedEvent = new ManualResetEventSlim(false);
				// Wrap callback in a delegate that notifies the event for this operation after completion.
				internalState.Enqueue(s => {
					internalState.Execute(callback, s);
					finishedEvent.Set();
				}, state);
				// Wait for the delegate to be completed.
				finishedEvent.Wait();
			}
		}
	}
}
