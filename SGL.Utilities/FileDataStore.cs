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
	/// <summary>
	/// Provides file handling logic for storing a value of type <typeparamref name="TValue"/> persistently in a file.
	/// This class handles the storage aspects of the functionality.
	/// The actual (de-)serialization of the value is handled by delegates taken at construction.
	///
	/// Write operations are protected against failures resulting in incomplete files by writing to a temporary file first and then atomically replacing the old file.
	///
	/// None of the I/O operations run on the calling thread of the operation methods of this class,
	/// instead all I/O is dispatched to the threadpool, even those where only synchronous APIs are available, like opening a file stream.
	///
	/// There is also (still experimental) optional support for concurrent file access using a lock file for write-write synchronization and
	/// retry loops with file sharing restrictions for read-write and write-read synchronization.
	/// </summary>
	/// <typeparam name="TValue">The type of the value to store, must be a non-nullable reference type.</typeparam>
	public class FileDataStore<TValue> where TValue : class {
		private const string tempSeparator = ".temp-";
		private const string lockFileSuffix = ".lockfile";
		/// <summary>
		/// When using concurrent mode, sets the interval for retry loops.
		/// </summary>
		public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(1);
		/// <summary>
		/// When using concurrent mode, sets the timeout for acquiring a lock file or for succeeding in a retry loop.
		/// </summary>
		public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(1);
		/// <summary>
		/// The path of the underlying file in which the value is stored.
		/// </summary>
		public string FilePath { get; }

		/// <summary>
		/// The logger used by internal operations of this class, defaults to <see cref="NullLogger.Instance"/>.
		/// </summary>
		public ILogger Logger { get; set; } = NullLogger.Instance;

		/// <summary>
		/// How to refer to what is stored in the file for logging purposes.
		/// </summary>
		public string FileTerminology { get; set; } = "file";

		private Func<Stream, CancellationToken, Task<TValue>> readContent;
		private Func<Stream, TValue, CancellationToken, Task> writeContent;
		private readonly bool concurrent;

		/// <summary>
		/// The event arguments passed to <see cref="TemporaryFileWritten"/>.
		/// </summary>
		public class TemporaryFileWrittenEventArgs : EventArgs {
			internal TemporaryFileWrittenEventArgs(string temporaryFilePath, TValue writtenValue) {
				TemporaryFilePath = temporaryFilePath;
				WrittenValue = writtenValue;
			}

			/// <summary>
			/// The path of the temporary file that was just written.
			/// </summary>
			public string TemporaryFilePath { get; }
			/// <summary>
			/// The value object written to the file.
			/// </summary>
			public TValue WrittenValue { get; }
		}
		/// <summary>
		/// Provides an event hook that is triggered after a temporary file was written, before it is moved to the final file.
		/// Note: The event handlers are invoked from a threadpool thread. If this requires synchronization, it must be done in the handlers.
		/// </summary>
		public event AsyncEventHandler<TemporaryFileWrittenEventArgs>? TemporaryFileWritten;

		private StringGenerator tempSuffixGenerator = new StringGenerator();

		/// <summary>
		/// Constructs a <see cref="FileDataStore{TValue}"/> with the given parameters.
		/// </summary>
		/// <param name="filePath">
		/// The path of the underlying file to use.
		/// Temporary files and the lock file will have an appended suffix added to this path and filename.
		/// </param>
		/// <param name="readContent">
		/// A delegate that implements the logic for reading a <typeparamref name="TValue"/> from a <see cref="Stream"/> asynchronously.
		/// Note: The delegate is invoked from a threadpool thread. If this requires synchronization, it must be done in the delegate.
		/// </param>
		/// <param name="writeContent">
		/// A delegate that implements the logic for writing a <typeparamref name="TValue"/> to a <see cref="Stream"/> asynchronously.
		/// Note: The delegate is invoked from a threadpool thread. If this requires synchronization, it must be done in the delegate.
		/// </param>
		/// <param name="concurrent">Whether to enable the concurrent mode.</param>
		public FileDataStore(string filePath, Func<Stream, CancellationToken, Task<TValue>> readContent, Func<Stream, TValue, CancellationToken, Task> writeContent, bool concurrent = false) {
			FilePath = filePath;
			this.readContent = readContent;
			this.writeContent = writeContent;
			this.concurrent = concurrent;
		}

		/// <summary>
		/// Asynchronously determines if the underlying file is present.
		/// </summary>
		/// <param name="ct">A <see cref="CancellationToken"/> to allow canceling the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> for the operation, containing bool indicating whether the file is present.</returns>
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
		/// <summary>
		/// Asynchronously deletes the underlying file, removing the stored value.
		/// </summary>
		/// <param name="ct">A <see cref="CancellationToken"/> to allow canceling the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> for the operation.</returns>
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
						Logger.LogDebug("Successfully removed " + FileTerminology + ".");
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
		/// <summary>
		/// Asynchronously stores <paramref name="value"/> in the file.
		/// </summary>
		/// <param name="value">The value to store.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> to allow canceling the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> for the operation.</returns>
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
					Logger.LogDebug("Successfully stored " + FileTerminology + ".");
				}
				catch (Exception ex) {
					DeleteTemporary(tempFile);
					if (ex is not OperationCanceledException) {
						Logger.LogError(ex, "Storing " + FileTerminology + " failed.");
					}
					throw;
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
					await writeContent(tempFileStream, value, ct);
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

		/// <summary>
		/// Asynchronously opens the raw underlying file for reading.
		/// This method is suitable only in rare cases, namely when <typeparamref name="TValue"/> is a <see cref="Stream"/> or similar.
		/// In this case, the write deledgate can just copy to the temp file stream and reading the content can be done using this method,
		/// bypassing the read delegate and allowing streamed read access.
		/// Using <see cref="GetValueAsync(CancellationToken)"/> for this would require copying the content into a <see cref="MemoryStream"/>
		/// because there, the file is closed at the end of the call.
		/// With this method, it is the responsibility of the caller to close / dispose the returned stream object.
		/// In such cases, the benefit of using <see cref="FileDataStore{TValue}"/> over direct file access is the automation of temp file handling on the writing side.
		/// </summary>
		/// <param name="ct">A <see cref="CancellationToken"/> to allow canceling the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> for the operation, containing a readable <see cref="Stream"/> for the underlying file, or null if no file is present.</returns>
		public Task<Stream?> OpenRawReadAsync(CancellationToken ct = default) {
			return Task.Run(() => OpenRawReadInnerAsync(ct), ct);
		}

		/// <summary>
		/// Asynchronously reads the value from the underlying file.
		/// </summary>
		/// <param name="ct">A <see cref="CancellationToken"/> to allow canceling the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> for the operation, containing the read value, or null if no file is present.</returns>
		public Task<TValue?> GetValueAsync(CancellationToken ct = default) {
			return Task.Run(async Task<TValue?> () => {
				try {
					await using (var fileStream = await OpenRawReadInnerAsync(ct)) {
						if (fileStream == null) {
							return null;
						}
						return await readContent(fileStream, ct);
					}
				}
				catch (OperationCanceledException) {
					throw;
				}
				catch (Exception ex) {
					Logger.LogError(ex, "Reading " + FileTerminology + " failed.");
					throw;
				}
			}, ct);
		}

		/// <summary>
		/// Asynchronously updates the value in the file by reading the value, applying <paramref name="update"/> to it and writing the modified value back to the file.
		/// </summary>
		/// <param name="update">
		/// A delegate containing the update logic to apply to the value.
		/// When running in concurrent mode, this delegate may be called multiple times due to retries.
		/// </param>
		/// <param name="ct">A <see cref="CancellationToken"/> to allow canceling the operation.</param>
		/// <returns>A <see cref="Task{TResult}"/> for the operation.</returns>
		/// <exception cref="InvalidOperationException">If there was no previous value that could be updated.</exception>
		public Task UpdateValueAsync(Action<TValue> update, CancellationToken ct = default) {
			return Task.Run(async () => {
				if (!File.Exists(FilePath)) {
					throw new InvalidOperationException("Can't update stored value because no value is stored.");
				}
				try {
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
										value = await readContent(fileStream, ct);
									}
									update(value);
									var tempFile = GetTempFilePath();
									try {
										await WriteTemporary(value, tempFile, ct);
										MakeFilePermanent(tempFile, ct);
									}
									catch {
										DeleteTemporary(tempFile);
										throw;
									}
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
							value = await readContent(fileStream, ct);
						}
						update(value);
						var tempFile = GetTempFilePath();
						try {
							await WriteTemporary(value, tempFile, ct);
							MakeFilePermanent(tempFile, ct);
						}
						catch {
							DeleteTemporary(tempFile);
							throw;
						}
					}
					Logger.LogDebug("Successfully updated " + FileTerminology + ".");
				}
				catch (Exception ex) {
					if (ex is not OperationCanceledException) {
						Logger.LogError(ex, "Updating " + FileTerminology + " failed.");
					}
					throw;
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
