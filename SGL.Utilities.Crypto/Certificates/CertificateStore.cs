using Microsoft.Extensions.Logging;
using Org.BouncyCastle.OpenSsl;
using SGL.Utilities.Crypto.Internals;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Certificates {

	/// <summary>
	/// Loads, holds and indexes certificates to provide access to them.
	/// Upon loading, the certifiactes are validated using a <see cref="ICertificateValidator"/> given at construction.
	/// </summary>
	public class CertificateStore {
		private readonly ILogger<CertificateStore> logger;
		private readonly ICertificateValidator validator;
		private Dictionary<KeyId, Certificate> certificatesByKeyId = new Dictionary<KeyId, Certificate>();
		private Dictionary<DistinguishedName, Certificate> certificatesBySubjectDN = new Dictionary<DistinguishedName, Certificate>();
		private Dictionary<KeyIdentifier, Certificate> certificatesBySKID = new Dictionary<KeyIdentifier, Certificate>();

		/// <summary>
		/// Creates a certifiacte store that uses the given <see cref="ICertificateValidator"/> to validate loaded certificates and the given logger to log information about its operations.
		/// </summary>
		/// <param name="validator">The <see cref="ICertificateValidator"/> to use to validate certificates when they are loaded into the certificate store.</param>
		/// <param name="logger">The logger to use for logging the operations of the certificate store.</param>
		public CertificateStore(ICertificateValidator validator, ILogger<CertificateStore> logger) {
			this.validator = validator;
			this.logger = logger;
		}

		/// <summary>
		/// Looks up a certificate using the <see cref="KeyId"/> of its public key.
		/// </summary>
		/// <param name="id">The id of the public key of the certificate to find.</param>
		/// <returns>The certificate object, or <see langword="null"/> if no matching certifiacte was found.</returns>
		public Certificate? GetCertificateByKeyId(KeyId id) {
			if (certificatesByKeyId.TryGetValue(id, out var cert)) {
				return cert;
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Looks up a certificate using its SubjectDistinguishedName.
		/// </summary>
		/// <param name="subjectDN">The SubjectDistinguishedName of the certificate to find.</param>
		/// <returns>The certificate object, or <see langword="null"/> if no matching certifiacte was found.</returns>
		public Certificate? GetCertificateBySubjectDN(DistinguishedName subjectDN) {
			if (certificatesBySubjectDN.TryGetValue(subjectDN, out var cert)) {
				return cert;
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Looks up a certificate using its value of the SubjectKeyIdentifier extension.
		/// </summary>
		/// <param name="skid">The id to lookup the certificate by using its SubjectKeyIdentifier.</param>
		/// <returns>The certificate object, or <see langword="null"/> if no matching certifiacte was found.</returns>
		public Certificate? GetCertificateBySubjectKeyIdentifier(KeyIdentifier skid) {
			if (certificatesBySKID.TryGetValue(skid, out var cert)) {
				return cert;
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Lists the <see cref="KeyId"/> of all certificates contained in the certificate store.
		/// </summary>
		/// <returns>An enumerable over all <see cref="KeyId"/>s.</returns>
		public IEnumerable<KeyId> ListKnownKeyIds() => certificatesByKeyId.Keys;
		/// <summary>
		/// Lists the SubjectDistinguishedNames of all certificates contained in the certificate store.
		/// </summary>
		/// <returns>An enumerable over all SubjectDistinguishedNames as <see cref="DistinguishedName"/>s.</returns>
		public IEnumerable<DistinguishedName> ListKnownSubjectDNs() => certificatesBySubjectDN.Keys;
		/// <summary>
		/// Lists the SubjectKeyIdentifier values of all certificates contained in the certificate store that have that extension.
		/// </summary>
		/// <returns>An enumerable over all <see cref="KeyIdentifier"/>s.</returns>
		public IEnumerable<KeyIdentifier> ListKnownSubjectKeyIdentifiers() => certificatesBySKID.Keys;

		/// <summary>
		/// Lists all certificates contained in the certificate store.
		/// </summary>
		/// <returns>An enumerable over all <see cref="Certificate"/>s.</returns>
		public IEnumerable<Certificate> ListKnownCertificates() => certificatesByKeyId.Values;
		/// <summary>
		/// Lists the public keys of all certificates in the certificate store.
		/// </summary>
		/// <returns>An enumerable over all public keys as <see cref="PublicKey"/>s.</returns>
		public IEnumerable<PublicKey> ListKnownPublicKeys() => ListKnownCertificates().Select(cert => cert.PublicKey);
		/// <summary>
		/// Lists all certificates contained in the certificate store as <see cref="KeyValuePair{KeyId, PublicKey}"/>s of the id of their public key paired with the actual certificate object.
		/// </summary>
		/// <returns>An enumerable over all public key ids paired with the certificate, as <see cref="KeyValuePair{KeyId, PublicKey}"/>.</returns>
		public IEnumerable<KeyValuePair<KeyId, PublicKey>> ListKnownKeyIdsAndPublicKeys() => certificatesByKeyId.Select(keyIdCert => new KeyValuePair<KeyId, PublicKey>(keyIdCert.Key, keyIdCert.Value.PublicKey));
		/// <summary>
		/// Loads and verifies certificates from the given string in PEM format.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="pemContent">A string containing the certificates in PEM format.</param>
		public void LoadCertificatesFromEmbeddedStringConstant(string pemContent) => LoadCertificatesFromReader(new StringReader(pemContent), "[embedded data]");

		/// <summary>
		/// Asynchronously downloads, loads, and verifies certificates from the given HTTP(S) URI in PEM format.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="source">A URI to download the certificates from using <see cref="HttpClient"/>.</param>
		/// <param name="ct">A cancellation token to allow cancelling of the asynchronous download operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		public Task LoadCertificatesFromHttpAsync(Uri source, CancellationToken ct = default) => LoadCertificatesFromHttpAsync(new(), source, ct);

		/// <summary>
		/// Asynchronously downloads, loads, and verifies certificates from the given HTTP(S) URI in PEM format.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> object to use for downloading.</param>
		/// <param name="source">A URI to download the certificates from using <paramref name="httpClient"/>.</param>
		/// <param name="ct">A cancellation token to allow cancelling of the asynchronous download operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async Task LoadCertificatesFromHttpAsync(HttpClient httpClient, Uri source, CancellationToken ct = default) {
			using var reader = new StreamReader(
#if NETSTANDARD
				await httpClient.GetStreamAsync(source/*, ct*/), 
#else
				await httpClient.GetStreamAsync(source, ct),
#endif
				Encoding.UTF8);
			ct.ThrowIfCancellationRequested();
			LoadCertificatesFromReader(reader, source.AbsoluteUri);
		}

		/// <summary>
		/// Asynchronously downloads, loads, and verifies certificates in PEM format obtained from the response to the given HTTP(S) request.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> object to use for downloading.</param>
		/// <param name="request">The request to perform using <paramref name="httpClient"/> to obtain the certificates.</param>
		/// <param name="ct">A cancellation token to allow cancelling of the asynchronous download operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async Task LoadCertificatesFromHttpAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken ct = default) {
			using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
			response.EnsureSuccessStatusCode();
			ct.ThrowIfCancellationRequested();
			await LoadCertificatesFromHttpAsync(response, ct);
		}


		/// <summary>
		/// Asynchronously downloads, loads, and verifies certificates in PEM format obtained from the given HTTP(S) response.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="response">An HTTP(S) response the content of which provides the certificates in PEM form.</param>
		/// <param name="ct">A cancellation token to allow cancelling of the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async Task LoadCertificatesFromHttpAsync(HttpResponseMessage response, CancellationToken ct = default) {
			response.EnsureSuccessStatusCode();
			// TODO: Switch to temp file if content length is over a threshold
			using var buffer = new MemoryStream();
#if NETSTANDARD
			ct.ThrowIfCancellationRequested();
			await response.Content.CopyToAsync(buffer/*, ct*/);
			ct.ThrowIfCancellationRequested();
#else
			await response.Content.CopyToAsync(buffer, ct);
#endif
			buffer.Position = 0;
			using var reader = new StreamReader(buffer, Encoding.UTF8);
			ct.ThrowIfCancellationRequested();
			LoadCertificatesFromReader(reader, response.RequestMessage?.RequestUri?.AbsoluteUri ?? "[web request]");
		}

		/// <summary>
		/// Loads and verifies certificates from all PEM files in the given directory and its subdirectory.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="directoryPath">The path of the directory to enumerate for PEM files.</param>
		/// <param name="fileNamePattern">The name pattern to search for files. By default the pattern <c>*.pem</c> is used.</param>
		public void LoadCertificatesFromDirectory(string directoryPath, string fileNamePattern = "*.pem") {
			var files = Directory.EnumerateFiles(directoryPath, fileNamePattern, SearchOption.AllDirectories);
			foreach (var file in files) {
				using var fileReader = File.OpenText(file);
				LoadCertificatesFromReader(fileReader, file);
			}
		}

		/// <summary>
		/// Loads and verifies certificates from the given <see cref="TextReader"/> in PEM format.
		/// The certificates are validated using the <see cref="ICertificateValidator"/> given at construction.
		/// Only certificates that pass the validation checks are added to the store.
		/// </summary>
		/// <param name="reader">A reader containing the certificates in PEM text format.</param>
		/// <param name="sourceName">A name for the source behind <paramref name="reader"/> to use for log messages. This can, e.g. be a filename or an URL.</param>
		public void LoadCertificatesFromReader(TextReader reader, string sourceName) {
			var certs = loadCertificates(reader, sourceName);
			foreach (var cert in certs) {
				var keyid = cert.PublicKey.CalculateId();
				if (validator.CheckCertificate(cert)) {
					certificatesByKeyId[keyid] = cert;
					certificatesBySubjectDN[cert.SubjectDN] = cert;
					var skid = cert.SubjectKeyIdentifier;
					if (skid != null) {
						certificatesBySKID[skid] = cert;
					}
				}
				else {
					logger.LogWarning("The certificate with subject {subject} and key ID {keyid} from {source} failed validation. It will not be added to the certificate store.", cert.SubjectDN, keyid, sourceName);
				}
			}
		}

		private IEnumerable<Certificate> loadCertificates(TextReader reader, string sourceName) {
			PemReader pemReader = new PemReader(reader);
			int loadedCount = 0;
			for (; ; ) {
				Certificate? cert = null;
				try {
					cert = PemHelper.ReadCertificate(pemReader);
					if (cert == null) break;
					loadedCount++;
				}
				catch (PemException pe) {
					logger.LogWarning(pe, "Source {src} contained an object of type {type}, expecting X509Certificate objects, ignoring this object.", sourceName, pe.PemContentType?.FullName);
					continue;
				}
				yield return cert;
			}
			if (loadedCount == 0) {
				logger.LogWarning("Source {src} contained no valid certificates.", sourceName);
			}
		}
	}
}
