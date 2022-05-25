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
	/// The <see cref="Send"/> operation is currently not supported.
	/// To ensure proper shutdown of the designated worker thread, the context should be disposed.
	/// </summary>
	public class SingleThreadedSynchronizationContext : SynchronizationContext, IDisposable {
		private class State : IDisposable {
			private Action<Exception> uncaughtExceptionCallback;
			private readonly Thread thread;
			private readonly AutoResetEvent resetEvent = new AutoResetEvent(false);
			private readonly CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
			private readonly CancellationToken shutdownToken;
			private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new ConcurrentQueue<(SendOrPostCallback Callback, object? State)>();
			private long refCount = 1;

			internal State(Action<Exception> uncaughtExceptionCallback, string threadName, SingleThreadedSynchronizationContext ctx) {
				this.uncaughtExceptionCallback = uncaughtExceptionCallback;
				shutdownToken = shutdownTokenSource.Token;
				thread = new Thread(() => pump(ctx));
				thread.Name = threadName;
				thread.Start(this);
			}
			internal void pump(SingleThreadedSynchronizationContext ctx) {
				SetSynchronizationContext(ctx);
				while (!shutdownToken.IsCancellationRequested) {
					resetEvent.WaitOne();
					process();
				}
			}

			private void process() {
				while (queue.TryDequeue(out var element)) {
					execute(element.Callback, element.State);
				}
			}
			public void Dispose() {
				shutdownTokenSource.Cancel();
				if (Thread.CurrentThread.ManagedThreadId == thread.ManagedThreadId) {
					process();
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

			public void execute(SendOrPostCallback callback, object? state) {
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
		/// Throws <see cref="NotSupportedException"/>, as synchronus callbacks are currently not supported.
		/// </summary>
		/// <param name="callback">ignored</param>
		/// <param name="state">ignored</param>
		public override void Send(SendOrPostCallback callback, object? state) {
			throw new NotSupportedException();
		}
	}
}
