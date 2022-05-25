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
	/// </summary>
	public class SingleThreadedSynchronizationContext : SynchronizationContext, IDisposable {
		private readonly Thread thread;
		private readonly AutoResetEvent resetEvent = new AutoResetEvent(false);
		private readonly CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
		private readonly CancellationToken shutdownToken;
		private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue = new ConcurrentQueue<(SendOrPostCallback Callback, object? State)>();

		/// <summary>
		/// Creates a new <see cref="SingleThreadedSynchronizationContext"/> with its own thread.
		/// </summary>
		/// <param name="threadName">
		/// Allows specifying a custom name for the thread that will run this context's callbacks.
		/// By default, the class name <see cref="SingleThreadedSynchronizationContext"/> is used.
		/// This is mainly relevant as a marker where the current code is runnning, e.g. for debugging purposes.
		/// </param>
		public SingleThreadedSynchronizationContext(string? threadName = nameof(SingleThreadedSynchronizationContext)) {
			shutdownToken = shutdownTokenSource.Token;
			thread = new Thread(pump);
			thread.Name = threadName;
			thread.Start();
		}

		private SingleThreadedSynchronizationContext(Thread thread, AutoResetEvent resetEvent, CancellationTokenSource shutdownTokenSource,
			CancellationToken shutdownToken, ConcurrentQueue<(SendOrPostCallback Callback, object? State)> queue) {
			this.thread = thread;
			this.resetEvent = resetEvent;
			this.shutdownTokenSource = shutdownTokenSource;
			this.shutdownToken = shutdownToken;
			this.queue = queue;
		}

		/// <summary>
		/// Creates a flat copy of the context, that shares the queue and the worker thread.
		/// Thus callbacks in the original and the copy are not executed concurrently and form a common ordering.
		/// </summary>
		/// <returns>The flat copy of the context.</returns>
		public override SynchronizationContext CreateCopy() {
			return new SingleThreadedSynchronizationContext(thread, resetEvent, shutdownTokenSource, shutdownToken, queue);
		}

		/// <summary>
		/// Tells the worker thread to shut down after executing the currently queued callbacks and waits for the shutdown to complete.
		/// </summary>
		public void Dispose() {
			shutdownTokenSource.Cancel();
			if (Thread.CurrentThread == thread) {
				process();
			}
			else {
				resetEvent.Set();
				thread.Join();
			}
			resetEvent.Dispose();
			shutdownTokenSource.Dispose();
		}

		/// <summary>
		/// Dispatches an asynchronous callback to this synchronization context, that will be executed on the worker thread.
		/// </summary>
		/// <param name="callback">The callback to queue and execute.</param>
		/// <param name="state">The state object to pass to the callback.</param>
		public override void Post(SendOrPostCallback callback, object? state) {
			queue.Enqueue((callback, state));
			resetEvent.Set();
		}

		/// <summary>
		/// Throws <see cref="NotSupportedException"/>, as synchronus callbacks are currently not supported.
		/// </summary>
		/// <param name="callback">ignored</param>
		/// <param name="state">ignored</param>
		public override void Send(SendOrPostCallback callback, object? state) {
			throw new NotSupportedException();
		}

		private void pump() {
			SetSynchronizationContext(this);
			while (!shutdownToken.IsCancellationRequested) {
				resetEvent.WaitOne();
				process();
			}
		}

		private void process() {
			while (queue.TryDequeue(out var element)) {
				element.Callback(element.State);
			}
		}
	}
}
