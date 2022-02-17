using Org.BouncyCastle.Asn1.X509;

namespace SGL.Utilities.Crypto {
	public class KeyIdentifier {
		internal SubjectKeyIdentifier wrapped;

		internal KeyIdentifier(SubjectKeyIdentifier wrapped) {
			this.wrapped = wrapped;
		}

		public override bool Equals(object? obj) => obj is KeyIdentifier identifier && wrapped.Equals(identifier.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();
	}
}
