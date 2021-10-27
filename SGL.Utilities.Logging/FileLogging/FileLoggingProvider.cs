﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public interface IFileLoggingProviderBuilder {
		void AddPlaceholder(string name, PlaceholderValueGetter<LogMessage> valueGetter);
	}

	[ProviderAlias("File")]
	public class FileLoggingProvider : ILoggerProvider, IAsyncDisposable {
		private FileLoggingProviderOptions options;
		internal AsyncConsumerQueue<LogMessage> WriterQueue = new();
		internal LoggerExternalScopeProvider Scopes = new LoggerExternalScopeProvider();
		private List<FileLoggingSink> sinks;
		private Task writerWorkerHandle;
		private bool disposed = false;
		internal LogLevel MinLevel;

		private Action<INamedPlaceholderFormatterFactoryBuilder<LogMessage>> formaterFactoryBuilder;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactory;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime;

		private Task startWriterWorker() {
			return Task.Run(async () => {
				await foreach (var msg in WriterQueue.DequeueAllAsync().ConfigureAwait(false)) {
					await Task.WhenAll(sinks.Select(sink => sink.WriteAsync(msg)).ToArray()).ConfigureAwait(false);
				}
			});
		}

		private class Builder : IFileLoggingProviderBuilder {
			INamedPlaceholderFormatterFactoryBuilder<LogMessage> formatterFactoryBuilder;

			public Builder(INamedPlaceholderFormatterFactoryBuilder<LogMessage> formatterFactoryBuilder) {
				this.formatterFactoryBuilder = formatterFactoryBuilder;
			}

			public void AddPlaceholder(string name, PlaceholderValueGetter<LogMessage> valueGetter) {
				formatterFactoryBuilder.AddPlaceholder(name, valueGetter);
			}
		}

		public FileLoggingProvider(IOptions<FileLoggingProviderOptions> options, Action<IFileLoggingProviderBuilder> logProvBuilder) :
			this(options.Value, logProvBuilder) { }

		public FileLoggingProvider(FileLoggingProviderOptions options, Action<IFileLoggingProviderBuilder> logProvBuilder) {
			this.options = options;
			MinLevel = options.Sinks.Any() ? options.Sinks.Select(s => s.MinLevel).Min() : LogLevel.None;
			formaterFactoryBuilder = builder => {
				builder.AddPlaceholder("AppDomainName", m => AppDomain.CurrentDomain.FriendlyName);
				builder.AddPlaceholder("Category", m => m.Category);
				builder.AddPlaceholder("ScopesJoined", m => string.Join(";", m.Scopes));
				builder.AddPlaceholder("Scope0", m => m.Scopes.FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope1", m => m.Scopes.Skip(1).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope2", m => m.Scopes.Skip(2).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope3", m => m.Scopes.Skip(3).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope4", m => m.Scopes.Skip(4).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope5", m => m.Scopes.Skip(5).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope6", m => m.Scopes.Skip(6).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope7", m => m.Scopes.Skip(7).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Level", m => m.Level);
				builder.AddPlaceholder("EventId", m => m.EventId);
				builder.AddPlaceholder("Time", m => m.Time);
				builder.AddPlaceholder("Text", m => m.Text);
				builder.AddPlaceholder("Exception", m => m.Exception?.ToString() ?? "");
				builder.SetFallbackValueGetter((name, msg) => this.options.Constants.TryGetValue(name, out var val) ? val : "");
				logProvBuilder(new Builder(builder));
			};
			formatterFactory = new NamedPlaceholderFormatterFactory<LogMessage>(formaterFactoryBuilder);
			formatterFactoryFixedTime = new NamedPlaceholderFormatterFactory<LogMessage>(builder => {
				formaterFactoryBuilder(builder);
				// For writers with time-based filenames we need to replace out-dated writers instead of just opening new ones.
				// Therefore, we need a version of the formatted name that is time-independent as a key to compare if an existing
				// writer is otherwise for the same file, i.e. for the predecessor.
				// For this, we use this factory to always format the strings with epoch for the time-based placeholders.
				builder.AddPlaceholder("Time", m => DateTime.UnixEpoch);
			});
			sinks = options.Sinks.Select(s => new FileLoggingSink(s,
				formatterFactory.Create(s.BaseDirectory ?? options.BaseDirectory),
				formatterFactory.Create(s.MessageFormat ?? options.MessageFormat),
				formatterFactory.Create(s.MessageFormatException ?? options.MessageFormatException),
				formatterFactory.Create(s.FilenameFormat ?? options.FilenameFormat),
				formatterFactoryFixedTime.Create(s.FilenameFormat ?? options.FilenameFormat)
				)).ToList();
			writerWorkerHandle = startWriterWorker();
		}

		public ILogger CreateLogger(string categoryName) {
			return new FileLogger(categoryName, this, LogLevel.Trace);
		}

		public async ValueTask DisposeAsync() {
			if (disposed) return;
			disposed = true;
			WriterQueue.Finish();
			await writerWorkerHandle.ConfigureAwait(false);
			foreach (var sink in sinks) {
				await sink.DisposeAsync().ConfigureAwait(false);
			}
		}

		public void Dispose() {
			DisposeAsync().AsTask().Wait();
		}
	}
}