using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.BlobStore {

	/// <summary>
	/// Encapsulates the configuration options for <see cref="FileSystemBlobRepository"/>
	/// </summary>
	public class FileSystemBlobRepositoryOptions {
		/// <summary>
		/// The directory where the blobs shall be stored in per-application and per-owner subdirectories.
		/// Defaults to the directory <c>BlobStorage</c> under the current directory.
		/// </summary>
		public string StorageDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, "BlobStorage");
	}

	/// <summary>
	/// Provides the <see cref="UseFileSystemBlobRepository(IServiceCollection, IConfiguration, string, string?)"/> extension method.
	/// </summary>
	public static class FileSystemBlobRepositoryExtensions {
		/// <summary>
		/// Adds <see cref="FileSystemBlobRepository"/> as the implementation for <see cref="IBlobRepository"/>
		/// with its configuration options obtained from the key <paramref name="configKey"/> in the configuration root object <paramref name="config"/> in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The config root to use.</param>
		/// <param name="configKey">The config key to read from <paramref name="config"/>.</param>
		/// <param name="healthCheckName">The name under which to add the health check for the blob repository. The default value of null disables the health check registration.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseFileSystemBlobRepository(this IServiceCollection services, IConfiguration config, string configKey, string? healthCheckName = null) {
			services.Configure<FileSystemBlobRepositoryOptions>(config.GetSection(configKey));
			services.AddScoped<IBlobRepository, FileSystemBlobRepository>();
			if (healthCheckName != null) {
				services.AddHealthChecks().AddCheck<BlobRepositoryHealthCheck>(healthCheckName);
			}
			return services;
		}
	}

	/// <summary>
	/// Provides an imlementation of <see cref="IBlobRepository"/> that stores the blobs under a specified directory
	/// with subdirectories for the application, containing per-user subdirectories that contain the blobs.
	/// </summary>
	public class FileSystemBlobRepository : IBlobRepository {
		private static readonly int guidLength = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".Length;
		private static readonly string tempSeparator = ".temp-";
		private static readonly int tempSuffixLength = makeTempSuffix().Length;
		private readonly string storageDirectory;

		/// <summary>
		/// Creates a repository using the given configuration options.
		/// </summary>
		/// <param name="configOptions">The configuration options.</param>
		public FileSystemBlobRepository(IOptions<FileSystemBlobRepositoryOptions> configOptions) : this(configOptions.Value) { }
		/// <summary>
		/// Creates a repository using the given configuration options.
		/// </summary>
		/// <param name="options">The configuration options.</param>
		public FileSystemBlobRepository(FileSystemBlobRepositoryOptions options) : this(options.StorageDirectory) { }
		/// <summary>
		/// Creates a repository using the given directory path as its root directory under which the subdirectories and blobs are stored.
		/// </summary>
		/// <param name="storageDirectory"></param>
		public FileSystemBlobRepository(string storageDirectory) {
			this.storageDirectory = storageDirectory;
		}
		private static string makeTempSuffix() {
			return tempSeparator + StringGenerator.GenerateRandomWord(6);
		}

		private string makeFilePath(string appName, Guid ownerId, Guid blobId, string suffix) {
			return Path.Combine(storageDirectory, appName, ownerId.ToString(), blobId.ToString() + suffix);
		}
		private string makeDirectoryPath(string appName, Guid ownerId) {
			return Path.Combine(storageDirectory, appName, ownerId.ToString());
		}

		private bool doesDirectoryExist(string appName, Guid ownerId) {
			return Directory.Exists(makeDirectoryPath(appName, ownerId));
		}

		private void ensureDirectoryExists(string appName, Guid ownerId) {
			Directory.CreateDirectory(makeDirectoryPath(appName, ownerId));
		}

		private static BlobPath? tryParseFilename(string appName, Guid ownerId, ReadOnlySpan<char> filename) {
			var guidSpan = filename.Slice(0, guidLength);
			if (!Guid.TryParse(guidSpan, out var blobId)) return null;
			var afterGuidSpan = filename.Slice(guidLength);
			if (afterGuidSpan.Length >= tempSuffixLength) {
				var potentialTempSuffix = afterGuidSpan.Slice(afterGuidSpan.Length - tempSuffixLength);
				if (potentialTempSuffix.StartsWith(".temp-")) {
					return null;
				}
			}
			return new BlobPath {
				AppName = appName,
				OwnerId = ownerId,
				BlobId = blobId,
				Suffix = afterGuidSpan.ToString()
			};
		}

		private IEnumerable<BlobPath> enumerateDirectory(string appName, Guid ownerId) {
			if (!doesDirectoryExist(appName, ownerId)) return Enumerable.Empty<BlobPath>();
			var files = Directory.EnumerateFiles(Path.Combine(storageDirectory, appName, ownerId.ToString()));
			return from file in files
				   let blobPath = tryParseFilename(appName, ownerId, Path.GetFileName(file.AsSpan()))
				   where blobPath.HasValue
				   select blobPath.Value;
		}

		/// <inheritdoc/>
		public async Task CopyBlobIntoAsync(string appName, Guid ownerId, Guid blobId, string suffix, Stream contentDestination, CancellationToken ct = default) {
			await using (var stream = await ReadBlobAsync(appName, ownerId, blobId, suffix, ct)) {
				await stream.CopyToAsync(contentDestination, ct);
			}
		}

		/// <inheritdoc/>
		public Task DeleteBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, CancellationToken ct = default) {
			return Task.Run(() => {
				File.Delete(makeFilePath(appName, ownerId, blobId, suffix));
			}, ct);
		}

		/// <inheritdoc/>
		public IEnumerable<BlobPath> EnumerateBlobs(string appName, Guid ownerId) {
			return enumerateDirectory(appName, ownerId);
		}

		/// <inheritdoc/>
		public IEnumerable<BlobPath> EnumerateBlobs(string appName) {
			var dirs = Directory.EnumerateDirectories(Path.Combine(storageDirectory, appName));
			var ownerIds = from dir in dirs
						   let ownerId = Guid.TryParse(Path.GetFileName(dir), out var guid) ? guid : (Guid?)null
						   where ownerId.HasValue
						   select ownerId.Value;
			foreach (var ownerId in ownerIds) {
				foreach (var blobPath in EnumerateBlobs(appName, ownerId)) {
					yield return blobPath;
				}
			}
		}

		/// <inheritdoc/>
		public IEnumerable<BlobPath> EnumerateBlobs() {
			var dirs = from dir in Directory.EnumerateDirectories(storageDirectory)
					   select Path.GetFileName(dir);
			foreach (var appName in dirs) {
				foreach (var blobPath in EnumerateBlobs(appName)) {
					yield return blobPath;
				}
			}
		}

		/// <summary>
		/// Represents a temporary file left by a failed <see cref="StoreBlobAsync(string, Guid, Guid, string, Stream, CancellationToken)"/>
		/// operation where the temporary file could not be removed, e.g. because of a server crash.
		/// </summary>
		public struct TempFilePath {
			/// <summary>
			/// The <see cref="BlobPath.AppName"/> for the file.
			/// </summary>
			public string AppName { get; set; }
			/// <summary>
			/// The <see cref="BlobPath.OwnerId"/> for the file.
			/// </summary>
			public string OwnerDir { get; set; }
			/// <summary>
			/// The name of the file within the application and owner directory.
			/// </summary>
			public string FileName { get; set; }

			/// <summary>
			/// Returns the combined path relative to the storage directory as a string representation.
			/// </summary>
			public override string ToString() {
				return Path.Combine(AppName, OwnerDir, FileName);
			}

			internal TempFilePath(string appName, string ownerDir, string fileName) {
				AppName = appName;
				OwnerDir = ownerDir;
				FileName = fileName;
			}
		}

		/// <summary>
		/// Enumerates temporary files left by failed <see cref="StoreBlobAsync(string, Guid, Guid, string, Stream, CancellationToken)"/>
		/// operations where the temporary file could not be removed, e.g. because of a server crash.
		/// These files can be removed using <see cref="DeleteTempFile(TempFilePath)"/>.
		/// </summary>
		/// <returns>An enumerable to iterate over the relative paths of the files.</returns>
		public IEnumerable<TempFilePath> EnumerateTempFiles() {
			string searchPattern = $"*{tempSeparator}{new string('?', tempSuffixLength)}";
			var appDirs = from dir in Directory.EnumerateDirectories(storageDirectory)
						  select Path.GetFileName(dir);
			foreach (var appName in appDirs) {
				if (appName is null) continue;
				var userDirs = from dir in Directory.EnumerateDirectories(Path.Combine(storageDirectory, appName))
							   let dirName = Path.GetFileName(dir)
							   let userId = Guid.TryParse(dirName, out var guid) ? guid : (Guid?)null
							   where userId.HasValue
							   select dirName;
				foreach (var userDir in userDirs) {
					foreach (var logFile in Directory.EnumerateFiles(Path.Combine(storageDirectory, appName, userDir), searchPattern)) {
						yield return new TempFilePath(appName, userDir, Path.GetFileName(logFile));
					}
				}
			}
		}

		/// <summary>
		/// Deletes the temporary file represented by the given <see cref="TempFilePath"/>.
		/// </summary>
		/// <param name="tempFile">The path of the file to remove.</param>
		public void DeleteTempFile(TempFilePath tempFile) {
			File.Delete(Path.Combine(storageDirectory, tempFile.AppName, tempFile.OwnerDir, tempFile.FileName));
		}

		/// <inheritdoc/>
		public Task<Stream> ReadBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, CancellationToken ct = default) {
			return Task.Run(() => {
				try {
					var filePath = makeFilePath(appName, ownerId, blobId, suffix);
					ct.ThrowIfCancellationRequested();
					return (Stream)new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
				}
				catch (OperationCanceledException) {
					throw;
				}
				catch (Exception ex) {
					throw new BlobNotAvailableException(new BlobPath { AppName = appName, OwnerId = ownerId, BlobId = blobId, Suffix = suffix }, ex);
				}
			}, ct);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// The log is first written to a temporary file that is not found by the enumerating and reading methods.
		/// and then renamed to the correct final filename upon successful completion.
		/// Thus, if an error occurs during the writing process, the incomplete contents are not visible.
		/// Instead the temporary file is removed if transfer fails.
		/// Furthermore, this strategy provides a last-writer wins resolution for concurrent uploads of the same log, where 'last' refers to the operation the finishes last.
		/// </remarks>
		public Task<long> StoreBlobAsync(string appName, Guid userId, Guid logId, string suffix, Stream content, CancellationToken ct = default) {
			return Task.Run(async () => {
				long size = 0;
				ct.ThrowIfCancellationRequested();
				ensureDirectoryExists(appName, userId);
				// Create target file with temporary name to not make it visible to other operations while it is still being written.
				var filePath = Path.Combine(storageDirectory, appName, userId.ToString(), logId.ToString() + suffix + makeTempSuffix());
				try {
					ct.ThrowIfCancellationRequested();
					using (var writeStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
						ct.ThrowIfCancellationRequested();
						await content.CopyToAsync(writeStream, ct);
						size = writeStream.Length;
					}
					ct.ThrowIfCancellationRequested();
				}
				catch {
					// The store operation failed, most likely due to the content stream producing an I/O error (e.g. because it is reading from a network connection that was interrupted).
					try {
						// Delete the temporary file before rethrowing.
						File.Delete(filePath);
					}
					// However, if the deletion also fails, e.g. due to some server-side I/O error or permission problem, rethrow the original exception, not the new one.
					catch { }
					throw;
				}
				// Rename to final file name to make it visible to other operations.
				File.Move(filePath, makeFilePath(appName, userId, logId, suffix), overwrite: true);
				return size;
			}, ct);
		}

		/// <summary>
		/// Asynchronously checks whether the configured storage directory is available and writeable by creating a directory in it and writing a file into that directory.
		/// The written file content is then re-read for verification and the directory is deleted afterwards.
		/// If any of these steps fails, an exception is thrown, indicating a health check failure.
		/// </summary>
		public async Task CheckHealthAsync(CancellationToken ct = default) {
			await Task.Yield();
			byte[] probe_data = Encoding.UTF8.GetBytes("Health Check Probe");
			ct.ThrowIfCancellationRequested();
			var health_check_dir = Path.Combine(storageDirectory, ".server_health_check");
			try {
				Directory.Delete(health_check_dir, true);
			}
			catch (Exception) { }
			Directory.CreateDirectory(health_check_dir);
			ct.ThrowIfCancellationRequested();
			var probe_file = Path.Combine(health_check_dir, "probe.file");
			using (var writeStream = new FileStream(probe_file, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
				ct.ThrowIfCancellationRequested();
				await writeStream.WriteAsync(probe_data, ct);
			}
			ct.ThrowIfCancellationRequested();
			using (var readStream = new FileStream(probe_file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true)) {
				ct.ThrowIfCancellationRequested();
				byte[] read_probe = new byte[4096];
				var read_amt = await readStream.ReadAsync(read_probe, ct);
				if (read_amt != probe_data.Length) {
					throw new Exception("Read probe data length did not match written probe data length.");
				}
				if (!probe_data.SequenceEqual(read_probe.Take(read_amt))) {
					throw new Exception("Read probe data did not match written probe data.");
				}
				ct.ThrowIfCancellationRequested();
			}
			Directory.Delete(health_check_dir, true);
		}
	}
}
