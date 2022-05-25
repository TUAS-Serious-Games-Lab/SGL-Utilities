using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// A stream that wraps another stream to prevent it from being closed when this stream is closed.
	/// </summary>
	public class LeaveOpenStreamWrapper : Stream {
		private readonly Stream wrapped;

		/// <summary>
		/// Wraps the given stream.
		/// </summary>
		/// <param name="wrapped">The stream to keep open.</param>
		public LeaveOpenStreamWrapper(Stream wrapped) {
			this.wrapped = wrapped;
		}

		/// <inheritdoc/>
		public override bool CanRead => wrapped.CanRead;
		/// <inheritdoc/>
		public override bool CanSeek => wrapped.CanSeek;
		/// <inheritdoc/>
		public override bool CanWrite => wrapped.CanWrite;
		/// <inheritdoc/>
		public override long Length => wrapped.Length;
		/// <inheritdoc/>
		public override long Position { get => wrapped.Position; set => wrapped.Position = value; }
		/// <inheritdoc/>
		public override void Flush() => wrapped.Flush();
		/// <inheritdoc/>
		public override int ReadByte() => wrapped.ReadByte();
		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => wrapped.Read(buffer, offset, count);
		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => wrapped.Seek(offset, origin);
		/// <inheritdoc/>
		public override void SetLength(long value) => wrapped.SetLength(value);
		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => wrapped.Write(buffer, offset, count);
		/// <inheritdoc/>
		public override void WriteByte(byte value) => wrapped.WriteByte(value);

		/// <summary>
		/// Intentionally does nothing to keep the wrapped stream unaffected.
		/// </summary>
		public override ValueTask DisposeAsync() => default;
		/// <summary>
		/// Intentionally does nothing to keep the wrapped stream unaffected.
		/// </summary>
		protected override void Dispose(bool disposing) { }
	}
}
