using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Logging.FileLogging {
	internal class FileLoggingSink : IDisposable, IAsyncDisposable {
		private readonly NamedPlaceholderFormatter<LogMessage> baseDirectoryFormatter;
		private readonly NamedPlaceholderFormatter<LogMessage> normalMessageFormatter;
		private readonly NamedPlaceholderFormatter<LogMessage> exceptionMessageFormatter;
		private readonly NamedPlaceholderFormatter<LogMessage> fileNameFormatter;
		private readonly NamedPlaceholderFormatter<LogMessage>? fileNameFormatterFixedTime;
		private readonly bool timeBased;

		public FileLoggingSink(FileLoggingSinkOptions options,
			NamedPlaceholderFormatter<LogMessage> baseDirectoryFormatter, NamedPlaceholderFormatter<LogMessage> normalMessageFormatter,
			NamedPlaceholderFormatter<LogMessage> exceptionMessageFormatter, NamedPlaceholderFormatter<LogMessage> fileNameFormatter,
			NamedPlaceholderFormatter<LogMessage>? fileNameFormatterFixedTime) {
			this.options = options;
			this.baseDirectoryFormatter = baseDirectoryFormatter;
			this.normalMessageFormatter = normalMessageFormatter;
			this.exceptionMessageFormatter = exceptionMessageFormatter;
			this.fileNameFormatter = fileNameFormatter;
			this.fileNameFormatterFixedTime = fileNameFormatterFixedTime;
			timeBased = fileNameFormatter.UsesPlaceholder("Time") || baseDirectoryFormatter.UsesPlaceholder("Time");
			if (timeBased) {
				timeBasedWriters = options.MaxOpenStreams > 0 ?
					new LRUCache<string, (string Path, StreamWriter Writer)>(options.MaxOpenStreams, w => closeList.Add(w.Writer)) :
					new Dictionary<string, (string Path, StreamWriter Writer)>();
			}
			else {
				normalWriters = options.MaxOpenStreams > 0 ?
					new LRUCache<string, StreamWriter>(options.MaxOpenStreams, w => closeList.Add(w)) :
					new Dictionary<string, StreamWriter>();
			}
		}

		private readonly FileLoggingSinkOptions options;
		private readonly IDictionary<string, (string Path, StreamWriter Writer)>? timeBasedWriters;
		private readonly IDictionary<string, StreamWriter>? normalWriters;
		private readonly List<StreamWriter> closeList = new();
		private readonly StringBuilder stringBuilder = new();

		private string SanitizePath(string filename) => new(filename.Select(c => c switch {
			'.' => c,
			'-' => c,
			'(' => c,
			')' => c,
			'[' => c,
			']' => c,
			_ when c == Path.DirectorySeparatorChar => c,
			_ when c == Path.AltDirectorySeparatorChar => c,
			_ when char.IsLetterOrDigit(c) => c,
			_ => '_'
		}).ToArray());

		private async ValueTask<StreamWriter> GetWriterAsync(LogMessage msg) {
			stringBuilder.Clear();
			var baseDirectory = SanitizePath(baseDirectoryFormatter.AppendFormattedTo(stringBuilder, msg).ToString());
			stringBuilder.Clear();
			var filename = SanitizePath(fileNameFormatter.AppendFormattedTo(stringBuilder, msg).ToString());
			var path = Path.Combine(baseDirectory, filename);
			var dir = Path.GetDirectoryName(path);
			if (timeBased) {
				stringBuilder.Clear();
				var timeIndependentFilenameSlug = SanitizePath(fileNameFormatterFixedTime!.AppendFormattedTo(stringBuilder, msg).ToString());
				if (timeBasedWriters!.TryGetValue(timeIndependentFilenameSlug, out var writerEntry)) {
					if (path == writerEntry.Path) {
						return writerEntry.Writer;
					}
					else {
						Directory.CreateDirectory(dir ?? baseDirectory);
						var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
						await writerEntry.Writer.FlushAsync();
						await writerEntry.Writer.DisposeAsync();
						timeBasedWriters[timeIndependentFilenameSlug] = (path, writer);
						return writer;
					}
				}
				else {
					Directory.CreateDirectory(dir ?? baseDirectory);
					var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
					timeBasedWriters[timeIndependentFilenameSlug] = (path, writer);
					return writer;
				}
			}
			else {
				if (!normalWriters!.TryGetValue(path, out var writer)) {
					Directory.CreateDirectory(dir ?? baseDirectory);
					writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true));
					normalWriters.Add(path, writer);
					return writer;
				}
				else return writer;
			}
		}

		private async ValueTask ProcessPendingClosesAsync() {
			foreach (var writer in closeList) {
				await writer.DisposeAsync();
			}
			closeList.Clear();
		}

		private bool Filter(LogMessage msg) {
			if (msg.Level < options.MinLevel) return false;
			return options.Categories.Count == 0 && options.CategoryContains.Count == 0 ||
				options.Categories.Contains(msg.Category) ||
				options.CategoryContains.Any(c => msg.Category.Contains(c));
		}

		public async Task WriteAsync(LogMessage msg) {
			if (!Filter(msg)) return;
			var writer = await GetWriterAsync(msg);
			await ProcessPendingClosesAsync();
			stringBuilder.Clear();
			if (msg.Exception != null) {
				exceptionMessageFormatter.AppendFormattedTo(stringBuilder, msg);
			}
			else {
				normalMessageFormatter.AppendFormattedTo(stringBuilder, msg);
			}
#if NETSTANDARD
			await writer.WriteLineAsync(stringBuilder.ToString());
#else
			await writer.WriteLineAsync(stringBuilder);
#endif
			await writer.FlushAsync();
		}

		public void Dispose() {
			foreach (var writer in (timeBased ? timeBasedWriters!.Values.Select(w => w.Writer) : normalWriters!.Values).Concat(closeList)) {
				writer.Dispose();
			}
			timeBasedWriters?.Clear();
			normalWriters?.Clear();
			closeList.Clear();
		}

		public async ValueTask DisposeAsync() {
			foreach (var writer in timeBased ? timeBasedWriters!.Values.Select(w => w.Writer) : normalWriters!.Values) {
				await writer.DisposeAsync();
			}
			timeBasedWriters?.Clear();
			normalWriters?.Clear();
			await ProcessPendingClosesAsync();
		}
	}
}
