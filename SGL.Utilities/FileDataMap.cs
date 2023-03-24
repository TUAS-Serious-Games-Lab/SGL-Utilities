using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	public class FileDataMap<TKey, TValue> where TKey : notnull where TValue : class {
		private const string tempSeparator = ".temp-";
		public string DirectoryPath { get; }

		public ILogger Logger { get; set; } = NullLogger.Instance;
		public Func<TKey, string> FileTerminology { get; set; } = key => $"file {key}";

		private Func<Stream, Task<TValue>> readContent;
		private Func<Stream, TValue, Task> writeContent;
		private Func<TKey, string> getFilePath;
		public event AsyncEventHandler<string>? TemporaryFileWritten;

		public FileDataMap(string directoryPath, Func<Stream, Task<TValue>> readContent, Func<Stream, TValue, Task> writeContent, Func<TKey, string>? getFilePath = null) {
			DirectoryPath = directoryPath;
			this.readContent = readContent;
			this.writeContent = writeContent;
			if (getFilePath == null) {
				getFilePath = key => key.ToString();
			}
			this.getFilePath = getFilePath;
		}

		public Task<bool> IsPresentAsync(TKey key, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
		public Task RemoveAsync(TKey key, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
		public Task StoreValueAsync(TKey key, TValue value, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
		public Task<Stream?> OpenRawReadAsync(TKey key, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
		public Task<TValue?> GetValueAsync(TKey key, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
		public Task UpdateValueAsync(TKey key, Action<TValue> update, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
	}
}
