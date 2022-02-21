using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using System.Collections.Generic;
using System.Linq;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents a distinguished name used in X509 certificates (represented by <see cref="Certificate"/>) to identify the subject or the issuer.
	/// If consists of a hierarchy of key-value pair components, where the key identifies the kind of name component / layer and the value identifies the part of the name on that level.
	/// The keys are exposed as strings here but are limited to the names available in X509, e.g. <c>O</c> for organization, <c>OU</c> for organizational unit, or <c>CN</c> for common name.
	/// </summary>
	public class DistinguishedName {
		internal X509Name wrapped;

		internal DistinguishedName(X509Name wrapped) {
			this.wrapped = wrapped;
		}

		/// <summary>
		/// Constructs a distingiushed name object from the given key-value pairs.
		/// </summary>
		/// <param name="values">The key-value pairs in descending hierarchical order, i.e. the first element is the root.</param>
		public DistinguishedName(IEnumerable<KeyValuePair<string, string>> values) {
			wrapped = new X509Name(string.Join(',', values.Select(kvp => kvp.Key + "=" + kvp.Value)));
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is DistinguishedName name && wrapped.Equals(name.wrapped);
		/// <inheritdoc/>
		public override int GetHashCode() => wrapped.GetHashCode();
		/// <inheritdoc/>
		public override string? ToString() => wrapped.ToString();

		/// <summary>
		/// Allows iteration over the name components as key-value pairs.
		/// </summary>
		/// <returns>An <see cref="IEnumerable{T}"/> over the key-value pairs in descending hierarchical order.</returns>
		public IEnumerable<KeyValuePair<string, string>> EnumerateComponents() {
			var keys = wrapped.GetOidList().Cast<DerObjectIdentifier>().Select(oid => (string?)X509Name.DefaultSymbols[oid] ?? oid.Id);
			return keys.Zip(wrapped.GetValueList().Cast<string>(), (k, v) => new KeyValuePair<string, string>(k, v));
		}
	}
}
