using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Utilities.Tests {
	public class MinmalJwtReaderTest {
		private JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

		[Fact]
		public void MinimalJwtReaderCanReadTokensFromJwtSecurityTokenHandler() {
			var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes("This is a (not) very s3cr3t signing key for testing!")),
				SecurityAlgorithms.HmacSha256);
			var uid = Guid.NewGuid();
			var issuer = "TestIssuer";
			var audience = "TestAudience";
			var expires = DateTime.UtcNow.AddMinutes(5);
			var claimObjs = new Claim[] { new Claim("test", "this is a test"), new Claim("userid", $"{uid:D}") };
			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claimObjs,
				expires: expires,
				signingCredentials: signingCredentials
			);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			var payload = MinimalJwtReader.ReadJwtPayload(tokenString);
			var expiresRounded = new DateTime(expires.Year, expires.Month, expires.Day, expires.Hour, expires.Minute, expires.Second);
			Assert.Equal(expiresRounded, MinimalJwtReader.ReadDateTimeValue(Assert.Contains(MinimalJwtReader.ExpirationTimeKey, payload)));
			Assert.Equal(issuer, Assert.Contains(MinimalJwtReader.IssuerKey, payload));
			Assert.Equal(audience, Assert.Contains(MinimalJwtReader.AudienceKey, payload));
			Assert.Equal("this is a test", Assert.Contains("test", payload));
			Assert.Equal(uid, Assert.IsType<Guid>(Assert.Contains("userid", payload)));
		}
	}
}
