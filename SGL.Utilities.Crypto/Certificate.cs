using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Utilities.Crypto {
	public class Certificate {
		internal X509Certificate wrapped;

		internal Certificate(X509Certificate wrapped) {
			this.wrapped = wrapped;
		}

		public override bool Equals(object? obj) => obj is Certificate certificate && wrapped.Equals(certificate.wrapped);
		public override int GetHashCode() => wrapped.GetHashCode();
		public override string? ToString() => wrapped.ToString();

		public PublicKey PublicKey => new PublicKey(wrapped.GetPublicKey());
		public DistinguishedName SubjectDN => new DistinguishedName(wrapped.SubjectDN);
		public DistinguishedName IssuerDN => new DistinguishedName(wrapped.IssuerDN);
		public DateTime NotBefore => wrapped.NotBefore;
		public DateTime NotAfter => wrapped.NotAfter;
		public byte[] SerialNumber => wrapped.SerialNumber.ToByteArrayUnsigned();

		public KeyIdentifier? SubjectKeyIdentifier {
			get {
				if (skidCache == null) {
					var skid = wrapped.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
					if (skid == null) {
						return null;
					}
					else {
						skidCache = new KeyIdentifier(Org.BouncyCastle.Asn1.X509.SubjectKeyIdentifier.GetInstance(Asn1Object.FromByteArray(skid.GetOctets())));
					}
				}
				return skidCache;
			}
		}
		private KeyIdentifier? skidCache = null;
		public (KeyIdentifier? KeyIdentifier, object? Issuer, byte[]? SerialNumber)? AuthorityIdentifier {
			get {
				if (akidCache == null) {
					var akidEnc = wrapped.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
					if (akidEnc == null) {
						return null;
					}
					else {
						var akid = Org.BouncyCastle.Asn1.X509.AuthorityKeyIdentifier.GetInstance(Asn1Object.FromByteArray(akidEnc.GetOctets()));
						var akidRaw = akid?.GetKeyIdentifier();
						var keyIdent = akidRaw != null ? new KeyIdentifier(new SubjectKeyIdentifier(akidRaw)) : null;
						akidCache = (keyIdent, akid?.AuthorityCertIssuer, akid?.AuthorityCertSerialNumber?.ToByteArrayUnsigned());
					}
				}
				return akidCache;
			}
		}
		private (KeyIdentifier? KeyIdentifier, object? Issuer, byte[]? SerialNumber)? akidCache = null;

		public static Certificate LoadOneFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadCertificate(reader);
		public static IEnumerable<Certificate> LoadAllFromPem(TextReader reader, Func<char[]> passwordGetter) => PemHelper.LoadCertificates(reader);
	}
}
