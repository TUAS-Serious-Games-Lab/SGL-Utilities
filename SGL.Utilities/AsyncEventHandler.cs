using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// An event handler delegate type that supports asynchronously executing handlers and thus expects handlers to return a <see cref="Task"/> object.
	/// Events of this type should be invoked using <see cref="AsyncEventHandlerExtensions.InvokeAllAsync{TEventArgs}(AsyncEventHandler{TEventArgs}, object?, TEventArgs, CancellationToken)"/>
	/// to obain a task object that can be used to await all handlers.
	/// </summary>
	/// <typeparam name="TEventArgs">A type representing event data.</typeparam>
	/// <param name="sender">The object that triggered the event.</param>
	/// <param name="e">The associated data for the event.</param>
	/// <param name="ct">A cancellation token to allow the event triggering code to cancel the handler.</param>
	/// <returns>A task representing the asynchronous execution of the handler.</returns>
	public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e, CancellationToken ct);

	/// <summary>
	/// Provides the <see cref="InvokeAllAsync{TEventArgs}(AsyncEventHandler{TEventArgs}, object?, TEventArgs, CancellationToken)"/> extension method.
	/// </summary>
	public static class AsyncEventHandlerExtensions {
		/// <summary>
		/// Invokes all registered handlers of the given <see cref="AsyncEventHandler{TEventArgs}"/> with the given arguments and provides a task object that represents the completion of all handlers.
		/// </summary>
		/// <typeparam name="TEventArgs">A type representing event data.</typeparam>
		/// <param name="handler">The event handler delegate of which to invoke the registered handlers.</param>
		/// <param name="sender">The object that triggered the event.</param>
		/// <param name="e">The associated data for the event.</param>
		/// <param name="ct">The cancellation token to pass to the handlers.</param>
		/// <returns>A task object that represents the completion of all handlers, that can be awaited to wait for the effects of the handlers.</returns>
		public static Task InvokeAllAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> handler, object? sender, TEventArgs e, CancellationToken ct = default) {
			var tasks = handler?.GetInvocationList()?.Select(callback => callback.DynamicInvoke(sender, e, ct))?.OfType<Task>()?.ToList();
			return Task.WhenAll(tasks ?? Enumerable.Empty<Task>());
		}
	}
}
