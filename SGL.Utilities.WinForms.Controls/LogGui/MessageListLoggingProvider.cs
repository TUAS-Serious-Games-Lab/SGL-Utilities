using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.WinForms.Controls.LogGui {
	public class MessageListLoggingProvider : ILoggerProvider {
		private readonly LogMessageList receiver;
		internal LoggerExternalScopeProvider Scopes = new LoggerExternalScopeProvider();

		public LogLevel MinLogLevel { get; }

		public MessageListLoggingProvider(LogMessageList receiver, LogLevel minLogLevel) {
			this.receiver = receiver;
			MinLogLevel = minLogLevel;
		}

		public ILogger CreateLogger(string categoryName) {
			return new Logger(categoryName, this, MinLogLevel);
		}

		internal void AddItem(LogMessage msg) {
			receiver.AddItem(msg);
		}

		private class Logger : ILogger {
			private string categoryName;
			private MessageListLoggingProvider provider;
			private LogLevel minLogLevel;

			public Logger(string categoryName, MessageListLoggingProvider provider, LogLevel minLogLevel) {
				this.categoryName = categoryName;
				this.provider = provider;
				this.minLogLevel = minLogLevel;
			}

			public IDisposable BeginScope<TState>(TState state) {
				return provider.Scopes.Push(state);
			}

			public bool IsEnabled(LogLevel logLevel) {
				return logLevel >= minLogLevel;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
				if (logLevel < minLogLevel) return;
				var time = DateTime.Now;
				var formattedSopes = new List<string>();
				provider.Scopes.ForEachScope((scope, formatted) => formatted.Add(scope?.ToString() ?? ""), formattedSopes);
				var formattedState = formatter(state, exception);
				provider.AddItem(new LogMessage(categoryName, formattedSopes, logLevel, eventId, time, formattedState, exception));
			}
		}

		public void Dispose() {
		}
	}
}
