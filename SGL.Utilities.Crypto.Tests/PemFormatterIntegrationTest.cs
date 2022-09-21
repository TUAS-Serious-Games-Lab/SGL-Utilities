using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Utilities.Crypto.AspNetCore;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {

	public class PemFormatterIntegrationTestFixture : WebApplicationFactory<PemOutputFormatterIntegrationTestStartup> {
		public RandomGenerator Random { get; } = new RandomGenerator();
		public KeyPair KeyPair1 { get; }
		public KeyPair KeyPair2 { get; }
		public KeyPair KeyPair3 { get; }
		public Certificate Cert1 { get; }
		public Certificate Cert2 { get; }
		public CertificateSigningRequest Csr1 { get; }
		public CertificateSigningRequest Csr2 { get; }
		public ITestOutputHelper? Output { get; set; } = null;

		public PemFormatterIntegrationTestFixture() {
			KeyPair1 = KeyPair.GenerateEllipticCurves(Random, 521);
			KeyPair2 = KeyPair.GenerateEllipticCurves(Random, 521);
			KeyPair3 = KeyPair.GenerateEllipticCurves(Random, 521);
			var signerDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "PEM Formatter Test Signer") });
			var subject1DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "PEM Formatter Test 1") });
			var subject2DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "PEM Formatter Test 2") });
			Cert1 = Certificate.Generate(signerDN, KeyPair3.Private, subject1DN, KeyPair1.Public, TimeSpan.FromHours(1), Random, 128);
			Cert2 = Certificate.Generate(signerDN, KeyPair3.Private, subject2DN, KeyPair2.Public, TimeSpan.FromHours(1), Random, 128);
			Csr1 = CertificateSigningRequest.Generate(signerDN, KeyPair3, requestSubjectKeyIdentifier: false, requestAuthorityKeyIdentifier: false, requestKeyUsages: KeyUsages.DigitalSignature);
			Csr2 = CertificateSigningRequest.Generate(signerDN, KeyPair3, requestSubjectKeyIdentifier: true, requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.KeyEncipherment);
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder) {
			base.ConfigureWebHost(builder);
			builder.ConfigureTestServices(services => {
				services.AddSingleton(this);
			});
		}

		protected override IHostBuilder CreateHostBuilder() {
			return Host.CreateDefaultBuilder().ConfigureWebHostDefaults(webBuilder => {
				webBuilder.UseStartup<PemOutputFormatterIntegrationTestStartup>();
			}).ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Information)); ;
		}
	}

	public class PemFormatterIntegrationTest : IClassFixture<PemFormatterIntegrationTestFixture> {
		private readonly PemFormatterIntegrationTestFixture fixture;
		private readonly ITestOutputHelper output;

		public PemFormatterIntegrationTest(PemFormatterIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
		}

		public async Task<TextReader> GetPem(string path) {
			using var client = fixture.CreateClient();
			var request = new HttpRequestMessage(HttpMethod.Get, path);
			request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/x-pem-file"));
			var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			response.EnsureSuccessStatusCode();
			return new StreamReader(await response.Content.ReadAsStreamAsync());
		}

		public async Task<HttpStatusCode> PostPem(string path, MemoryStream body) {
			body.Position = 0;
			using var client = fixture.CreateClient();
			var request = new HttpRequestMessage(HttpMethod.Post, path);
			var content = new StreamContent(body);
			content.Headers.ContentType = MediaTypeWithQualityHeaderValue.Parse("application/x-pem-file");
			request.Content = content;
			var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			response.EnsureSuccessStatusCode();
			return response.StatusCode;
		}

		[Fact]
		public async Task SingleCertPemStringIsCorrectlyPassedThroughGet() {
			var response = Certificate.LoadOneFromPem(await GetPem("api/pem-formatter-test/cert/string/single"));
			Assert.Equal(fixture.Cert1, response);
		}
		[Fact]
		public async Task MultipleCertPemStringsAreCorrectlyPassedThroughGet() {
			var response = Certificate.LoadAllFromPem(await GetPem("api/pem-formatter-test/cert/string/multiple"));
			Assert.Equal(new[] { fixture.Cert1, fixture.Cert2 }, response);
		}
		[Fact]
		public async Task SingleCsrPemStringIsCorrectlyPassedThroughGet() {
			var response = CertificateSigningRequest.LoadOneFromPem(await GetPem("api/pem-formatter-test/csr/string/single"));
			Assert.Equal(fixture.Csr1, response);
		}
		[Fact]
		public async Task MultipleCsrPemStringsAreCorrectlyPassedThroughGet() {
			var response = CertificateSigningRequest.LoadAllFromPem(await GetPem("api/pem-formatter-test/csr/string/multiple"));
			Assert.Equal(new[] { fixture.Csr1, fixture.Csr2 }, response);
		}
		[Fact]
		public async Task SingleKeyPemStringIsCorrectlyPassedThroughGet() {
			var response = PublicKey.LoadOneFromPem(await GetPem("api/pem-formatter-test/key/string/single"));
			Assert.Equal(fixture.KeyPair1.Public, response);
		}
		[Fact]
		public async Task MultipleKeyPemStringsAreCorrectlyPassedThroughGet() {
			var response = PublicKey.LoadAllFromPem(await GetPem("api/pem-formatter-test/key/string/multiple"));
			Assert.Equal(new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public }, response);
		}
		[Fact]
		public async Task SingleCertPemObjectIsCorrectlyFormatted() {
			var response = Certificate.LoadOneFromPem(await GetPem("api/pem-formatter-test/cert/object/single"));
			Assert.Equal(fixture.Cert1, response);
		}
		[Fact]
		public async Task MultipleCertPemObjectsAreCorrectlyFormatted() {
			var response = Certificate.LoadAllFromPem(await GetPem("api/pem-formatter-test/cert/object/multiple"));
			Assert.Equal(new[] { fixture.Cert1, fixture.Cert2 }, response);
		}
		[Fact]
		public async Task SingleCsrPemObjectIsCorrectlyFormatted() {
			var response = CertificateSigningRequest.LoadOneFromPem(await GetPem("api/pem-formatter-test/csr/object/single"));
			Assert.Equal(fixture.Csr1, response);
		}
		[Fact]
		public async Task MultipleCsrPemObjectsAreCorrectlyFormatted() {
			var response = CertificateSigningRequest.LoadAllFromPem(await GetPem("api/pem-formatter-test/csr/object/multiple"));
			Assert.Equal(new[] { fixture.Csr1, fixture.Csr2 }, response);
		}
		[Fact]
		public async Task SingleKeyPemObjectIsCorrectlyFormatted() {
			var response = PublicKey.LoadOneFromPem(await GetPem("api/pem-formatter-test/key/object/single"));
			Assert.Equal(fixture.KeyPair1.Public, response);
		}
		[Fact]
		public async Task MultipleKeyPemObjectsAreCorrectlyFormatted() {
			var response = PublicKey.LoadAllFromPem(await GetPem("api/pem-formatter-test/key/object/multiple"));
			Assert.Equal(new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public }, response);
		}
		[Fact]
		public async Task MultipleMixedPemObjectsAreCorrectlyFormatted() {
			var response = new PemObjectReader(await GetPem("api/pem-formatter-test/mixed/object/multiple")).ReadAllObjects();
			Assert.Equal(new object[] { fixture.KeyPair1.Public, fixture.Cert1, fixture.Csr1, fixture.KeyPair2.Public, fixture.Cert2, fixture.Csr2, fixture.KeyPair3.Public }, response);
		}

		[Fact]
		public async Task SingleCertPemStringIsCorrectlyPassedThroughPost() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Cert1.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/cert/string/single", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleCertPemStringsAreCorrectlyPassedThroughPost() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Cert1.StoreToPem(writer);
				fixture.Cert2.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/cert/string/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task SingleCsrPemStringIsCorrectlyPassedThroughPost() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Csr1.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/csr/string/single", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleCsrPemStringsAreCorrectlyPassedThroughPost() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Csr1.StoreToPem(writer);
				fixture.Csr2.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/csr/string/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task SingleKeyPemStringIsCorrectlyPassedThroughPost() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.KeyPair1.Public.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/key/string/single", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleKeyPemStringsAreCorrectlyPassedThroughPost() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.KeyPair1.Public.StoreToPem(writer);
				fixture.KeyPair2.Public.StoreToPem(writer);
				fixture.KeyPair3.Public.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/key/string/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task SingleCertPemObjectIsCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Cert1.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/cert/object/single", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleCertPemObjectsAreCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Cert1.StoreToPem(writer);
				fixture.Cert2.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/cert/object/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task SingleCsrPemObjectIsCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Csr1.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/csr/object/single", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleCsrPemObjectsAreCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.Csr1.StoreToPem(writer);
				fixture.Csr2.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/csr/object/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task SingleKeyPemObjectIsCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.KeyPair1.Public.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/key/object/single", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleKeyPemObjectsAreCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.KeyPair1.Public.StoreToPem(writer);
				fixture.KeyPair2.Public.StoreToPem(writer);
				fixture.KeyPair3.Public.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/key/object/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
		[Fact]
		public async Task MultipleMixedPemObjectsAreCorrectlyParsed() {
			using var body = new MemoryStream();
			using (var writer = new StreamWriter(body, Encoding.UTF8, leaveOpen: true)) {
				fixture.KeyPair1.Public.StoreToPem(writer);
				fixture.Cert1.StoreToPem(writer);
				fixture.Csr1.StoreToPem(writer);
				fixture.KeyPair2.Public.StoreToPem(writer);
				fixture.Cert2.StoreToPem(writer);
				fixture.Csr2.StoreToPem(writer);
				fixture.KeyPair3.Public.StoreToPem(writer);
			}
			var statusCode = await PostPem("api/pem-formatter-test/mixed/object/multiple", body);
			Assert.Equal(HttpStatusCode.OK, statusCode);
		}
	}

	public class PemOutputFormatterIntegrationTestStartup {
		public void ConfigureServices(IServiceCollection services) {
			services.AddControllers(options => options.AddPemFormatters());
		}
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			app.UseRouting();
			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}

	[Route("api/pem-formatter-test")]
	[ApiController]
	public class TestController : ControllerBase {

		private PemFormatterIntegrationTestFixture fixture;

		public TestController(PemFormatterIntegrationTestFixture fixture) {
			this.fixture = fixture;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("cert/string/single")]
		public string GetSingleCertString() {
			using var strWriter = new StringWriter();
			fixture.Cert1.StoreToPem(strWriter);
			return strWriter.ToString();
		}

		[Produces("application/x-pem-file")]
		[HttpGet("cert/string/multiple")]
		public IEnumerable<string> GetMultipleCertStrings() {
			var certStrs = new[] { fixture.Cert1, fixture.Cert2 }.Select(cert => {
				using var strWriter = new StringWriter();
				cert.StoreToPem(strWriter);
				return strWriter.ToString();
			}).ToList();

			return certStrs;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("csr/string/single")]
		public string GetSingleCsrString() {
			using var strWriter = new StringWriter();
			fixture.Csr1.StoreToPem(strWriter);
			return strWriter.ToString();
		}

		[Produces("application/x-pem-file")]
		[HttpGet("csr/string/multiple")]
		public IEnumerable<string> GetMultipleCsrStrings() {
			var certStrs = new[] { fixture.Csr1, fixture.Csr2 }.Select(csr => {
				using var strWriter = new StringWriter();
				csr.StoreToPem(strWriter);
				return strWriter.ToString();
			}).ToList();

			return certStrs;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("key/string/single")]
		public string GetSingleKeyString() {
			using var strWriter = new StringWriter();
			fixture.KeyPair1.Public.StoreToPem(strWriter);
			return strWriter.ToString();
		}

		[Produces("application/x-pem-file")]
		[HttpGet("key/string/multiple")]
		public IEnumerable<string> GetMultipleKeyStrings() {
			var keyStrs = new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public }.Select(key => {
				using var strWriter = new StringWriter();
				key.StoreToPem(strWriter);
				return strWriter.ToString();
			}).ToList();

			return keyStrs;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("cert/object/single")]
		public Certificate GetSingleCertObject() {
			return fixture.Cert1;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("cert/object/multiple")]
		public IEnumerable<Certificate> GetMultipleCertObjects() {
			return new[] { fixture.Cert1, fixture.Cert2 };
		}

		[Produces("application/x-pem-file")]
		[HttpGet("csr/object/single")]
		public CertificateSigningRequest GetSingleCsrObject() {
			return fixture.Csr1;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("csr/object/multiple")]
		public IEnumerable<CertificateSigningRequest> GetMultipleCsrObjects() {
			return new[] { fixture.Csr1, fixture.Csr2 };
		}

		[Produces("application/x-pem-file")]
		[HttpGet("key/object/single")]
		public PublicKey GetSingleKeyObject() {
			return fixture.KeyPair1.Public;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("key/object/multiple")]
		public IEnumerable<PublicKey> GetMultipleKeyObjects() {
			return new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public };
		}

		[Produces("application/x-pem-file")]
		[HttpGet("mixed/object/multiple")]
		public IEnumerable<object> GetMultipleMixedObjects() {
			return new object[] { fixture.KeyPair1.Public, fixture.Cert1, fixture.Csr1, fixture.KeyPair2.Public, fixture.Cert2, fixture.Csr2, fixture.KeyPair3.Public };
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("cert/string/single")]
		public ActionResult PostSingleCertString([FromBody] string pem) {
			using var reader = new StringReader(pem);
			var cert = Certificate.LoadOneFromPem(reader);
			if (!cert.Equals(fixture.Cert1)) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("cert/string/multiple")]
		public ActionResult PostMultipleCertStrings([FromBody] IEnumerable<string> pems) {
			var certs = pems.Select(pem => {
				using var reader = new StringReader(pem);
				return Certificate.LoadOneFromPem(reader);
			}).ToList();
			if (!certs.SequenceEqual(new[] { fixture.Cert1, fixture.Cert2 })) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("csr/string/single")]
		public ActionResult PostSingleCsrString([FromBody] string pem) {
			using var reader = new StringReader(pem);
			var cert = CertificateSigningRequest.LoadOneFromPem(reader);
			if (!cert.Equals(fixture.Csr1)) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("csr/string/multiple")]
		public ActionResult PostMultipleCsrStrings([FromBody] IEnumerable<string> pems) {
			var csrs = pems.Select(pem => {
				using var reader = new StringReader(pem);
				return CertificateSigningRequest.LoadOneFromPem(reader);
			}).ToList();
			if (!csrs.SequenceEqual(new[] { fixture.Csr1, fixture.Csr2 })) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("key/string/single")]
		public ActionResult PostSingleKeyString([FromBody] string pem) {
			using var reader = new StringReader(pem);
			var key = PublicKey.LoadOneFromPem(reader);
			if (!key.Equals(fixture.KeyPair1.Public)) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("key/string/multiple")]
		public ActionResult PostMultipleKeyStrings([FromBody] IEnumerable<string> pems) {
			var keys = pems.Select(pem => {
				using var reader = new StringReader(pem);
				return PublicKey.LoadOneFromPem(reader);
			}).ToList();
			if (!keys.SequenceEqual(new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public })) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("cert/object/single")]
		public ActionResult PostSingleCertObject([FromBody] Certificate cert) {
			if (!cert.Equals(fixture.Cert1)) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("cert/object/multiple")]
		public ActionResult PostMultipleCertObjects([FromBody] IEnumerable<Certificate> certs) {
			if (!certs.SequenceEqual(new[] { fixture.Cert1, fixture.Cert2 })) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("csr/object/single")]
		public ActionResult PostSingleCsrObject([FromBody] CertificateSigningRequest csr) {
			if (!csr.Equals(fixture.Csr1)) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("csr/object/multiple")]
		public ActionResult PostMultipleCsrObjects([FromBody] IEnumerable<CertificateSigningRequest> csrs) {
			if (!csrs.SequenceEqual(new[] { fixture.Csr1, fixture.Csr2 })) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("key/object/single")]
		public ActionResult PostSingleKeyObject([FromBody] PublicKey key) {
			if (!key.Equals(fixture.KeyPair1.Public)) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("key/object/multiple")]
		public ActionResult PostMultipleKeyObjects([FromBody] IEnumerable<PublicKey> keys) {
			if (!keys.SequenceEqual(new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public })) {
				return Conflict();
			}
			return Ok();
		}

		[Consumes("application/x-pem-file")]
		[HttpPost("mixed/object/multiple")]
		public ActionResult PostMultipleMixedObjects([FromBody] IEnumerable<object> keys) {
			if (!keys.SequenceEqual(new object[] { fixture.KeyPair1.Public, fixture.Cert1, fixture.Csr1, fixture.KeyPair2.Public, fixture.Cert2, fixture.Csr2, fixture.KeyPair3.Public })) {
				return Conflict();
			}
			return Ok();
		}
	}
}
