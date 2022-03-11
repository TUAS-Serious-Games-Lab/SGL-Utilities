using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.BlobStore {
	/// <summary>
	/// Represents the components needed to logically address a specific blob.
	/// </summary>
	public struct BlobPath {
		/// <summary>
		/// The technical name of the application from which the blob originates.
		/// </summary>
		public string AppName { get; set; }
		/// <summary>
		/// The id of the user or group with which the blob is associated.
		/// </summary>
		public Guid OwnerId { get; set; }
		/// <summary>
		/// The unique id of the blob itself.
		/// </summary>
		public Guid BlobId { get; set; }
		/// <summary>
		/// The file suffix for the file name.
		/// </summary>
		public string Suffix { get; set; }

		/// <summary>
		/// Generates a string representation of the logical path.
		/// </summary>
		/// <returns>A string representation of the path components.</returns>
		public override string ToString() => $"[{AppName}/{OwnerId}/{BlobId}{Suffix}]";
	}

	/// <summary>
	/// The exception thrown when a requested blob is not available.
	/// This is most likely the case, if the file doesn't exist, but can also mean, that it exists but is not accessible, e.g. due to file permissions.
	/// </summary>
	public class BlobNotAvailableException : Exception {
		/// <summary>
		/// Constructs an exception object for the given path and optionally the given inner exception to describe the root cause.
		/// </summary>
		/// <param name="blobPath">The affected log path.</param>
		/// <param name="innerException">Another exception that describes the cause of this exception.</param>
		public BlobNotAvailableException(BlobPath blobPath, Exception? innerException = null) : base($"The binary object {blobPath} is not available.", innerException) {
			BlobPath = blobPath;
		}

		/// <summary>
		/// The affected path, i.e. the path that was requested but is not available.
		/// </summary>
		public BlobPath BlobPath { get; set; }
	}

	/// <summary>
	/// Specifies the interface for a repository of blobs, used to store, retrieve, enumerate and delete blob contents.
	/// </summary>
	/// <remarks>
	/// For a maximum of implementation flexibility, it allows operations to be performed asynchronously where possible, even if the default implementation (using files and directories) can only use synchronous APIs.
	/// E.g. while opening a stream to a local file uses a synchronous API, a possible alternate implementation might be backed by an object store where the opening operation involves a request that can be done asynchronously.
	/// However, <c>EnumerateLogs</c> methods need to provide synchronous versions, because LINQ extension methods don't apply to <see cref="IAsyncEnumerable{T}"/>, but only to <see cref="IEnumerable{T}"/>.
	/// </remarks>
	public interface IBlobRepository {
		/// <summary>
		/// Asynchronously stores the data contained in <paramref name="content"/> under the logical path given in <paramref name="blobPath"/>.
		/// </summary>
		/// <param name="blobPath">The logical path to which the file should be stored.</param>
		/// <param name="content">A <see cref="Stream"/> with the desired content. The stream will be read to completion, copying all read data into the target file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the store operation.</param>
		/// <returns>A task object representing the store operation.</returns>
		Task<long> StoreBlobAsync(BlobPath blobPath, Stream content, CancellationToken ct = default) {
			return StoreBlobAsync(blobPath.AppName, blobPath.OwnerId, blobPath.BlobId, blobPath.Suffix, content, ct);
		}
		/// <summary>
		/// Asynchronously stores the data contained in <paramref name="content"/> under the logical path given in <paramref name="appName"/>, <paramref name="ownerId"/>, <paramref name="ownerId"/>, and <paramref name="suffix"/>.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the blob originates.</param>
		/// <param name="ownerId">The id of the user that submitted the blob.</param>
		/// <param name="blobId">The unique id of the blob itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="content">A <see cref="Stream"/> with the desired content. The stream will be read to completion, copying all read data into the target file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the store operation.</param>
		/// <returns>A task object representing the store operation, that contains the size of stored log content.</returns>
		Task<long> StoreBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, Stream content, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously opens the blob under the logical path given in <paramref name="blobPath"/> for reading.
		/// </summary>
		/// <param name="blobPath">The logical path of the blob to read from.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>
		/// A task representing the operation, that contains the opened stream upon success.
		/// It is the responsibility of the caller to dispose of this stream.
		/// </returns>
		Task<Stream> ReadBlobAsync(BlobPath blobPath, CancellationToken ct = default) {
			return ReadBlobAsync(blobPath.AppName, blobPath.OwnerId, blobPath.BlobId, blobPath.Suffix, ct);
		}
		/// <summary>
		/// Asynchronously opens the blob under the logical path given in <paramref name="appName"/>, <paramref name="ownerId"/>, <paramref name="ownerId"/>, and <paramref name="suffix"/> for reading.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the blob originates.</param>
		/// <param name="ownerId">The id of the user that submitted the blob.</param>
		/// <param name="blobId">The unique id of the blob itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>
		/// A task representing the operation, that contains the opened stream upon success.
		/// It is the responsibility of the caller to dispose of this stream.
		/// </returns>
		Task<Stream> ReadBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously copies the contents of the blob under the logical path given in <paramref name="blobPath"/> into the stream given in <paramref name="contentDestination"/>.
		/// </summary>
		/// <param name="blobPath">The logical path of the blob to read from.</param>
		/// <param name="contentDestination">A stream to write the copied content to.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>A task object representing the copy operation.</returns>
		Task CopyBlobIntoAsync(BlobPath blobPath, Stream contentDestination, CancellationToken ct = default) {
			return CopyBlobIntoAsync(blobPath.AppName, blobPath.OwnerId, blobPath.BlobId, blobPath.Suffix, contentDestination, ct);
		}
		/// <summary>
		/// Asynchronously copies the contents of the blob under the logical path given in <paramref name="appName"/>, <paramref name="ownerId"/>, <paramref name="ownerId"/>, and <paramref name="suffix"/> into the stream given in <paramref name="contentDestination"/>.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the blob originates.</param>
		/// <param name="ownerId">The id of the user that submitted the blob.</param>
		/// <param name="blobId">The unique id of the blob itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="contentDestination">A stream to write the copied content to.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>A task object representing the copy operation.</returns>
		Task CopyBlobIntoAsync(string appName, Guid ownerId, Guid blobId, string suffix, Stream contentDestination, CancellationToken ct = default);
		/// <summary>
		/// Enumerates over all known blobs belonging to the given user of the given application.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the blobs originate.</param>
		/// <param name="userId">The id of the user that submitted the blobs.</param>
		/// <returns>An enumerable to iterate over the paths.</returns>
		IEnumerable<BlobPath> EnumerateBlobs(string appName, Guid userId);
		/// <summary>
		/// Enumerates over all known blobs for a given application.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the blobs originate.</param>
		/// <returns>An enumerable to iterate over the paths.</returns>
		IEnumerable<BlobPath> EnumerateBlobs(string appName);
		/// <summary>
		/// Enumerates the paths of all known blobs.
		/// </summary>
		/// <returns>An enumerable to iterate over the paths.</returns>
		IEnumerable<BlobPath> EnumerateBlobs();
		/// <summary>
		/// Asynchronously deletes the blob under the logical path given in <paramref name="blobPath"/>.
		/// </summary>
		/// <param name="blobPath">The logical path of the blob to delete.</param>
		/// <param name="ct">A cancellation token to allow cancelling (the waiting on) the opertation. Note the it is not guaranteed whether this prevents the deletion, as it is dependent on when in the process the task is interrupted.</param>
		/// <returns>A task object representing the delete operation.</returns>
		Task DeleteBlobAsync(BlobPath blobPath, CancellationToken ct = default) {
			return DeleteBlobAsync(blobPath.AppName, blobPath.OwnerId, blobPath.BlobId, blobPath.Suffix, ct);
		}
		/// <summary>
		/// Asynchronously deletes the blob under the logical path given in <paramref name="appName"/>, <paramref name="ownerId"/>, <paramref name="ownerId"/>, and <paramref name="suffix"/>.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the blob originates.</param>
		/// <param name="ownerId">The id of the user that submitted the blob.</param>
		/// <param name="blobId">The unique id of the blob itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="ct">A cancellation token to allow cancelling (the waiting on) the opertation. Note the it is not guaranteed whether this prevents the deletion, as it is dependent on when in the process the task is interrupted.</param>
		/// <returns>A task object representing the delete operation.</returns>
		Task DeleteBlobAsync(string appName, Guid ownerId, Guid blobId, string suffix, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously checks the service health of the underlying storage layer and throws an appropriate exception if it is not healthy.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the health check operation.</param>
		/// <returns>A task representing the health check check operation.</returns>
		Task CheckHealthAsync(CancellationToken ct = default);
	}
}
