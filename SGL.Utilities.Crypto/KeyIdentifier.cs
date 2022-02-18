using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;

namespace SGL.Utilities.Crypto {
	public class KeyIdentifier {
		internal SubjectKeyIdentifier wrapped;

		internal KeyIdentifier(SubjectKeyIdentifier wrapped) {
			this.wrapped = wrapped;
		}
		public KeyIdentifier(PublicKey publicKey) {
			wrapped = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey.wrapped));
		}

		public override bool Equals(object? obj) => obj is KeyIdentifier identifier && wrapped.Equals(identifier.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();
	}
}
