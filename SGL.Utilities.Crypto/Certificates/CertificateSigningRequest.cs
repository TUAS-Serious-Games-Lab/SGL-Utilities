using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Pkcs;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents a request for a cryptographic certificate that the subject requests an issuer to sign into a certificate.
	/// It contains a distinguished name for the subject, the subject's public key, and optionally extensions that the subject requests the issuer to use.
	/// The request itself is signed by the subject and can be verified using <see cref="Verify()"/>.
	/// The issuer can use <see cref="GenerateCertificate(Certificate, KeyPair, CsrSigningPolicy)"/> to generate a certificate from the request data, according to their specified policies.
	/// </summary>
	public class CertificateSigningRequest {
		internal Pkcs10CertificationRequest wrapped;
		private KeyId? cachedKeyId;

		internal CertificateSigningRequest(Pkcs10CertificationRequest wrapped) {
			this.wrapped = wrapped;
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is CertificateSigningRequest csr && wrapped.Equals(csr.wrapped);
		/// <inheritdoc/>
		public override int GetHashCode() => wrapped.GetHashCode();
		/// <inheritdoc/>
		public override string? ToString() => $"{SubjectDN} | {cachedKeyId ??= SubjectPublicKey.CalculateId()}";

		/// <summary>
		/// Verifies the subject's signature on the certificate.
		/// </summary>
		/// <returns><see cref="CertificateCheckOutcome.Valid"/> upon success, <see cref="CertificateCheckOutcome.InvalidSignature"/> if the signature is is incorrect,
		/// or <see cref="CertificateCheckOutcome.OtherError"/> if an error was encountered during the verification process.</returns>
		public CertificateCheckOutcome Verify() {
			try {
				return wrapped.Verify() ? CertificateCheckOutcome.Valid : CertificateCheckOutcome.InvalidSignature;
			}
			catch {
				return CertificateCheckOutcome.OtherError;
			}

		}

		/// <summary>
		/// The public key of the requesting subject.
		/// </summary>
		public PublicKey SubjectPublicKey => new(wrapped.GetPublicKey());

		/// <summary>
		/// The distinguished name identifying the requesting subject.
		/// </summary>
		public DistinguishedName SubjectDN => new(wrapped.GetCertificationRequestInfo().Subject);

		/// <summary>
		/// If the certificate signing request requests specific usages of the associated key pair to be allowed by the certificate using the KeyUsage and ExtendedKeyUsage extensions,
		/// provides the requested key usages, otherwise null.
		/// </summary>
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

		/// <summary>
		/// If the certificate signing request requests specific constraints regarding usage as certificate authority using the BasicConstraints extension,
		/// provides the requested constraints, otherwise null.
		/// </summary>
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

		/// <summary>
		/// Indicates whether the requested extensions of the certificate signing request contain the SubjectKeyIdentifier extension.
		/// </summary>
		public bool RequestedSubjectKeyIdentifier => wrapped.GetRequestedExtensions()?.GetExtensionOids()?.Contains(X509Extensions.SubjectKeyIdentifier) ?? false;

		/// <summary>
		/// Indicates whether the requested extensions of the certificate signing request contain the AuthorityKeyIdentifier extension.
		/// </summary>
		public bool RequestedAuthorityKeyIdentifier => wrapped.GetRequestedExtensions()?.GetExtensionOids()?.Contains(X509Extensions.AuthorityKeyIdentifier) ?? false;

		/// <summary>
		/// Builds a new CertificateSigningRequest using the given data.
		/// </summary>
		/// <param name="subjectDN">The distinguished name identifying the subject.</param>
		/// <param name="subjectKeyPair">The key pair used by the subject.</param>
		/// <param name="digest">The digest algorithm with which to sign the request.</param>
		/// <param name="requestSubjectKeyIdentifier">Whether the SubjectKeyIdentifier extension shall be requested.</param>
		/// <param name="requestAuthorityKeyIdentifier">Whether the AuthorityKeyIdentifier extension shall be requested.</param>
		/// <param name="requestKeyUsages">When set, requests specific key usages for <paramref name="subjectKeyPair"/>.</param>
		/// <param name="requestCABasicConstraints">When set, requests constraints about the usage for CA purposes.</param>
		/// <returns>The created <see cref="CertificateSigningRequest"/>.</returns>
		public static CertificateSigningRequest Generate(DistinguishedName subjectDN, KeyPair subjectKeyPair, SignatureDigest digest = SignatureDigest.Sha256,
				bool requestSubjectKeyIdentifier = false, bool requestAuthorityKeyIdentifier = false, KeyUsages? requestKeyUsages = null,
				(bool IsCA, int? PathLength)? requestCABasicConstraints = null) {
			var generator = new X509ExtensionsGenerator();
			bool anyExtensions = false;
			if (requestSubjectKeyIdentifier) {
				anyExtensions = true;
				generator.AddExtension(X509Extensions.SubjectKeyIdentifier, true, Array.Empty<byte>());
			}
			if (requestAuthorityKeyIdentifier) {
				anyExtensions = true;
				generator.AddExtension(X509Extensions.AuthorityKeyIdentifier, true, Array.Empty<byte>());
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
			var wrappedCSR = new Pkcs10CertificationRequest(new Asn1SignatureFactory(SignatureHelper.GetSignatureAlgorithmName(subjectKeyPair.Private.Type, digest), subjectKeyPair.Private.wrapped),
				subjectDN.wrapped, subjectKeyPair.Public.wrapped, attributes);
			return new CertificateSigningRequest(wrappedCSR);
		}

		/// <summary>
		/// Creates a <see cref="Certificate"/> using the <see cref="CertificateSigningRequest"/>.
		/// The certificate is signed by the issuer represented by <paramref name="signerCertificate"/> and <paramref name="signerKeyPair"/>.
		/// The <paramref name="policy"/> allows modifying the data of the CSR before they are used in the certificate.
		/// This e.g. allows the issuer to restrict which key usages are acceptable.
		/// The <paramref name="policy"/> also specifies how the serial number of the certificate is generated and for what period it shall be valid.
		/// </summary>
		/// <param name="signerCertificate"></param>
		/// <param name="signerKeyPair"></param>
		/// <param name="policy"></param>
		/// <returns>The signed certificate.</returns>
		/// <exception cref="CertificateException">If the CSR fails verification before creating the certificate.</exception>
		public Certificate GenerateCertificate(Certificate signerCertificate, KeyPair signerKeyPair, CsrSigningPolicy policy) {
			var verificationResult = Verify();
			if (verificationResult != CertificateCheckOutcome.Valid) {
				throw new CertificateException("The certification request failed verification.");
			}
			if (!signerCertificate.PublicKey.Equals(signerKeyPair.Public)) {
				throw new ArgumentException("Given signer key pair doesn't match the given signer certificate.");
			}
			if (!signerCertificate.AllowedKeyUsages.HasValue) {
				throw new CertificateException("The given signer certificate doesn't have allowed key usages defined. A certificate used for signing other certificate needs to have a KeyUsage extension with KeyCertSign.");
			}
			if (!signerCertificate.AllowedKeyUsages.Value.HasFlag(KeyUsages.KeyCertSign)) {
				throw new CertificateException("The given signer certificate has a KeyUsage extension without KeyCertSign. To allow use for siging another certificate, it needs to have KeyCertSign set.");
			}
			if (!signerCertificate.CABasicConstraints.HasValue) {
				throw new CertificateException("The given signer certificate doesn't have CA basic constraints defined. A certificate used for signing other certificate needs to have a BasicConstraints extension with CA=true.");

			}
			if (!signerCertificate.CABasicConstraints.Value.IsCA) {
				throw new CertificateException("The given signer certificate has a BasicConstraints extension with CA=false. This forbids it from being used for siging another certificate.");
			}
			var (ValidFrom, ValidTo) = policy.GetValidityPeriod();
			KeyIdentifier? authorityKeyIdentifier = null;
			if (policy.ShouldGenerateAuthorityKeyIdentifier(RequestedAuthorityKeyIdentifier)) {
				authorityKeyIdentifier = signerCertificate.SubjectKeyIdentifier;
				if (authorityKeyIdentifier == null) {
					throw new InvalidOperationException("Policy indicates that AuthorityKeyIdentifier extension shall be generated, but signer certificate doesn't have SubjectKeyIdentifier.");
				}
			}
			return Certificate.Generate(signerCertificate.SubjectDN, signerKeyPair.Private, SubjectDN, SubjectPublicKey, ValidFrom, ValidTo, policy.GetSerialNumber(),
				policy.GetSignatureDigest(), authorityKeyIdentifier, policy.ShouldGenerateSubjectKeyIdentifier(RequestedSubjectKeyIdentifier),
				policy.AcceptedKeyUsages(RequestedKeyUsages) ?? KeyUsages.NoneDefined, policy.AcceptedCAConstraints(RequestedCABasicConstraints));
		}

		/// <summary>
		/// Loads one certificate signing request from the PEM-encoded data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader containing at least one PEM-encoded certificate signing request.</param>
		/// <returns>The loaded certificate signing request.</returns>
		/// <exception cref="PemException">When the reader either contained no PEM objects or if the PEM object that was read was no certificate signing request.</exception>
		public static CertificateSigningRequest LoadOneFromPem(TextReader reader) => PemHelper.TryLoadCertificateSigningRequest(reader) ?? throw new PemException("Input contained no PEM objects.");
		/// <summary>
		/// Attempts to load one certificate signing request from the PEM-encoded data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader containing at least one PEM-encoded certificate signing request.</param>
		/// <returns>The loaded certificate signing request, or null if <paramref name="reader"/> contains not PEM objects.</returns>
		/// <exception cref="PemException">When the the PEM object read from <paramref name="reader"/> was no certificate signing request.</exception>
		public static CertificateSigningRequest? TryLoadOneFromPem(TextReader reader) => PemHelper.TryLoadCertificateSigningRequest(reader);
		/// <summary>
		/// Loads all certificate signing requests from the PEM-encoded data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader containing PEM-encoded certificate signing requests.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> containing all loaded certificate signing requests.</returns>
		/// <exception cref="PemException">When non-CSR objects were encountered in the PEM data.</exception>
		public static IEnumerable<CertificateSigningRequest> LoadAllFromPem(TextReader reader) => PemHelper.LoadCertificateSigningRequests(reader);
		/// <summary>
		/// Writes the certificate signing request to <paramref name="writer"/> in PEM-encoded form.
		/// </summary>
		/// <param name="writer">The writer to which the certificate signing request should be written.</param>
		public void StoreToPem(TextWriter writer) => PemHelper.Write(writer, this);

	}

	/// <summary>
	/// Specifies the policies that a signing authority intends to apply when issuing a certificate based on a certificate signing request.
	/// </summary>
	public class CsrSigningPolicy {
		internal RandomGenerator Random { get; set; }

		/// <summary>
		/// Like <see cref="CsrSigningPolicy()"/> but creates a new <see cref="RandomGenerator"/>.
		/// </summary>
		public CsrSigningPolicy() : this(new RandomGenerator()) { }

		/// <summary>
		/// Creates a defaulted policy object using the given random generator.
		/// </summary>
		/// <param name="random">The random generator to use for <see cref="GenerateRandomSerialNumber(int)"/>.</param>
		public CsrSigningPolicy(RandomGenerator random) {
			Random = random;
			GetSerialNumber = () => GenerateRandomSerialNumber(128);
		}

		/// <summary>
		/// Generates a new random vertificate serial number of the given length.
		/// </summary>
		/// <param name="length">The length (in bits) of the serial number.</param>
		/// <returns>The generated serial number.</returns>
		public byte[] GenerateRandomSerialNumber(int length) {
			return Random.GetRandomBigInteger(length).ToByteArray();
		}

		/// <summary>
		/// A function object to obtain the validity period of the certificate.
		/// This is usually set by <see cref="CsrSigningPolicyExtensions.UseFixedValidityPeriod(CsrSigningPolicy, DateTime, DateTime)"/> or
		/// by <see cref="CsrSigningPolicyExtensions.UseValidityDuration(CsrSigningPolicy, TimeSpan)"/> and defaults to 3 years, starting at the time the getter is called.
		/// </summary>
		public Func<(DateTime From, DateTime To)> GetValidityPeriod { get; set; } = () => {
			var now = DateTime.UtcNow;
			return (now, now.AddYears(3));
		};

		/// <summary>
		/// A function object to obtain the serial number for the certificate.
		/// Defaults to <see cref="GenerateRandomSerialNumber(int)"/> with a length of 128.
		/// </summary>
		public Func<byte[]> GetSerialNumber { get; set; }

		/// <summary>
		/// A function object that specifies the digest to use for the signature of the certificate.
		/// Defaults to <see cref="SignatureDigest.Sha256"/>.
		/// </summary>
		public Func<SignatureDigest> GetSignatureDigest { get; set; } = () => SignatureDigest.Sha256;

		/// <summary>
		/// A function object that indicates whether the SubjectKeyIdentifier extension shall be generated.
		/// The funtion object is passed a boolean indicating whether the certificate signing request requests this extension and thus can follow that request or override the behavior.
		/// The default function simply follows the request.
		/// </summary>
		public Func<bool, bool> ShouldGenerateSubjectKeyIdentifier { get; set; } = requested => requested;

		/// <summary>
		/// A function object that indicates whether the AuthorityKeyIdentifier extension shall be generated.
		/// The funtion object is passed a boolean indicating whether the certificate signing request requests this extension and thus can follow that request or override the behavior.
		/// The default function simply follows the request.
		/// </summary>
		public Func<bool, bool> ShouldGenerateAuthorityKeyIdentifier { get; set; } = requested => requested;

		/// <summary>
		/// A function object to decide the key usages for the created certificate based on the requested key usages.
		/// The latter are passed to the function object, the former are what the function object is expected to be returned.
		/// In both cases, a null value indicates that the relevant extensions shall not be created.
		/// The default function forbids <see cref="KeyUsages.KeyCertSign"/> and <see cref="KeyUsages.CrlSign"/>, as they are only relevant to CA certificates which are a special case,
		/// and removes these usages from the requested usages. The remaining usages are passed on from the request.
		/// </summary>
		public Func<KeyUsages?, KeyUsages?> AcceptedKeyUsages { get; set; } = requested => requested & ~(KeyUsages.CrlSign | KeyUsages.KeyCertSign);

		/// <summary>
		/// A function object to decide the contrained about use as a CA that shall be created in the created certificate,
		/// based on the contraints requested by the certificate signing request.
		/// The latter are passed to the function object, the former are what the function object is expected to be returned.
		/// In both cases, a null value indicates that the relevant extensions shall not be created.
		/// The default function prevents usage as a CA by forcing a BasicConstraint extension with CA = false.
		/// </summary>
		public Func<(bool IsCA, int? PathLength)?, (bool IsCA, int? PathLength)?> AcceptedCAConstraints { get; set; } = requested => (false, null);
	}

	/// <summary>
	/// Provides extension methods for <see cref="CsrSigningPolicy"/> to setup typical policies more easily.
	/// </summary>
	public static class CsrSigningPolicyExtensions {
		/// <summary>
		/// Uses a fixed validity period, specified by the given <see cref="DateTime"/>s.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <param name="from">The time from which the certificate shall be valid.</param>
		/// <param name="to">The time until which the certificate shall be valid.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy UseFixedValidityPeriod(this CsrSigningPolicy policy, DateTime from, DateTime to) {
			policy.GetValidityPeriod = () => (from.ToUniversalTime(), to.ToUniversalTime());
			return policy;
		}

		/// <summary>
		/// Uses the specified duration for the validity of the certificate, starting from the time when it is issued.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <param name="duration">How long the certificate shall be valid.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy UseValidityDuration(this CsrSigningPolicy policy, TimeSpan duration) {
			policy.GetValidityPeriod = () => {
				var now = DateTime.UtcNow;
				return (now, now + duration);
			};
			return policy;
		}

		/// <summary>
		/// Uses the specified digest for the signature of the certificate.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <param name="digest">The digest to use.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy UseSignatureDigest(this CsrSigningPolicy policy, SignatureDigest digest) {
			policy.GetSignatureDigest = () => digest;
			return policy;
		}

		/// <summary>
		/// Forces the use of the SubjectKeyIdentifier and AuthorityKeyIdentifier extensions, regardless of what is requested in the CSR.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy ForceKeyIdentifiers(this CsrSigningPolicy policy) {
			policy.ShouldGenerateAuthorityKeyIdentifier = _ => true;
			policy.ShouldGenerateSubjectKeyIdentifier = _ => true;
			return policy;
		}

		/// <summary>
		/// Forces the use of the specified key usages, regardless of what is requested in the CSR.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <param name="forcedKeyUsages">The key usages to allow, null omits the extension.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy ForceKeyUsages(this CsrSigningPolicy policy, KeyUsages? forcedKeyUsages) {
			policy.AcceptedKeyUsages = _ => forcedKeyUsages;
			return policy;
		}
		/// <summary>
		/// Forces the use of the specified CA constraints, regardless of what is requested in the CSR.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <param name="forcedCAConstraints">The CA constraints to specify in the certificate, null omits the extension.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy ForceCAConstraints(this CsrSigningPolicy policy, (bool IsCA, int? PathLength)? forcedCAConstraints) {
			policy.AcceptedCAConstraints = _ => forcedCAConstraints;
			return policy;
		}

		/// <summary>
		/// Allows the CSR to request CA-specific features and have them issued in the certificate, namly lets <see cref="CsrSigningPolicy.AcceptedCAConstraints"/> and
		/// <see cref="CsrSigningPolicy.AcceptedKeyUsages"/> keep what was requested instead of blocking <see cref="KeyUsages.KeyCertSign"/> and <see cref="KeyUsages.CrlSign"/> and
		/// forcing a CA=false constraint.
		/// </summary>
		/// <param name="policy">The policy object to modify.</param>
		/// <returns>A reference to <paramref name="policy"/> for chaining.</returns>
		public static CsrSigningPolicy AllowExtensionRequestsForCA(this CsrSigningPolicy policy) {
			policy.AcceptedKeyUsages = requested => requested;
			policy.AcceptedCAConstraints = requested => requested;
			return policy;
		}
	}
}
