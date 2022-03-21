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
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Backend.AspNetCore.Tests {
	public class MultipartSectionModelBinderTestFixture : WebApplicationFactory<MultipartSectionModelBinderTestStartup> {
		public ITestOutputHelper? Output { get; set; } = null;
		public TestDto1 SimpleBody { get; }
		public TestDto2 ComplexBody { get; }

		public MultipartSectionModelBinderTestFixture() {
			SimpleBody = new TestDto1 { Number = 12345, Text = "Hello World! This is a test", Date = DateTime.Now };
			ComplexBody = new TestDto2 {
				Strings = new[] { "Hello", "World", "!", "This", "is", "a", "test" },
				Mapping = new Dictionary<string, int> {
					["x"] = 123,
					["y"] = 234,
					["x"] = 345,
					["answer"] = 42
				}
			};
		}

		public void AssertExpected(TestDto1 simple) {
			Assert.Equal(SimpleBody.Number, simple.Number);
			Assert.Equal(SimpleBody.Text, simple.Text);
			Assert.Equal(SimpleBody.Date, simple.Date);
		}
		public void AssertExpected(TestDto2 complex) {
			Assert.Equal(ComplexBody.Strings, complex.Strings);
			Assert.All(ComplexBody.Mapping, (kvp) => Assert.Equal(kvp.Value, Assert.Contains(kvp.Key, (IDictionary<string, int>)complex.Mapping)));
			Assert.All(complex.Mapping, (kvp) => Assert.Equal(kvp.Value, Assert.Contains(kvp.Key, (IDictionary<string, int>)ComplexBody.Mapping)));
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder) {
			base.ConfigureWebHost(builder);
			builder.ConfigureTestServices(services => {
				services.AddSingleton(this);
			});
		}

		protected override IHostBuilder CreateHostBuilder() {
			return Host.CreateDefaultBuilder().ConfigureWebHostDefaults(webBuilder => {
				webBuilder.UseStartup<MultipartSectionModelBinderTestStartup>();
			}).ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Information)); ;
		}
	}

	public class MultipartSectionModelBinderTestStartup {
		public void ConfigureServices(IServiceCollection services) {
			services.AddControllers(options => { });
		}
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			app.UseRouting();
			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}
	}

	public class TestDto1 {
		public int Number { get; set; }
		public string Text { get; set; }
		public DateTime Date { get; set; }
	}

	public class TestDto2 {
		public IEnumerable<string> Strings { get; set; }
		public Dictionary<string, int> Mapping { get; set; }
	}

	[Route("api/multipart-section-model-binder-test")]
	[ApiController]
	public class TestController : ControllerBase {

		private MultipartSectionModelBinderTestFixture fixture;

		public TestController(MultipartSectionModelBinderTestFixture fixture) {
			this.fixture = fixture;
		}

		[DisableFormValueModelBinding]
		[HttpPost("two-json/no-name-override")]
		public ActionResult PostTwoJsonBodiesWithoutNameOverride([FromMultipartSection] TestDto1 simple, [FromMultipartSection] TestDto2 complex) {
			fixture.AssertExpected(simple);
			fixture.AssertExpected(complex);
			return Ok();
		}
	}

	public class MultipartSectionModelBinderTest : IClassFixture<MultipartSectionModelBinderTestFixture> {
		private readonly MultipartSectionModelBinderTestFixture fixture;
		private readonly ITestOutputHelper output;

		public MultipartSectionModelBinderTest(MultipartSectionModelBinderTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
		}

		[Fact]
		public async Task TwoJsonBodiesWithoutNameOverrideAreBoundCorrectly() {
			using var client = fixture.CreateClient();
			var request = new HttpRequestMessage(HttpMethod.Post, "api/multipart-section-model-binder-test/two-json/no-name-override");
			var contentPart1 = JsonContent.Create(fixture.SimpleBody);
			var contentPart2 = JsonContent.Create(fixture.ComplexBody);
			var content = new MultipartFormDataContent();
			content.Add(contentPart1, "simple");
			content.Add(contentPart2, "complex");
			request.Content = content;
			var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			output.WriteStreamContents(await response.Content.ReadAsStreamAsync());
			response.EnsureSuccessStatusCode();
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		}
	}
}
