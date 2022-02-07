using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto {
	public class CertificateStore {
		private readonly ILogger<CertificateStore> logger;
		private readonly ICertificateValidator validator;
		private Dictionary<KeyId, X509Certificate> certificatesByKeyId = new Dictionary<KeyId, X509Certificate>();
		private Dictionary<X509Name, X509Certificate> certificatesBySubjectDN = new Dictionary<X509Name, X509Certificate>();
		private Dictionary<SubjectKeyIdentifier, X509Certificate> certificatesBySKID = new Dictionary<SubjectKeyIdentifier, X509Certificate>();

		public CertificateStore(ILogger<CertificateStore> logger, ICertificateValidator validator) {
			this.logger = logger;
			this.validator = validator;
		}

		public X509Certificate? GetCertificateByKeyId(KeyId id) {
			if (certificatesByKeyId.TryGetValue(id, out var cert)) {
				return cert;
			}
			else {
				return null;
			}
		}

		public X509Certificate? GetCertificateBySubjectDN(X509Name subjectDN) {
			if (certificatesBySubjectDN.TryGetValue(subjectDN, out var cert)) {
				return cert;
			}
			else {
				return null;
			}
		}

		public X509Certificate? GetCertificateBySubjectKeyIdentifier(SubjectKeyIdentifier skid) {
			if (certificatesBySKID.TryGetValue(skid, out var cert)) {
				return cert;
			}
			else {
				return null;
			}
		}

		public IEnumerable<KeyId> ListKnownKeyIds() {
			return certificatesByKeyId.Keys;
		}
		public IEnumerable<X509Name> ListKnownSubjectDNs() {
			return certificatesBySubjectDN.Keys;
		}
		public IEnumerable<SubjectKeyIdentifier> ListKnownSubjectKeyIdentifiers() {
			return certificatesBySKID.Keys;
		}
		public IEnumerable<X509Certificate> ListKnownCertificates() {
			return certificatesByKeyId.Values;
		}
		public IEnumerable<AsymmetricKeyParameter> ListKnownPublicKeys() {
			return ListKnownCertificates().Select(cert => cert.GetPublicKey());
		}

		public void LoadCertificatesFromEmbeddedStringConstant(string pemContent) {
			LoadCertificatesFromReader(new StringReader(pemContent), "[embedded data]");
		}

		public Task LoadCertificatesFromHttpAsync(Uri source, CancellationToken ct = default) {
			HttpClient httpClient = new();
			return LoadCertificatesFromHttpAsync(httpClient, source, ct);
		}

		public async Task LoadCertificatesFromHttpAsync(HttpClient httpClient, Uri source, CancellationToken ct = default) {
			using var reader = new StreamReader(await httpClient.GetStreamAsync(source, ct), Encoding.UTF8);
			LoadCertificatesFromReader(reader, source.AbsoluteUri);
		}

		public void LoadCertificatesFromDirectory(string directoryPath, string fileNamePattern = "*.pem") {
			var files = Directory.EnumerateFiles(directoryPath, fileNamePattern);
			foreach (var file in files) {
				using var fileReader = File.OpenText(file);
				LoadCertificatesFromReader(fileReader, file);
			}
		}

		public void LoadCertificatesFromReader(TextReader reader, string sourceName) {
			var certs = loadCertificates(reader, sourceName);
			foreach (var cert in certs) {
				var keyid = KeyId.CalculateId(cert.GetPublicKey());
				if (validator.CheckCertificate(cert)) {
					certificatesByKeyId[keyid] = cert;
					certificatesBySubjectDN[cert.SubjectDN] = cert;
					var skid = cert.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
					if (skid != null) {
						certificatesBySKID[new SubjectKeyIdentifier(skid)] = cert;
					}
				}
				else {
					logger.LogWarning("The certificate with subject {subject} and key ID {keyid} from {source} failed validation. It will not be added to the certificate store.", cert.SubjectDN, keyid, sourceName);
				}
			}
		}

		private List<X509Certificate> loadCertificates(TextReader reader, string sourceName) {
			PemReader pemReader = new PemReader(reader);
			List<X509Certificate> certs = new List<X509Certificate>();
			object content;
			while ((content = pemReader.ReadObject()) != null) {
				if (content is X509Certificate cert) {
					certs.Add(cert);
				}
				else {
					logger.LogWarning("Source {src} contained an object of type {type}, expecting X509Certificate objects, ignoring this object.", sourceName, content.GetType().FullName);
				}
			}
			if (certs.Count == 0) {
				logger.LogWarning("Source {src} contained no valid certificates.", sourceName);
			}
			return certs;
		}
	}
}
