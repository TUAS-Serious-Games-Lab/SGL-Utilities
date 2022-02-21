using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto.Keys;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents a key identifier as it is used by the SubjectKeyIdentifier and AuthorityKeyIdentifier extensions in X509 certificates.
	/// </summary>
	public class KeyIdentifier {
		internal SubjectKeyIdentifier wrapped;

		internal KeyIdentifier(SubjectKeyIdentifier wrapped) {
			this.wrapped = wrapped;
		}

		/// <summary>
		/// Allows creating a KeyIdentifier object from the given raw key identifier bytes.
		/// </summary>
		/// <param name="identifier">The raw bytes of the key identifier, as returned by <see cref="Identifier"/>.</param>
		public KeyIdentifier(byte[] identifier) {
			wrapped = new SubjectKeyIdentifier(identifier);
		}

		/// <summary>
		/// Constructs a KeyIdentifier that represents the given public key.
		/// Note that no compatibilty between other key identifier generators can be assumed, therefore an issuer should copy
		/// the subject key identifier from their certificate into the authority key identifier of certificates they issue and
		/// the CA certificate look-up should look for a CA certificate with a subject key identifier matching the checked certificate's
		/// subject key identifier, instead of comparing recorded key identifiers with ones generated from the public key.
		/// I.e. this constructor is usually only used when generating certificates produce their subject key identifiers.
		/// </summary>
		/// <param name="publicKey"></param>
		public KeyIdentifier(PublicKey publicKey) {
			wrapped = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey.wrapped));
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is KeyIdentifier identifier && wrapped.Equals(identifier.wrapped);
		/// <inheritdoc/>
		public override int GetHashCode() => wrapped.GetHashCode();
		/// <inheritdoc/>
		public override string? ToString() => wrapped.ToString();

		/// <summary>
		/// Provides access to the raw key identifier byte.
		/// </summary>
		public byte[] Identifier => wrapped.GetKeyIdentifier();
	}
}
