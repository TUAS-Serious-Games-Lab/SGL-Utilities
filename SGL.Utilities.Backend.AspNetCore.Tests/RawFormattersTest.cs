using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Backend.AspNetCore.Tests {

	public class RawFormattersTestFixture : WebApplicationFactory<RawFormattersTestStartup> {
		public ITestOutputHelper? Output { get; set; } = null;

		public string TestString { get; }
		public byte[] TestBytes { get; }

		public RawFormattersTestFixture() {
			TestString = StringGenerator.GenerateRandomString(10 * 1024 * 1024);
			var rnd = new Random();
			TestBytes = new byte[10 * 1024 * 1024];
			rnd.NextBytes(TestBytes);
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder) {
			base.ConfigureWebHost(builder);
			builder.ConfigureTestServices(services => {
				services.AddSingleton(this);
			});
		}

		protected override IHostBuilder CreateHostBuilder() {
			return Host.CreateDefaultBuilder().ConfigureWebHostDefaults(webBuilder => {
				webBuilder.UseStartup<RawFormattersTestStartup>();
			}).ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Information)); ;
		}
	}

	public class RawFormattersTestStartup {
		public void ConfigureServices(IServiceCollection services) {
			services.AddControllers(options => options.AddPlainTextInputFormatter().AddRawBytesFormatters());
		}
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			app.UseRouting();
			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}

	[Route("api/raw-formatters-test")]
	[ApiController]
	public class RawFormattersTestController : ControllerBase {

		private RawFormattersTestFixture fixture;

		public RawFormattersTestController(RawFormattersTestFixture fixture) {
			this.fixture = fixture;
		}

		[Consumes("text/plain")]
		[Produces("text/plain")]
		[HttpPost("echo/string")]
		public ActionResult<string> EchoString([FromBody] string body) {
			return Ok(body);
		}

		[Consumes("application/octet-stream")]
		[Produces("application/octet-stream")]
		[HttpPost("echo/bytes")]
		public ActionResult<byte[]> EchoBytes([FromBody] byte[] body) {
			return Ok(body);
		}
	}

	public class RawFormattersTest : IClassFixture<RawFormattersTestFixture> {
		private readonly RawFormattersTestFixture fixture;
		private readonly ITestOutputHelper output;

		public RawFormattersTest(RawFormattersTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
		}

		[Fact]
		public async Task PlainTextInputFormattingCorrectlyReadsBody() {
			using var client = fixture.CreateClient();
			var content = new StringContent(fixture.TestString);
			var request = new HttpRequestMessage(HttpMethod.Post, "api/raw-formatters-test/echo/string");
			request.Content = content;
			var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			response.EnsureSuccessStatusCode();
			var responseBody = await response.Content.ReadAsStringAsync();
			Assert.Equal(fixture.TestString, responseBody);
		}
		[Fact]
		public async Task RawBytesFormattersCorrectlyReadAndWriteBody() {
			using var client = fixture.CreateClient();
			var content = new ByteArrayContent(fixture.TestBytes);
			var request = new HttpRequestMessage(HttpMethod.Post, "api/raw-formatters-test/echo/bytes");
			request.Content = content;
			var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			response.EnsureSuccessStatusCode();
			var responseBody = await response.Content.ReadAsByteArrayAsync();
			Assert.Equal(fixture.TestBytes, responseBody);
		}
	}
}
