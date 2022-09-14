using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
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
	/// Provides functionality to write (heterogenously typed) cryptographic objects as PEM-formatted data.
	/// </summary>
	public class PemObjectWriter {
		private TextWriter rawPemWriter;
		private readonly PemWriter wrapped;
		private readonly string privateKeyEncModeString = "";
		private readonly Func<char[]>? privateKeyPasswordGetter = null;
		private readonly RandomGenerator? random = null;

		/// <summary>
		/// Contructs a PEM writer to write to <paramref name="pemDataWriter"/>.
		/// Writing encrypted objects is not supported with this overload, as no password is taken.
		/// Thus, sensitive objects like private keys and key pairs (containing private keys) can't be written with the resulting writer.
		/// To write such objects, use <see cref="PemObjectWriter(TextWriter, PemEncryptionMode, Func{char[]}, RandomGenerator)"/>
		/// </summary>
		/// <param name="pemDataWriter">The <see cref="TextWriter"/> to which the PEM data shall be written.</param>
		public PemObjectWriter(TextWriter pemDataWriter) {
			rawPemWriter = pemDataWriter;
			wrapped = new PemWriter(pemDataWriter);
		}

		/// <summary>
		/// Contructs a PEM writer to write to <paramref name="pemDataWriter"/>.
		/// Writing encrypted objects is supported with this overload using the given <paramref name="privateKeyEncryptionMode"/> and a password obtained from <paramref name="privateKeyPasswordGetter"/>.
		/// </summary>
		/// <param name="pemDataWriter">The <see cref="TextWriter"/> to which the PEM data shall be written.</param>
		/// <param name="privateKeyEncryptionMode">The mode with which private keys and key pairs shall be encrypted.</param>
		/// <param name="privateKeyPasswordGetter">A function object that is invoked when a private key (or key pair) is encrypted, to obtain the encryption password.</param>
		/// <param name="random">The random genartor to use for generating an initialization vector when encrypting a private key (or key pair).</param>
		/// <exception cref="PemException">When an unsupported <see cref="PemEncryptionMode"/> is specified.</exception>
		public PemObjectWriter(TextWriter pemDataWriter, PemEncryptionMode privateKeyEncryptionMode, Func<char[]> privateKeyPasswordGetter, RandomGenerator random) {
			rawPemWriter = pemDataWriter;
			wrapped = new PemWriter(pemDataWriter);
			privateKeyEncModeString = privateKeyEncryptionMode switch {
				PemEncryptionMode.AES_256_CBC => "AES-256-CBC",
				_ => throw new PemException($"Unsupported PEM encryption mode {privateKeyEncryptionMode}")
			};
			this.privateKeyPasswordGetter = privateKeyPasswordGetter;
			this.random = random;
		}

		/// <summary>
		/// Writes the given object as PEM data.
		/// </summary>
		/// <param name="obj">
		/// The object to write. Supported cryptographic object types are: <see cref="PublicKey"/>, <see cref="PrivateKey"/>, <see cref="KeyPair"/>, and <see cref="Certificate"/>.
		/// Additionally, writing a <see cref="string"/> is supported to write simple text to the PEM file as a kind of comment.
		/// This text outside of BEGIN-END-blocks is ignored by PEM format. Thus the text will be present in the file but will not be read back by <see cref="PemObjectReader"/>.
		/// </param>
		/// <exception cref="PemException">When the object could not be written to PEM data successfully.</exception>
		public void WriteObject(object obj) {
			try {
				switch (obj) {
					case PublicKey pubKey:
						wrapped.WriteObject(pubKey.wrapped);
						return;
					case PrivateKey privKey:
						checkEncryptionMembers();
						wrapped.WriteObject(privKey.wrapped, privateKeyEncModeString, privateKeyPasswordGetter!.Invoke(), random!.wrapped);
						return;
					case KeyPair keyPair:
						checkEncryptionMembers();
						wrapped.WriteObject(keyPair.ToWrappedPair(), privateKeyEncModeString, privateKeyPasswordGetter!.Invoke(), random!.wrapped);
						return;
					case Certificate cert:
						wrapped.WriteObject(cert.wrapped);
						return;
					case CertificateSigningRequest csr:
						wrapped.WriteObject(csr.wrapped);
						return;
					case string comment:
						// Text outside of BEGIN-END-blocks is ignored by PEM format. We can use this to write documenting text lines inbetween.
						// This can be useful to document which object is which.
						rawPemWriter.WriteLine(comment);
						return;
					default:
						throw new PemException("The given object type is not supported for writing into PEM data.", obj.GetType());
				};
			}
			catch (Exception ex) {
				throw new PemException("Error while writing PEM data.", innerException: ex);
			}
		}

		private void checkEncryptionMembers() {
			if (string.IsNullOrEmpty(privateKeyEncModeString)) {
				throw new ArgumentNullException("privateKeyEncryptionMode");
			}
			if (privateKeyPasswordGetter == null) {
				throw new ArgumentNullException(nameof(privateKeyPasswordGetter));
			}
			if (random == null) {
				throw new ArgumentNullException(nameof(random));
			}
		}

		/// <summary>
		/// Writes the given objects to the PEM data output, as if by writing each one using <see cref="WriteObject(object)"/>.
		/// </summary>
		/// <param name="objects">The objects to write.</param>
		public void WriteAllObjects(IEnumerable<object> objects) {
			foreach (object obj in objects) {
				WriteObject(obj);
			}
		}
	}
}
