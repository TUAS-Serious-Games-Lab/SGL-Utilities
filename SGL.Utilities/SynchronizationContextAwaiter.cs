using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides the extension method <see cref="GetAwaiter(SynchronizationContext?)"/> that enables awaiting a <see cref="SynchronizationContext"/> to easily switch execution to it.
	/// The pattern is based on https://thomaslevesque.com/2015/11/11/explicitly-switch-to-the-ui-thread-in-an-async-method/, but this implementation differs in two aspects:
	/// <list type="bullet">
	/// <item><description>
	/// The extension method takes a nullable parameter and the awaiter handles the null-case by running the completion using <see cref="Task.Run(Action)"/>.
	/// This allows awaiting a captured context without seperate null-checking, as when the target context is null, the execution is switched to the thread pool.
	/// Thus, this behavior matches the behavior of tasks that have a null <see cref="SynchronizationContext"/>.
	/// </description></item>
	/// <item><description>
	/// <see cref="SynchronizationContextAwaiter.GetResult"/> returns the original <see cref="SynchronizationContext"/> before switching.
	/// This allows code of the form:
	/// <code><![CDATA[
	/// DoSomething1(); // on context A
	/// var origContext = await uiSyncContext;
	/// DoSomethingOnUiThread(); // on context B
	/// await origContext;
	/// DoSomething2(); // on context A again
	/// ]]></code>
	/// </description></item>
	/// </list>
	/// </summary>
	public static class SynchronizationContextAwaitable {
		/// <summary>
		/// Returns an awaiter that continues execution on the given <paramref name="context"/> and returns the previous <see cref="SynchronizationContext"/> as the result of the await operation.
		/// </summary>
		/// <param name="context">The synchronization context to switch to, or null to let the execution continue on the thread pool.</param>
		/// <returns>The awaiter object for switching contexts.</returns>
		public static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext? context) {
			return new SynchronizationContextAwaiter(context);
		}
	}
	/// <summary>
	/// An awaiter that can be used to await a <see cref="SynchronizationContext"/> with the semantic of switching execution to it after the await expression.
	/// The previos context is returned to easily allow switching back.
	/// </summary>
	public class SynchronizationContextAwaiter : INotifyCompletion {
		private SynchronizationContext? context;
		private SynchronizationContext? originalContext;

		internal SynchronizationContextAwaiter(SynchronizationContext? context) {
			this.context = context;
			originalContext = SynchronizationContext.Current;
		}

		/// <summary>
		/// Returns true if we are already on the desired <see cref="SynchronizationContext"/>, or false if a context switch is required.
		/// </summary>
		public bool IsCompleted => SynchronizationContext.Current == context;
		/// <summary>
		/// Is called by the generated code for the await expression with a delegate that represents the steps after the switch, and that is to be called on the target context.
		/// </summary>
		/// <param name="continuation">The delegate to run on the target context.</param>
		public void OnCompleted(Action continuation) {
			if (context != null) {
				context.Post(s => ((Action)s!)(), continuation);
			}
			else {
				Task.Run(continuation);
			}
		}
		/// <summary>
		/// Returns the previous context as a result of the await expression, which allows an easier switch back.
		/// </summary>
		/// <returns>The previous context, before the context switch.</returns>
		public SynchronizationContext? GetResult() {
			return originalContext;
		}
	}
}
