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
	public class FileDataStore<TValue> where TValue : class {
		private const string tempSeparator = ".temp-";
		private const string lockFileSuffix = ".lockfile";
		public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(1);
		public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(1);
		public string FilePath { get; }

		public ILogger Logger { get; set; } = NullLogger.Instance;
		public string FileTerminology { get; set; } = "file";

		private Func<Stream, Task<TValue>> readContent;
		private Func<Stream, TValue, Task> writeContent;
		private readonly bool concurrent;

		public class TemporaryFileWrittenEventArgs : EventArgs {
			internal TemporaryFileWrittenEventArgs(string temporaryFilePath, TValue writtenValue) {
				TemporaryFilePath = temporaryFilePath;
				WrittenValue = writtenValue;
			}

			public string TemporaryFilePath { get; }
			public TValue WrittenValue { get; }
		}
		public event AsyncEventHandler<TemporaryFileWrittenEventArgs>? TemporaryFileWritten;
		private StringGenerator tempSuffixGenerator = new StringGenerator();

		public FileDataStore(string filePath, Func<Stream, Task<TValue>> readContent, Func<Stream, TValue, Task> writeContent, bool concurrent = false) {
			FilePath = filePath;
			this.readContent = readContent;
			this.writeContent = writeContent;
			this.concurrent = concurrent;
		}

		public Task<bool> IsPresentAsync(CancellationToken ct = default) {
			return Task.Run(() => {
				try {
					return File.Exists(FilePath);
				}
				catch (Exception ex) {
					Logger.LogError(ex, "Couldn't check for existence of " + FileTerminology + ", reporting non-existent.");
					return false;
				}
			}, ct);
		}
		public Task ClearAsync(CancellationToken ct = default) {
			return Task.Run(async () => {
				try {
					if (File.Exists(FilePath)) {
						ct.ThrowIfCancellationRequested();
						if (concurrent) {
							using var ctsDelay = new CancellationTokenSource(WaitTimeout);
							using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
							ct = ctsCombined.Token;
							await using (var lockFile = await AcquireLock(ct)) {
								while (true) {
									try {
										ct.ThrowIfCancellationRequested();
										File.Delete(FilePath);
										break;
									}
									catch (IOException ex) when (ex.GetType() == typeof(IOException)) {
										Logger.LogInformation(ex, "Couldn't remove " + FileTerminology + " due to I/O error, file may be in use, will retry.");
										await Task.Delay(PollingInterval, ct);
									}
								}
							}
						}
						else {
							File.Delete(FilePath);
						}
						Logger.LogInformation("Successfully removed " + FileTerminology + ".");
					}
				}
				catch (OperationCanceledException) {
					throw;
				}
				catch (Exception ex) {
					Logger.LogError(ex, "Removing " + FileTerminology + " failed.");
					throw;
				}
			}, ct);
		}
		public Task StoreValueAsync(TValue value, CancellationToken ct = default) {
			return Task.Run(async () => {
				var tempFile = GetTempFilePath();
				ct.ThrowIfCancellationRequested();
				try {
					if (concurrent) {
						using var ctsDelay = new CancellationTokenSource(WaitTimeout);
						using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
						ct = ctsCombined.Token;
						await using (var lockFile = await AcquireLock(ct)) {
							await WriteTemporary(value, tempFile, ct);
							while (true) {
								try {
									ct.ThrowIfCancellationRequested();
									MakeFilePermanent(tempFile, ct);
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
						await WriteTemporary(value, tempFile, ct);
						MakeFilePermanent(tempFile, ct);
					}
				}
				catch {
					DeleteTemporary(tempFile);
				}
			}, ct);
		}

		private Task<LockFile> AcquireLock(CancellationToken ct) {
			return LockFile.AcquireAsync($"{FilePath}{FileDataStore<TValue>.lockFileSuffix}", PollingInterval, ct);
		}

		private void MakeFilePermanent(string tempFile, CancellationToken ct) {
#if NETCOREAPP3_0_OR_GREATER
			File.Move(tempFile, FilePath, overwrite: true);
#else
			try {
				File.Move(tempFile, FilePath);
			}
			catch (IOException) {
				Logger.LogTrace("Couldn't move " + FileTerminology + " to its target location directly." +
					"Target file may already exist and current API level does not support atomic move with override, " +
					"falling back to atomic copy with override followed by delete.");
				File.Copy(tempFile, FilePath, overwrite: true);
				DeleteTemporary(tempFile);
			}
#endif
		}

		private async Task WriteTemporary(TValue value, string tempFile, CancellationToken ct) {
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
			var evtArgs = new TemporaryFileWrittenEventArgs(tempFile, value);
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

		private async Task<Stream?> OpenRawReadInnerAsync(CancellationToken ct = default) {
			if (!File.Exists(FilePath)) {
				return null;
			}
			if (concurrent) {
				using var ctsDelay = new CancellationTokenSource(WaitTimeout);
				using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
				ct = ctsCombined.Token;
				while (true) {
					try {
						if (!File.Exists(FilePath)) {
							return null;
						}
						ct.ThrowIfCancellationRequested();
						return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
					}
					catch (FileNotFoundException) {
						return null;
					}
					catch (IOException ex) when (ex.GetType() == typeof(IOException)) {
						Logger.LogInformation(ex, "Couldn't open " + FileTerminology + " for reading due to I/O error, file may be in use, will retry.");
						await Task.Delay(PollingInterval, ct);
					}
				}
			}
			else {
				ct.ThrowIfCancellationRequested();
				return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
			}
		}

		public Task<Stream?> OpenRawReadAsync(CancellationToken ct = default) {
			return Task.Run(() => OpenRawReadInnerAsync(ct), ct);
		}
		public Task<TValue?> GetValueAsync(CancellationToken ct = default) {
			return Task.Run(async Task<TValue?> () => {
				await using (var fileStream = await OpenRawReadInnerAsync(ct)) {
					if (fileStream == null) {
						return null;
					}
					return await readContent(fileStream);
				}
			}, ct);
		}

		public Task UpdateValueAsync(Action<TValue> update, CancellationToken ct = default) {
			return Task.Run(async () => {
				if (!File.Exists(FilePath)) {
					throw new InvalidOperationException("Can't update stored value because no value is stored.");
				}
				if (concurrent) {
					using var ctsDelay = new CancellationTokenSource(WaitTimeout);
					using var ctsCombined = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token);
					ct = ctsCombined.Token;
					while (true) {
						try {
							await using (var lockFile = await AcquireLock(ct)) {
								TValue value;
								await using (var fileStream = await OpenRawReadInnerAsync(ct)) {
									if (fileStream == null) {
										throw new InvalidOperationException("Can't update stored value because no value is stored.");
									}
									value = await readContent(fileStream);
								}
								update(value);
								var tempFile = GetTempFilePath();
								await WriteTemporary(value, tempFile, ct);
								MakeFilePermanent(tempFile, ct);
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
					await using (var fileStream = await OpenRawReadInnerAsync(ct)) {
						if (fileStream == null) {
							throw new InvalidOperationException("Can't update stored value because no value is stored.");
						}
						value = await readContent(fileStream);
					}
					update(value);
					var tempFile = GetTempFilePath();
					await WriteTemporary(value, tempFile, ct);
					MakeFilePermanent(tempFile, ct);
				}
			}, ct);
		}

		private string GetTempFilePath() {
			string tempSuffix;
			lock (tempSuffixGenerator) {
				tempSuffix = tempSuffixGenerator.ProduceRandomWord(8);
			}
			return $"{FilePath}{tempSeparator}{tempSuffix}";
		}
	}
}
