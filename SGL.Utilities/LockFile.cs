using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	public class LockFile : IDisposable, IAsyncDisposable {
		private FileStream stream;
		private LockFile(FileStream stream) {
			this.stream = stream;
		}
		ValueTask IAsyncDisposable.DisposeAsync() {
			return stream.DisposeAsync();
		}

		void IDisposable.Dispose() {
			stream.Dispose();
		}

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
