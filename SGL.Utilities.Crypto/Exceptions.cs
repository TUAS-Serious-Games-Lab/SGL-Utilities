using System;

namespace SGL.Utilities.Crypto {

	// TODO: Wrapp all Bouncy Castle exceptions in these.

	public class CryptographyException : Exception {
		public CryptographyException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	public class KeyException : CryptographyException {
		public KeyException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
	public class CertififcateException : CryptographyException {
		public CertififcateException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
	public class EncryptionException : CryptographyException {
		public EncryptionException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
	public class DecryptionException : CryptographyException {
		public DecryptionException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
