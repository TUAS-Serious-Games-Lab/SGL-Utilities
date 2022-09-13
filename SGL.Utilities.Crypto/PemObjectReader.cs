using Org.BouncyCastle.Asn1.Crmf;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto {

	/// <summary>
	/// Provides functionality to read (heterogenously typed) cryptographic objects from PEM-formatted data.
	/// </summary>
	public class PemObjectReader {
		private readonly PemReader wrapped;

		/// <summary>
		/// Constructs a PEM reader to read from <paramref name="pemDataReader"/>.
		/// Only supports reading unencrypted objects.
		/// </summary>
		/// <param name="pemDataReader">A <see cref="TextReader"/> containing PEM-encoded data.</param>
		public PemObjectReader(TextReader pemDataReader) {
			wrapped = new PemReader(pemDataReader);
		}
		/// <summary>
		/// Constructs a PEM reader to read from <paramref name="pemDataReader"/>.
		/// Supports reading encrypted objects using <paramref name="passwordGetter"/> to obtain the password if needed.
		/// </summary>
		/// <param name="pemDataReader">A <see cref="TextReader"/> containing PEM-encoded data.</param>
		/// <param name="passwordGetter">Invoked when a password is needed to decrypt an object.</param>
		public PemObjectReader(TextReader pemDataReader, Func<char[]> passwordGetter) {
			wrapped = new PemReader(pemDataReader, new PemHelper.FuncPasswordFinder(passwordGetter));
		}

		/// <summary>
		/// Reads the next object from the PEM data.
		/// </summary>
		/// <returns>
		/// The read object, can be a <see cref="PublicKey"/>, <see cref="PrivateKey"/>, <see cref="KeyPair"/>, or <see cref="Certificate"/>.
		/// If there was no further object to read, <see langword="null"/> is returned.
		/// </returns>
		/// <exception cref="PemException">If the next object in the PEM data is of a not supported type or when an error is encountered while reading the PEM data itself.</exception>
		public object? ReadNextObject() {
			try {
				var obj = wrapped.ReadObject();
				return obj switch {
					null => null,
					AsymmetricKeyParameter pubKey when (!pubKey.IsPrivate && PublicKey.IsValidWrappedType(pubKey)) => new PublicKey(pubKey),
					AsymmetricKeyParameter privKey when (privKey.IsPrivate && PrivateKey.IsValidWrappedType(privKey)) => new PrivateKey(privKey),
					AsymmetricCipherKeyPair keyPair when (PublicKey.IsValidWrappedType(keyPair.Public) && PrivateKey.IsValidWrappedType(keyPair.Private)) => new KeyPair(keyPair),
					X509Certificate cert => new Certificate(cert),
					Pkcs10CertificationRequest csr => throw new NotImplementedException(),
					_ => throw new PemException("The PEM data contained an unsupported object type.", obj.GetType())
				};
			}
			catch (Exception ex) {
				throw new PemException("Error reading PEM data.", innerException: ex);
			}
		}

		/// <summary>
		/// Reads all objects from the PEM data, as if by continously calling <see cref="ReadNextObject()"/> until null is returned from it.
		/// </summary>
		/// <returns>An enumerable of the read objects.</returns>
		public IEnumerable<object> ReadAllObjects() {
			object? obj;
			while ((obj = ReadNextObject()) != null) {
				yield return obj;
			}
		}
	}
}
