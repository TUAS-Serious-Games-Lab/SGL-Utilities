using Org.BouncyCastle.X509;

namespace SGL.Utilities.Crypto {
	public interface ICertificateValidator {
		bool CheckCertificate(X509Certificate cert);
	}
}
