using Org.BouncyCastle.Asn1.X509;
using System.Collections.Generic;
using System.Linq;

namespace SGL.Utilities.Crypto {
	public class DistinguishedName {
		internal X509Name wrapped;

		internal DistinguishedName(X509Name wrapped) {
			this.wrapped = wrapped;
		}

		public DistinguishedName(IEnumerable<KeyValuePair<string, string>> values) {
			wrapped = new X509Name(string.Join(',', values.Select(kvp => kvp.Key + "=" + kvp.Value)));
		}

		public override bool Equals(object? obj) => obj is DistinguishedName name && wrapped.Equals(name.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();
	}
}
