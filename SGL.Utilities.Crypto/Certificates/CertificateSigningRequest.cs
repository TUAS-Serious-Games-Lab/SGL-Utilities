using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Pkcs;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Certificates {
	public class CertificateSigningRequest {
		internal Pkcs10CertificationRequest wrapped;

		internal CertificateSigningRequest(Pkcs10CertificationRequest wrapped) {
			this.wrapped = wrapped;
		}

		public CertificateCheckOutcome Verify() {
			try {
				return wrapped.Verify() ? CertificateCheckOutcome.Valid : CertificateCheckOutcome.InvalidSignature;
			}
			catch {
				return CertificateCheckOutcome.OtherError;
			}

		}

		public PublicKey SubjectPublicKey => new PublicKey(wrapped.GetPublicKey());

		public DistinguishedName SubjectDN => new DistinguishedName(wrapped.GetCertificationRequestInfo().Subject);

		public KeyUsages? RequestedKeyUsages {
			get {
				var usages = KeyUsages.NoneDefined;
				var keyUsageExt = KeyUsage.GetInstance(wrapped.GetRequestedExtensions()?.GetExtension(X509Extensions.KeyUsage));
				if (keyUsageExt != null) {
					var keyUsageBitFlags = keyUsageExt.IntValue;
					CertificateHelpers.MapBasicKeyUsageFlags(ref usages, keyUsageBitFlags);
				}
				var extKeyUsageExt = ExtendedKeyUsage.GetInstance(wrapped.GetRequestedExtensions()?.GetExtension(X509Extensions.ExtendedKeyUsage));
				if (extKeyUsageExt != null) {
					CertificateHelpers.MapExtendedKeyUsageFlags(ref usages, extKeyUsageExt);
				}
				return usages;
			}
		}

		public (bool IsCA, int? PathLength)? RequestedCABasicConstraints {
			get {
				var basicConstraintsExtObj = BasicConstraints.GetInstance(wrapped.GetRequestedExtensions()?.GetExtension(X509Extensions.BasicConstraints));
				if (basicConstraintsExtObj != null) {
					if (basicConstraintsExtObj.IsCA()) {
						return (true, basicConstraintsExtObj.PathLenConstraint?.IntValueExact);
					}
					else {
						return (false, null);
					}
				}
				else {
					return null;
				}
			}
		}

		public bool RequestedSubjectKeyIdentifier => wrapped.GetRequestedExtensions()?.GetExtensionOids()?.Contains(X509Extensions.SubjectKeyIdentifier) ?? false;

		public bool RequestedAuthorityKeyIdentifier => wrapped.GetRequestedExtensions()?.GetExtensionOids()?.Contains(X509Extensions.AuthorityKeyIdentifier) ?? false;

		public static CertificateSigningRequest Generate(DistinguishedName subjectDN, KeyPair subjectKeyPair, CertificateSignatureDigest digest,
				bool requestSubjectKeyIdentifier = false, bool requestAuthorityKeyIdentifier = false, KeyUsages? requestKeyUsages = null,
				(bool IsCA, int? PathLength)? requestCABasicConstraints = null) {
			X509ExtensionsGenerator generator = new X509ExtensionsGenerator();
			bool anyExtensions = false;
			if (requestSubjectKeyIdentifier) {
				anyExtensions = true;
				generator.AddExtension(X509Extensions.SubjectKeyIdentifier, true, new byte[0]);
			}
			if (requestAuthorityKeyIdentifier) {
				anyExtensions = true;
				generator.AddExtension(X509Extensions.AuthorityKeyIdentifier, true, new byte[0]);
			}
			if (requestKeyUsages.HasValue && (requestKeyUsages.Value & KeyUsages.AllBasic) != 0) {
				anyExtensions = true;
				generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage((int)(requestKeyUsages.Value & KeyUsages.AllBasic)).ToAsn1Object());
			}
			if (requestKeyUsages.HasValue && (requestKeyUsages.Value & KeyUsages.AllSupportedExt) != 0) {
				anyExtensions = true;
				generator.AddExtension(X509Extensions.ExtendedKeyUsage, true, GeneratorHelper.MapKeyUsageEnumToExtensionObject(requestKeyUsages.Value).ToAsn1Object());
			}
			if (requestCABasicConstraints != null) {
				anyExtensions = true;
				if (requestCABasicConstraints.Value.IsCA) {
					generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(requestCABasicConstraints.Value.PathLength ?? 0).ToAsn1Object());
				}
				else {
					generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false).ToAsn1Object());
				}
			}
			Asn1Set? attributes = null;
			if (anyExtensions) {
				attributes = new DerSet(new AttributePkcs(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(generator.Generate())));
			}
			var wrappedCSR = new Pkcs10CertificationRequest(new Asn1SignatureFactory(GeneratorHelper.GetSignerName(subjectKeyPair.Private.Type, digest), subjectKeyPair.Private.wrapped),
				subjectDN.wrapped, subjectKeyPair.Public.wrapped, attributes);
			return new CertificateSigningRequest(wrappedCSR);
		}

		public Certificate GenerateCertificate(Certificate signerCertificate, KeyPair signerKeyPair, CsrSigningPolicy policy) {
			if (!signerCertificate.PublicKey.Equals(signerKeyPair.Public)) {
				throw new ArgumentException("Given signer key pair doesn't match the given signer certificate.");
			}
			var validityPeriod = policy.GetValidityPeriod();
			KeyIdentifier? authorityKeyIdentifier = null;
			if (policy.ShouldGenerateAuthorityKeyIdentifier(RequestedAuthorityKeyIdentifier)) {
				authorityKeyIdentifier = signerCertificate.SubjectKeyIdentifier;
				if (authorityKeyIdentifier == null) {
					throw new InvalidOperationException("Policy indicates that AuthorityKeyIdentifier extension shall be generated, but signer certificate doesn't have SubjectKeyIdentifier.");
				}
			}
			return Certificate.Generate(signerCertificate.SubjectDN, signerKeyPair.Private, SubjectDN, SubjectPublicKey, validityPeriod.From, validityPeriod.To, policy.GetSerialNumber(),
				policy.GetSignatureDigest(), authorityKeyIdentifier, policy.ShouldGenerateSubjectKeyIdentifier(RequestedSubjectKeyIdentifier),
				policy.AcceptedKeyUsages(RequestedKeyUsages) ?? KeyUsages.NoneDefined, policy.AcceptedCAConstraints(RequestedCABasicConstraints));
		}
	}

	public class CsrSigningPolicy {
		public RandomGenerator Random { get; set; }
		public CsrSigningPolicy() : this(new RandomGenerator()) { }

		public CsrSigningPolicy(RandomGenerator random) {
			Random = random;
			GetSerialNumber = () => GenerateRandomSerialNumber(128);
		}

		public byte[] GenerateRandomSerialNumber(int length) {
			return Random.GetBytes(length);
		}

		public Func<(DateTime From, DateTime To)> GetValidityPeriod { get; set; } = () => {
			var now = DateTime.UtcNow;
			return (now, now.AddYears(3));
		};

		public Func<byte[]> GetSerialNumber { get; set; }

		public Func<CertificateSignatureDigest> GetSignatureDigest { get; set; } = () => CertificateSignatureDigest.Sha256;

		public Func<bool, bool> ShouldGenerateSubjectKeyIdentifier { get; set; } = requested => requested;

		public Func<bool, bool> ShouldGenerateAuthorityKeyIdentifier { get; set; } = requested => requested;

		public Func<KeyUsages?, KeyUsages?> AcceptedKeyUsages { get; set; } = requested => requested & ~(KeyUsages.CrlSign | KeyUsages.KeyCertSign);

		public Func<(bool IsCA, int? PathLength)?, (bool IsCA, int? PathLength)?> AcceptedCAConstraints { get; set; } = requested => null;
	}

	public static class CsrSigningPolicyExtensions {
		public static CsrSigningPolicy UseFixedValidityPeriod(this CsrSigningPolicy policy, DateTime from, DateTime to) {
			policy.GetValidityPeriod = () => (from.ToUniversalTime(), to.ToUniversalTime());
			return policy;
		}

		public static CsrSigningPolicy UseValidityDuration(this CsrSigningPolicy policy, TimeSpan duration) {
			policy.GetValidityPeriod = () => {
				var now = DateTime.UtcNow;
				return (now, now + duration);
			};
			return policy;
		}

		public static CsrSigningPolicy UseSignatureDigest(this CsrSigningPolicy policy, CertificateSignatureDigest digest) {
			policy.GetSignatureDigest = () => digest;
			return policy;
		}

		public static CsrSigningPolicy ForceKeyIdentifiers(this CsrSigningPolicy policy) {
			policy.ShouldGenerateAuthorityKeyIdentifier = _ => true;
			policy.ShouldGenerateSubjectKeyIdentifier = _ => true;
			return policy;
		}

		public static CsrSigningPolicy ForceKeyUsages(this CsrSigningPolicy policy, KeyUsages? forcedKeyUsages) {
			policy.AcceptedKeyUsages = _ => forcedKeyUsages;
			return policy;
		}
		public static CsrSigningPolicy ForceCAConstraints(this CsrSigningPolicy policy, (bool IsCA, int? PathLength)? forcedCAConstraints) {
			policy.AcceptedCAConstraints = _ => forcedCAConstraints;
			return policy;
		}
		public static CsrSigningPolicy AllowExtensionRequestsForCA(this CsrSigningPolicy policy) {
			policy.AcceptedKeyUsages = requested => requested;
			policy.AcceptedCAConstraints = requested => requested;
			return policy;
		}
	}
}
