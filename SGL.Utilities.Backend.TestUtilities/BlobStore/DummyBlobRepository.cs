using SGL.Utilities.Backend.BlobStore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.TestUtilities.BlobStore {
	/// <summary>
	/// An in-memory dummy implementation of <see cref="IBlobRepository"/> to use in test code.
	/// </summary>
	public class DummyBlobRepository : IBlobRepository, IDisposable {
		private class StreamWrapper : Stream {
			private Stream inner;

			public StreamWrapper(Stream inner) {
				this.inner = inner;
				this.inner.Position = 0;
			}

			public override bool CanRead => inner.CanRead;
			public override bool CanSeek => inner.CanSeek;
			public override bool CanWrite => inner.CanWrite;
			public override long Length => inner.Length;
			public override long Position { get => inner.Position; set => inner.Position = value; }
			public override void Flush() => inner.Flush();
			public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
			public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
			public override void SetLength(long value) => inner.SetLength(value);
			public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
			public override void Close() { }
		}

		private Dictionary<BlobPath, MemoryStream> files = new();

		/// <inheritdoc/>
		public async Task CopyBlobIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination, CancellationToken ct = default) {
			await using (var stream = await ReadBlobAsync(appName, userId, logId, suffix, ct)) {
				await stream.CopyToAsync(contentDestination, ct);
			}
		}

		/// <inheritdoc/>
		public async Task DeleteBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, CancellationToken ct = default) {
			var key = new BlobPath() { AppName = appName, OwnerId = ownerId, BlobId = blobId, Suffix = suffix };
			ct.ThrowIfCancellationRequested();
			files.Remove(key);
			await Task.CompletedTask;
		}

		/// <summary>
		/// Disposes the internal memory streams used to represent the blob contents.
		/// </summary>
		public void Dispose() {
			foreach (var kvp in files) {
				kvp.Value.Dispose();
			}
		}

		/// <inheritdoc/>
		public IEnumerable<BlobPath> EnumerateBlobs(string appName, Guid ownerId) {
			return files.Keys.Where(f => f.AppName == appName && f.OwnerId == ownerId);
		}

		/// <inheritdoc/>
		public IEnumerable<BlobPath> EnumerateBlobs(string appName) {
			return files.Keys.Where(f => f.AppName == appName);
		}

		/// <inheritdoc/>
		public IEnumerable<BlobPath> EnumerateBlobs() {
			return files.Keys;
		}

		/// <inheritdoc/>
		public async Task<Stream> ReadBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, CancellationToken ct = default) {
			await Task.CompletedTask;
			var key = new BlobPath() { AppName = appName, OwnerId = ownerId, BlobId = blobId, Suffix = suffix };
			ct.ThrowIfCancellationRequested();
			if (files.TryGetValue(key, out var content)) {
				return new StreamWrapper(content);
			}
			else {
				throw new BlobNotAvailableException(key);
			}
		}

		/// <inheritdoc/>
		public async Task<long> StoreBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, Stream content, CancellationToken ct = default) {
			var stream = new MemoryStream();
			files[new BlobPath() { AppName = appName, OwnerId = ownerId, BlobId = blobId, Suffix = suffix }] = stream;
			await content.CopyToAsync(stream, ct);
			return stream.Length;
		}

		/// <inheritdoc/>
		public Task CheckHealthAsync(CancellationToken ct = default) => Task.CompletedTask;
	}
}
