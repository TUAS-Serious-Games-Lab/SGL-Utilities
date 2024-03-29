﻿using System.IO;

namespace SGL.Utilities.Crypto.EndToEnd {

	/// <summary>
	/// Represents the operation mode of a <see cref="CipherStream"/>.
	/// </summary>
	public enum CipherStreamOperationMode {
		/// <summary>
		/// The stream is intended for reading clear text data from the underlying stream, encrypting them and then returning the encrypted data to the reader.
		/// </summary>
		/// <remarks>This can be used, e.g. if an unecrypted file is read from a local drive and shall be transferred somewhere in encrypted form where the transfer mechanism expects to read the encrypted data from a stream.</remarks>
		EncryptingRead,
		/// <summary>
		/// The stream is intended for writing clear text data to the stream, which are then encrypted and the encrypted data is then written to an underlying stream.
		/// </summary>
		/// <remarks>This can be used, e.g. when writing a file that shall be stored in encrypted form.</remarks>
		EncryptingWrite,
		/// <summary>
		/// The stream is intended for reading cleear text data from the stream.
		/// The corresponding encrypted data is read from an underlying stream and decrypted by the <see cref="CipherStream"/>, before it is returned to the reader.
		/// </summary>
		/// <remarks>This can be used, e.g. when reading an encrypted file.</remarks>
		DecryptingRead,
		/// <summary>
		/// The stream is intended for writing encrypted data to it, which are the decrypted and written to the underlying stream in clear text form.
		/// </summary>
		/// <remarks>This can be used, e.g. when downloading encrypted data that shall be sotred locally in unencrypted form.</remarks>
		DecryptingWrite
	}

	/// <summary>
	/// A <see cref="Stream"/> inplementation that encrypts or decrypts data on-the-fly.
	/// Objects of this class are returned by <see cref="DataEncryptor.OpenEncryptionWriteStream(Stream, int, bool)"/>, <see cref="DataEncryptor.OpenEncryptionReadStream(Stream, int, bool)"/>,
	/// <see cref="DataDecryptor.OpenDecryptionWriteStream(Stream, int, bool)"/>, and <see cref="DataDecryptor.OpenDecryptionReadStream(Stream, int, bool)"/>.
	/// </summary>
	public class CipherStream : Stream {
		private readonly Stream wrapped;

		/// <summary>
		/// The operation mode of the stream.
		/// </summary>
		public CipherStreamOperationMode Mode { get; }

		internal CipherStream(Stream wrapped, CipherStreamOperationMode mode) {
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
