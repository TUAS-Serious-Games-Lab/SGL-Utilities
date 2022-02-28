﻿using Microsoft.AspNetCore.Builder;
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {

	public class PemOutputFormatterIntegrationTestFixture : WebApplicationFactory<PemOutputFormatterIntegrationTestStartup> {
		public RandomGenerator Random { get; } = new RandomGenerator();
		public KeyPair KeyPair1 { get; }
		public KeyPair KeyPair2 { get; }
		public KeyPair KeyPair3 { get; }
		public Certificate Cert1 { get; }
		public Certificate Cert2 { get; }
		public ITestOutputHelper? Output { get; set; } = null;

		public PemOutputFormatterIntegrationTestFixture() {
			KeyPair1 = KeyPair.GenerateEllipticCurves(Random, 521);
			KeyPair2 = KeyPair.GenerateEllipticCurves(Random, 521);
			KeyPair3 = KeyPair.GenerateEllipticCurves(Random, 521);
			var signerDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "PEM Formatter Test Signer") });
			var subject1DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "PEM Formatter Test 1") });
			var subject2DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "PEM Formatter Test 2") });
			Cert1 = Certificate.Generate(signerDN, KeyPair3.Private, subject1DN, KeyPair1.Public, TimeSpan.FromHours(1), Random, 128);
			Cert2 = Certificate.Generate(signerDN, KeyPair3.Private, subject2DN, KeyPair2.Public, TimeSpan.FromHours(1), Random, 128);
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

	public class PemOutputFormatterIntegrationTest : IClassFixture<PemOutputFormatterIntegrationTestFixture> {
		private readonly PemOutputFormatterIntegrationTestFixture fixture;
		private readonly ITestOutputHelper output;

		public PemOutputFormatterIntegrationTest(PemOutputFormatterIntegrationTestFixture fixture, ITestOutputHelper output) {
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

		[Fact]
		public async Task SingleCertPemStringIsCorrectlyPassedThrough() {
			var response = Certificate.LoadOneFromPem(await GetPem("api/pem-formatter-test/cert/string/single"));
			Assert.Equal(fixture.Cert1, response);
		}
		[Fact]
		public async Task MultipleCertPemStringsAreCorrectlyPassedThrough() {
			var response = Certificate.LoadAllFromPem(await GetPem("api/pem-formatter-test/cert/string/multiple"));
			Assert.Equal(new[] { fixture.Cert1, fixture.Cert2 }, response);
		}
		[Fact]
		public async Task SingleKeyPemStringIsCorrectlyPassedThrough() {
			var response = PublicKey.LoadOneFromPem(await GetPem("api/pem-formatter-test/key/string/single"));
			Assert.Equal(fixture.KeyPair1.Public, response);
		}
		[Fact]
		public async Task MultipleKeyPemStringsAreCorrectlyPassedThrough() {
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
		public async Task SingleKeyPemObjectIsCorrectlyFormatted() {
			var response = PublicKey.LoadOneFromPem(await GetPem("api/pem-formatter-test/key/object/single"));
			Assert.Equal(fixture.KeyPair1.Public, response);
		}
		[Fact]
		public async Task MultipleKeyPemObjectsAreCorrectlyFormatted() {
			var response = PublicKey.LoadAllFromPem(await GetPem("api/pem-formatter-test/key/object/multiple"));
			Assert.Equal(new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public }, response);
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

		private PemOutputFormatterIntegrationTestFixture fixture;

		public TestController(PemOutputFormatterIntegrationTestFixture fixture) {
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
		[HttpGet("key/object/single")]
		public PublicKey GetSingleKeyObject() {
			return fixture.KeyPair1.Public;
		}

		[Produces("application/x-pem-file")]
		[HttpGet("key/object/multiple")]
		public IEnumerable<PublicKey> GetMultipleKeyObjects() {
			return new[] { fixture.KeyPair1.Public, fixture.KeyPair2.Public, fixture.KeyPair3.Public };
		}
	}
}