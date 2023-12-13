using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit.Abstractions;

namespace SGL.Utilities.TestUtilities.XUnit {

	/// <summary>
	/// An <see cref="ILoggerProvider"/> implementation that outputs the log messages to a <see cref="ITestOutputHelper"/> to provide log output for test cases.
	/// </summary>
	public class XUnitLoggingProvider : ILoggerProvider {
		private readonly Func<ITestOutputHelper?> outputObtainer;
		private readonly Configurator configuration = new();
		private static readonly ThreadLocal<StringBuilder> cachedStringBuilder = new(() => new StringBuilder());
		private readonly LoggerExternalScopeProvider scopes = new();

		internal class XUnitLogger : ILogger {
			private readonly Func<ITestOutputHelper?> outputObtainer;
			private readonly string categoryName;
			private readonly LoggerExternalScopeProvider scopes;
			private readonly Configurator configuration;

			internal XUnitLogger(Func<ITestOutputHelper?> outputObtainer, string categoryName, LoggerExternalScopeProvider scopes, Configurator configuration) {
				this.outputObtainer = outputObtainer;
				this.categoryName = categoryName;
				this.scopes = scopes;
				this.configuration = configuration;
			}

			public IDisposable BeginScope<TState>(TState state) {
				return scopes.Push(state);
			}

			public bool IsEnabled(LogLevel logLevel) {
				return logLevel < LogLevel.None;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
				var output = outputObtainer();
				if (output is null) return;
				StringBuilder builder = cachedStringBuilder.Value ??= new StringBuilder();
				builder.Clear();
				if (configuration.Tags.Any()) {
					builder.AppendFormat("{0:G} [{1}]", logLevel, categoryName);
					builder.Append(" (");
					bool first = true;
					foreach (var tag in configuration.Tags) {
						if (first) {
							first = false;
						}
						else {
							builder.Append(", ");
						}
						builder.Append(tag);
					}
					builder.Append(") ");
					builder.Append(formatter(state, exception));
				}
				else {
					builder.AppendFormat("{0:G} [{1}] {2}", logLevel, categoryName, formatter(state, exception));
				}
				if (exception != null) {
					builder.Append("\n Exception: ");
					builder.Append(exception);
					builder.AppendLine();
				}
				builder.Append(' ');
				scopes.ForEachScope((scope, sb) => sb.AppendFormat("<{0}>", scope), builder);
				var str = builder.ToString();
				lock (output) {
					try {
						output.WriteLine(str);
					}
					catch (Exception) {
						// Couldn't write to output. This error will be ignored, because there is nothing else to do.
						// This may happen if something logs during disposal and output is not assigned to a test anymore.
					}
				}
			}
		}

		/// <summary>
		/// Constructs a <see cref="XUnitLoggingProvider"/> that uses the given delegate to obtain the <see cref="ITestOutputHelper"/> for the current test case.
		/// </summary>
		/// <param name="outputObtainer">A delegate to get the output helper of the test case.</param>
		/// <param name="config">Allows additional options to be configured.</param>
		/// <remarks>
		/// In many cases, the <paramref name="outputObtainer"/> delegate can simply return the appropriate dependency-injected variable,
		/// it is however necessary to use a delegate instead of just passing a reference to the output helper to support having the logging provider in a fixture object.
		/// There, the current test case needs to set a variable in the fixture to its output helper and the logging provider needs to look for the current value of that variable.
		/// </remarks>
		public XUnitLoggingProvider(Func<ITestOutputHelper?> outputObtainer, Action<IXUnitLoggingConfigurator>? config = null) {
			this.outputObtainer = outputObtainer;
			config?.Invoke(configuration);
		}

		internal class Configurator : IXUnitLoggingConfigurator {
			internal List<string> Tags { get; } = new List<string>();
			public IXUnitLoggingConfigurator AddTag(string tag) {
				Tags.Add(tag);
				return this;
			}
		}

		/// <inheritdoc/>
		public ILogger CreateLogger(string categoryName) {
			return new XUnitLogger(outputObtainer, categoryName, scopes, configuration);
		}

		void IDisposable.Dispose() { }
	}

	/// <summary>
	/// Provides optional configuration for <see cref="XUnitLoggingProvider"/>.
	/// </summary>
	public interface IXUnitLoggingConfigurator {
		/// <summary>
		/// Adds a tag that should be added to every message.
		/// This can be useful, e.g. if a test case needs to instantiate both sides of a communication and the log should reflect from which side the log output originates.
		/// In this example, one would instantiate one <see cref="ILoggerFactory"/> object for each side and configure them with different tags.
		/// </summary>
		/// <param name="tag">The tag to add.</param>
		/// <returns>A reference to the configurator object, to support chaining calls.</returns>
		IXUnitLoggingConfigurator AddTag(string tag);
	}

	/// <summary>
	/// Provides extension methods to add the <see cref="XUnitLoggingProvider"/> to an <see cref="ILoggingBuilder"/>.
	/// </summary>
	public static class XUnitLoggingExtensions {
		/// <summary>
		/// Adds <see cref="XUnitLoggingProvider"/> to the <see cref="ILoggingBuilder"/> using a fixed <see cref="ITestOutputHelper"/>.
		/// This is suitable for simple test cases, where the logging provider doesn't outlive the test case associated with the output helper.
		/// </summary>
		/// <param name="builder">A logging framework builder to which the XUnit logging bridge provider shall be added.</param>
		/// <param name="output">The test case output to which it shall log.</param>
		/// <param name="config">Allows additional options to be configured.</param>
		/// <returns></returns>
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, ITestOutputHelper output, Action<IXUnitLoggingConfigurator>? config = null) {
			builder.AddProvider(new XUnitLoggingProvider(() => output, config));
			return builder;
		}

		/// <summary>
		/// Adds <see cref="XUnitLoggingProvider"/> to the <see cref="ILoggingBuilder"/> using a delegate to obtain the current <see cref="ITestOutputHelper"/> to log to.
		/// This is needed, when the logging provider is placed in a fixture object that is used by multiple test cases and thus needs to be able to switch between output helpers when a new test case is started.
		/// Typically, the fixture calss should have a property / field that is set by each test case and the provided delegate should just return the current value of that variable.
		/// </summary>
		/// <param name="builder">A logging framework builder to which the XUnit logging bridge provider shall be added.</param>
		/// <param name="outputObtainer">A delegate to obtain the <see cref="ITestOutputHelper"/> of the current test case.</param>
		/// <param name="config">Allows additional options to be configured.</param>
		/// <returns></returns>
		public static ILoggingBuilder AddXUnit(this ILoggingBuilder builder, Func<ITestOutputHelper?> outputObtainer, Action<IXUnitLoggingConfigurator>? config = null) {
			builder.AddProvider(new XUnitLoggingProvider(outputObtainer, config));
			return builder;
		}
	}
}
