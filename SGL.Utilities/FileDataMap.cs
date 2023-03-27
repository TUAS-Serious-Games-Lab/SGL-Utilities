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
		private const string lockFileSuffix = ".lockfile";
		public string DirectoryPath { get; }
		/// <summary>
		/// When using concurrent mode, sets the interval for retry loops.
		/// </summary>
		public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(1);
		/// <summary>
		/// When using concurrent mode, sets the timeout for acquiring a lock file or for succeeding in a retry loop.
		/// </summary>
		public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(1);

		public ILogger Logger { get; set; } = NullLogger.Instance;
		public string FileTerminology { get; set; } = "file {key}";

		private Func<Stream, Task<TValue>> readContent;
		private Func<Stream, TValue, Task> writeContent;
		private Func<TKey, string> getFilePath;
		private readonly bool concurrent;

		/// <summary>
		/// The event arguments passed to <see cref="TemporaryFileWritten"/>.
		/// </summary>
		public class TemporaryFileWrittenEventArgs : EventArgs {
			internal TemporaryFileWrittenEventArgs(string temporaryFilePath, TKey affectedKey, TValue writtenValue) {
				TemporaryFilePath = temporaryFilePath;
				AffectedKey = affectedKey;
				WrittenValue = writtenValue;
			}

			/// <summary>
			/// The path of the temporary file that was just written.
			/// </summary>
			public string TemporaryFilePath { get; }
			/// <summary>
			/// The key for which the temporary file was written.
			/// </summary>
			public TKey AffectedKey { get; }
			/// <summary>
			/// The value object written to the file.
			/// </summary>
			public TValue WrittenValue { get; }
		}
		public event AsyncEventHandler<TemporaryFileWrittenEventArgs>? TemporaryFileWritten;
		private StringGenerator tempSuffixGenerator = new StringGenerator();

		public FileDataMap(string directoryPath, Func<Stream, Task<TValue>> readContent, Func<Stream, TValue, Task> writeContent, Func<TKey, string>? getFilePath = null, bool concurrent = false) {
			DirectoryPath = directoryPath;
			this.readContent = readContent;
			this.writeContent = writeContent;
			if (getFilePath == null) {
				getFilePath = key => key.ToString();
			}
			this.getFilePath = getFilePath;
			this.concurrent = concurrent;
		}

		public Task<bool> IsPresentAsync(TKey key, CancellationToken ct = default) {
			var filePath = getFilePath(key);
			return Task.Run(() => {
				try {
					return File.Exists(filePath);
				}
				catch (Exception ex) {
					Logger.LogError(ex, "Couldn't check for existence of " + FileTerminology + ", reporting non-existent.", key);
					return false;
				}
			}, ct);
		}
		public Task RemoveAsync(TKey key, CancellationToken ct = default) {
			var filePath = getFilePath(key);
			return Task.Run(async () => {
				try {
					if (File.Exists(filePath)) {
						ct.ThrowIfCancellationRequested();
						if (concurrent) {
							using var ctsDelay = new CancellationTokenSource(WaitTimeout);
							using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
							ct = ctsCombined.Token;
							await using (var lockFile = await AcquireLock(filePath, ct)) {
								while (true) {
									try {
										ct.ThrowIfCancellationRequested();
										File.Delete(filePath);
										break;
									}
									catch (IOException ex) when (ex.GetType() == typeof(IOException)) {
										Logger.LogInformation(ex, "Couldn't remove " + FileTerminology + " due to I/O error, file may be in use, will retry.", key);
										await Task.Delay(PollingInterval, ct);
									}
								}
							}
						}
						else {
							File.Delete(filePath);
						}
						Logger.LogInformation("Successfully removed " + FileTerminology + ".", key);
					}
				}
				catch (OperationCanceledException) {
					throw;
				}
				catch (Exception ex) {
					Logger.LogError(ex, "Removing " + FileTerminology + " failed.", key);
					throw;
				}
			}, ct);
		}
		public Task StoreValueAsync(TKey key, TValue value, CancellationToken ct = default) {
			var filePath = getFilePath(key);
			return Task.Run(async () => {
				var tempFile = GetTempFilePath(filePath);
				ct.ThrowIfCancellationRequested();
				try {
					if (concurrent) {
						using var ctsDelay = new CancellationTokenSource(WaitTimeout);
						using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
						ct = ctsCombined.Token;
						await using (var lockFile = await AcquireLock(filePath, ct)) {
							await WriteTemporary(key, value, tempFile, ct);
							while (true) {
								try {
									ct.ThrowIfCancellationRequested();
									MakeFilePermanent(key, filePath, tempFile, ct);
									break;
								}
								catch (IOException ex) when (ex.GetType() == typeof(IOException)) {
									Logger.LogInformation(ex, "Couldn't store " + FileTerminology + " due to I/O error, file may be in use, will retry.");
									await Task.Delay(PollingInterval, ct);
								}
								// For some reason, File.Move(...,true) reports a locked target file as UnauthorizedAccessException:
								catch (UnauthorizedAccessException ex) {
									Logger.LogInformation(ex, "Couldn't store " + FileTerminology + " due to I/O error, file may be in use, will retry.");
									await Task.Delay(PollingInterval, ct);
								}
							}
						}
					}
					else {
						await WriteTemporary(key, value, tempFile, ct);
						MakeFilePermanent(key, filePath, tempFile, ct);
					}
				}
				catch {
					DeleteTemporary(tempFile);
				}
			}, ct);
		}
		public Task<Stream?> OpenRawReadAsync(TKey key, CancellationToken ct = default) {
			var filePath = getFilePath(key);
			return Task.Run(() => OpenRawReadInnerAsync(key, filePath, ct), ct);
		}
		public Task<TValue?> GetValueAsync(TKey key, CancellationToken ct = default) {
			var filePath = getFilePath(key);
			return Task.Run(async Task<TValue?> () => {
				await using (var fileStream = await OpenRawReadInnerAsync(key, filePath, ct)) {
					if (fileStream == null) {
						return null;
					}
					return await readContent(fileStream);
				}
			}, ct);
		}
		public Task UpdateValueAsync(TKey key, Action<TValue> update, CancellationToken ct = default) {
			var filePath = getFilePath(key);
			return Task.Run(async () => {
				if (!File.Exists(filePath)) {
					throw new InvalidOperationException("Can't update stored value because no value is stored.");
				}
				if (concurrent) {
					using var ctsDelay = new CancellationTokenSource(WaitTimeout);
					using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
					ct = ctsCombined.Token;
					while (true) {
						try {
							await using (var lockFile = await AcquireLock(filePath, ct)) {
								TValue value;
								await using (var fileStream = await OpenRawReadInnerAsync(key, filePath, ct)) {
									if (fileStream == null) {
										throw new InvalidOperationException("Can't update stored value because no value is stored.");
									}
									value = await readContent(fileStream);
								}
								update(value);
								var tempFile = GetTempFilePath(filePath);
								await WriteTemporary(key, value, tempFile, ct);
								MakeFilePermanent(key, filePath, tempFile, ct);
							}
							break;
						}
						catch (IOException ex) when (ex.GetType() == typeof(IOException)) {
							Logger.LogInformation(ex, "Couldn't store " + FileTerminology + " due to I/O error, file may be in use, will retry.");
							await Task.Delay(PollingInterval, ct);
						}
						// For some reason, File.Move(...,true) reports a locked target file as UnauthorizedAccessException:
						catch (UnauthorizedAccessException ex) {
							Logger.LogInformation(ex, "Couldn't store " + FileTerminology + " due to I/O error, file may be in use, will retry.");
							await Task.Delay(PollingInterval, ct);
						}
					}
				}
				else {
					TValue value;
					await using (var fileStream = await OpenRawReadInnerAsync(key, filePath, ct)) {
						if (fileStream == null) {
							throw new InvalidOperationException("Can't update stored value because no value is stored.");
						}
						value = await readContent(fileStream);
					}
					update(value);
					var tempFile = GetTempFilePath(filePath);
					await WriteTemporary(key, value, tempFile, ct);
					MakeFilePermanent(key, filePath, tempFile, ct);
				}
			}, ct);
		}

		private string GetTempFilePath(string filePath) {
			string tempSuffix;
			lock (tempSuffixGenerator) {
				tempSuffix = tempSuffixGenerator.ProduceRandomWord(8);
			}
			return $"{filePath}{tempSeparator}{tempSuffix}";
		}

		private Task<LockFile> AcquireLock(string filePath, CancellationToken ct) {
			return LockFile.AcquireAsync($"{filePath}{FileDataMap<TKey, TValue>.lockFileSuffix}", PollingInterval, ct);
		}

		private void MakeFilePermanent(TKey key, string filePath, string tempFile, CancellationToken ct) {
#if NETCOREAPP3_0_OR_GREATER
			File.Move(tempFile, filePath, overwrite: true);
#else
			try {
				File.Move(tempFile, filePath);
			}
			catch (IOException) {
				Logger.LogTrace("Couldn't move " + FileTerminology + " to its target location directly." +
					"Target file may already exist and current API level does not support atomic move with override, " +
					"falling back to atomic copy with override followed by delete.", key);
				File.Copy(tempFile, filePath, overwrite: true);
				DeleteTemporary(tempFile);
			}
#endif
		}

		private async Task WriteTemporary(TKey key, TValue value, string tempFile, CancellationToken ct) {
			try {
				await using (var tempFileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
					ct.ThrowIfCancellationRequested();
					await writeContent(tempFileStream, value);
				}
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				Logger.LogError(ex, "Failed to write temporary for " + FileTerminology + ".");
				throw;
			}
			var evtArgs = new TemporaryFileWrittenEventArgs(tempFile, key, value);
			try {
				ct.ThrowIfCancellationRequested();
				await (TemporaryFileWritten?.InvokeAllAsync(this, evtArgs, ct) ?? Task.CompletedTask);
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				Logger.LogError(ex, "Error occurred during " + nameof(TemporaryFileWritten) + " event.");
				throw;
			}
		}

		private void DeleteTemporary(string tempPath) {
			try {
				File.Delete(tempPath);
			}
			catch (Exception ex) {
				Logger.LogError(ex, "Failed deleting the temporary file '{temp}'.", tempPath);
			}
		}

		private async Task<Stream?> OpenRawReadInnerAsync(TKey key, string filePath, CancellationToken ct = default) {
			if (!File.Exists(filePath)) {
				return null;
			}
			if (concurrent) {
				using var ctsDelay = new CancellationTokenSource(WaitTimeout);
				using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
				ct = ctsCombined.Token;
				while (true) {
					try {
						if (!File.Exists(filePath)) {
							return null;
						}
						ct.ThrowIfCancellationRequested();
						return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
					}
					catch (FileNotFoundException) {
						return null;
					}
					catch (IOException ex) when (ex.GetType() == typeof(IOException)) {
						Logger.LogInformation(ex, "Couldn't open " + FileTerminology + " for reading due to I/O error, file may be in use, will retry.", key);
						await Task.Delay(PollingInterval, ct);
					}
				}
			}
			else {
				ct.ThrowIfCancellationRequested();
				return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
			}
		}

	}
}
