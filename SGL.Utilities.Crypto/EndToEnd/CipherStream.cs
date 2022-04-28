using System.IO;

namespace SGL.Utilities.Crypto.EndToEnd {

	/// <summary>
	/// Represents the operation mode of a <see cref="CipherStream"/>.
	/// </summary>
	public enum CipherStreamOperationMode {
		/// <summary>
		/// The stream is intended for writing clear text data to the stream, which are then encrypted and the encrypted data is then written to an underlying stream.
		/// </summary>
		EncryptingWrite,
		/// <summary>
		/// The stream is intended for reading cleear text data from the stream.
		/// The corresponding encrypted data is read from an underlying stream and decrypted by the <see cref="CipherStream"/>, before it is returned to the reader.
		/// </summary>
		DecryptingRead
	}

	/// <summary>
	/// A <see cref="Stream"/> inplementation that encrypts or decrypts data on-the-fly.
	/// Objects of this class are returned by <see cref="DataEncryptor.OpenEncryptionWriteStream(Stream, int)"/> and <see cref="DataDecryptor.OpenDecryptionReadStream(Stream, int)"/>.
	/// </summary>
	public class CipherStream : Stream {
		private Org.BouncyCastle.Crypto.IO.CipherStream wrapped;

		/// <summary>
		/// The operation mode of the stream.
		/// </summary>
		public CipherStreamOperationMode Mode { get; }

		internal CipherStream(Org.BouncyCastle.Crypto.IO.CipherStream wrapped, CipherStreamOperationMode mode) {
			this.wrapped = wrapped;
			Mode = mode;
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
		public override void Flush() {
			wrapped.Flush();
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) {
			return wrapped.Read(buffer, offset, count);
		}

		/// <inheritdoc/>
		public override int ReadByte() {
			return wrapped.ReadByte();
		}

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) {
			return wrapped.Seek(offset, origin);
		}

		/// <inheritdoc/>
		public override void SetLength(long value) {
			wrapped.SetLength(value);
		}

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) {
			wrapped.Write(buffer, offset, count);
		}

		/// <inheritdoc/>
		public override void WriteByte(byte value) {
			wrapped.WriteByte(value);
		}

		/// <inheritdoc/>
		public override void Close() {
			wrapped.Close();
		}
	}
}
