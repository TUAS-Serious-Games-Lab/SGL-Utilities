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
	public class PemObjectWriter {
		private TextWriter rawPemWriter;
		private readonly PemWriter wrapped;
		private readonly string privateKeyEncModeString;
		private readonly Func<char[]> privateKeyPasswordGetter;
		private readonly RandomGenerator random;

		public PemObjectWriter(TextWriter pemDataWriter) {
			wrapped = new PemWriter(pemDataWriter);
		}
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

		public void WriteObject(object obj) {
			switch (obj) {
				case PublicKey pubKey:
					wrapped.WriteObject(pubKey.wrapped);
					return;
				case PrivateKey privKey:
					checkEncryptionMembers();
					wrapped.WriteObject(privKey.wrapped, privateKeyEncModeString, privateKeyPasswordGetter.Invoke(), random.wrapped);
					return;
				case KeyPair keyPair:
					checkEncryptionMembers();
					wrapped.WriteObject(keyPair.ToWrappedPair(), privateKeyEncModeString, privateKeyPasswordGetter.Invoke(), random.wrapped);
					return;
				case Certificate cert:
					wrapped.WriteObject(cert.wrapped);
					return;
				// TODO: CSR => Pkcs10CertificationRequest
				case string comment:
					// Text between BEGIN and END lines is ignored by PEM format. We can use this to write documenting text lines inbetween.
					// This can be useful to document which object is which.
					rawPemWriter.WriteLine(comment);
					return;
				default:
					throw new PemException("The given object type is not supported for writing into PEM data.", obj.GetType());
			};
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

		public void WriteAllObjects(IEnumerable<object> objects) {
			foreach (object obj in objects) {
				WriteObject(obj);
			}
		}
	}
}
