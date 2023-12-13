using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a cross-process locking mechanism using a filesystem lock.
	/// The lock is acquired using <see cref="TryAcquire(string)"/> or <see cref="AcquireAsync(string, TimeSpan, CancellationToken)"/> 
	/// and released when the <see cref="LockFile"/> object is disposed.
	/// </summary>
	public class LockFile : IDisposable, IAsyncDisposable {
		private readonly FileStream stream;
		private LockFile(FileStream stream) {
			this.stream = stream;
		}
		ValueTask IAsyncDisposable.DisposeAsync() {
			return stream.DisposeAsync();
		}

		void IDisposable.Dispose() {
			stream.Dispose();
		}

		/// <summary>
		/// Tries to acquire a lock on the lock file at the specified path <paramref name="lockFilePath"/>.
		/// The file is marked to be automatically deleted when it is closed when the lock is released.
		/// </summary>
		/// <param name="lockFilePath">The path at which to create+lock or open+lock the lock file.</param>
		/// <returns>A <see cref="LockFile"/> object upon success, or null if the file is already locked.</returns>
		/// <exception cref="DirectoryNotFoundException">If a directory under which the file shall be locked wasn't found.</exception>
		/// <exception cref="DriveNotFoundException">If the drive on which the file shall be locked wasn't found.</exception>
		/// <exception cref="PathTooLongException">If <paramref name="lockFilePath"/> is too long.</exception>
		public static LockFile? TryAcquire(string lockFilePath) {
			try {
				return new LockFile(new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 0, FileOptions.DeleteOnClose));
			}
			catch (DirectoryNotFoundException) {
				throw;
			}
			catch (DriveNotFoundException) {
				throw;
			}
			catch (FileNotFoundException) {
				throw;
			}
			catch (PathTooLongException) {
				throw;
			}
			catch (IOException) {
				return null;
			}
		}

		/// <summary>
		/// Asynchronously acquires a lock on the file at <paramref name="lockFilePath"/> by repeatedly calling <see cref="TryAcquire(string)"/> until it succeeds.
		/// Waiting is done using the given <paramref name="pollingInterval"/>.
		/// </summary>
		/// <param name="lockFilePath">The path at which to create+lock or open+lock the lock file.</param>
		/// <param name="pollingInterval">The time to wait between attempts.</param>
		/// <param name="ct">A cancellation token to allow cancellation of the acquire operation.</param>
		/// <returns>A task representing the asynchronous acquire operation, providing a <see cref="LockFile"/> object upon success.</returns>
		/// <exception cref="DirectoryNotFoundException">If a directory under which the file shall be locked wasn't found.</exception>
		/// <exception cref="DriveNotFoundException">If the drive on which the file shall be locked wasn't found.</exception>
		/// <exception cref="PathTooLongException">If <paramref name="lockFilePath"/> is too long.</exception>
		public static async Task<LockFile> AcquireAsync(string lockFilePath, TimeSpan pollingInterval, CancellationToken ct = default) {
			while (true) {
				ct.ThrowIfCancellationRequested();
				var lockFile = TryAcquire(lockFilePath);
				if (lockFile != null) {
					return lockFile;
				}
				await Task.Delay(pollingInterval, ct);
			}
		}
	}
}
