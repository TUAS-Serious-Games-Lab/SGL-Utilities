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
	public class PemObjectReader {
		private readonly PemReader wrapped;

		public PemObjectReader(TextReader pemDataReader) {
			wrapped = new PemReader(pemDataReader);
		}
		public PemObjectReader(TextReader pemDataReader, Func<char[]> passwordGetter) {
			wrapped = new PemReader(pemDataReader, new PemHelper.FuncPasswordFinder(passwordGetter));
		}

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

		public IEnumerable<object> ReadAllObjects() {
			object? obj;
			while ((obj = ReadNextObject()) != null) {
				yield return obj;
			}
		}
	}
}
