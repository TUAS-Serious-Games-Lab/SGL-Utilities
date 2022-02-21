using System;

namespace SGL.Utilities.Crypto {

	// TODO: Wrapp all Bouncy Castle exceptions in these.

	/// <summary>
	/// The base exception class for cryptography-related exceptions.
	/// </summary>
	public class CryptographyException : Exception {
		/// <summary>
		/// Instantiates a new exception object with the given data.
		/// </summary>
		/// <param name="message">The exception's error message text.</param>
		/// <param name="innerException">The exception that caused this exception.</param>
		public CryptographyException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	/// <summary>
	/// An exception class that indicates an error concerning key, e.g. keys being incompatible with the attempted operation.
	/// </summary>
	public class KeyException : CryptographyException {
		/// <summary>
		/// Instantiates a new exception object with the given data.
		/// </summary>
		/// <param name="message">The exception's error message text.</param>
		/// <param name="innerException">The exception that caused this exception.</param>
		public KeyException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	/// <summary>
	/// An exception type that indicates a problem with PEM-encoded data, e.g. the object in the PEM data being of a type that is incompatible with the expected type.
	/// </summary>
	public class PemException : CryptographyException {
		/// <summary>
		/// For type mismatches this indicates the (internal) type of the read object.
		/// </summary>
		public Type? PemContentType { get; }
		/// <summary>
		/// Instantiates a new exception object with the given data.
		/// </summary>
		/// <param name="message">The exception's error message text.</param>
		/// <param name="pemContentType">The actually read type that mismatched the expected type, if applicable, null otherwise.</param>
		/// <param name="innerException">The exception that caused this exception.</param>
		public PemException(string? message, Type? pemContentType = null, Exception? innerException = null) : base(message, innerException) {
			PemContentType = pemContentType;
		}
	}
	/// <summary>
	/// An exception indicating a problem with certificates.
	/// </summary>
	public class CertififcateException : CryptographyException {
		/// <summary>
		/// Instantiates a new exception object with the given data.
		/// </summary>
		/// <param name="message">The exception's error message text.</param>
		/// <param name="innerException">The exception that caused this exception.</param>
		public CertififcateException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
	/// <summary>
	/// An exception indicating problem encountered during encryption.
	/// </summary>
	public class EncryptionException : CryptographyException {
		/// <summary>
		/// Instantiates a new exception object with the given data.
		/// </summary>
		/// <param name="message">The exception's error message text.</param>
		/// <param name="innerException">The exception that caused this exception.</param>
		public EncryptionException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
	/// <summary>
	/// An exception indicating problem encountered during decryption.
	/// </summary>
	public class DecryptionException : CryptographyException {
		/// <summary>
		/// Instantiates a new exception object with the given data.
		/// </summary>
		/// <param name="message">The exception's error message text.</param>
		/// <param name="innerException">The exception that caused this exception.</param>
		public DecryptionException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
