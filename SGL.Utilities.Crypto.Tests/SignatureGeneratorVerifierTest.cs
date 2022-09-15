using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {
	public class SignatureGeneratorVerifierTestFixture {
		public RandomGenerator Random { get; } = new RandomGenerator();
		public KeyPair RsaKeyPair { get; }
		public KeyPair EcKeyPair { get; }

		public SignatureGeneratorVerifierTestFixture() {
			RsaKeyPair = KeyPair.GenerateRSA(Random, 2048);
			EcKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
		}
	}

	public class SignatureGeneratorVerifierTest : IClassFixture<SignatureGeneratorVerifierTestFixture> {
		private readonly ITestOutputHelper output;
		private readonly SignatureGeneratorVerifierTestFixture fixture;
		private readonly byte[] content;

		public SignatureGeneratorVerifierTest(ITestOutputHelper output, SignatureGeneratorVerifierTestFixture fixture) {
			this.output = output;
			this.fixture = fixture;
			content = fixture.Random.GetBytes(128 * 1024);
		}

		public static IEnumerable<object[]> KeyDigestCombinations {
			get {
				var rsa = nameof(SignatureGeneratorVerifierTestFixture.RsaKeyPair);
				var ec = nameof(SignatureGeneratorVerifierTestFixture.EcKeyPair);
				yield return new object[] { rsa, SignatureDigest.Sha256 };
				yield return new object[] { rsa, SignatureDigest.Sha384 };
				yield return new object[] { rsa, SignatureDigest.Sha512 };
				yield return new object[] { ec, SignatureDigest.Sha256 };
				yield return new object[] { ec, SignatureDigest.Sha384 };
				yield return new object[] { ec, SignatureDigest.Sha512 };
			}
		}
		private KeyPair getKeyPairByName(string keyPairName) {
			return typeof(SignatureGeneratorVerifierTestFixture).GetProperty(keyPairName)?.GetValue(fixture) as KeyPair ?? throw new ArgumentNullException();
		}

		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public void SignatureCanBeGeneratedAndVerifiedCorrectly(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			generator.ProcessBytes(content);
			var signature = generator.Sign();
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			verifier.ProcessBytes(content);
			Assert.True(verifier.IsValidSignature(signature));
		}
		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public void SignatureVerificationOnCorruptedDataFails(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			generator.ProcessBytes(content);
			var signature = generator.Sign();
			content[1337] ^= (1 << 4); // Flip a single bit
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			verifier.ProcessBytes(content);
			Assert.False(verifier.IsValidSignature(signature));
		}
		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public void CorruptSignatureFailsVerification(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			generator.ProcessBytes(content);
			var signature = generator.Sign();
			signature[42] ^= (1 << 3); // Flip a single bit
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			verifier.ProcessBytes(content);
			Assert.False(verifier.IsValidSignature(signature));
		}

		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public void SignatureCanBeGeneratedAndCheckedCorrectly(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			generator.ProcessBytes(content);
			var signature = generator.Sign();
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			verifier.ProcessBytes(content);
			verifier.CheckSignature(signature);
		}

		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public void SignatureCheckOnCorruptedDataFails(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			generator.ProcessBytes(content);
			var signature = generator.Sign();
			content[1337] ^= (1 << 4); // Flip a single bit
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			verifier.ProcessBytes(content);
			Assert.Throws<SignatureException>(() => verifier.CheckSignature(signature));
		}
		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public void CorruptSignatureFailsCheck(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			generator.ProcessBytes(content);
			var signature = generator.Sign();
			signature[42] ^= (1 << 3); // Flip a single bit
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			verifier.ProcessBytes(content);
			Assert.Throws<SignatureException>(() => verifier.CheckSignature(signature));
		}

		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public async Task SignatureOverStreamCanBeGeneratedAndVerifiedCorrectly(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			using var stream = new MemoryStream(content, writable: false);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			await generator.ConsumeBytesAsync(stream);
			var signature = generator.Sign();
			stream.Position = 0;
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			await verifier.ConsumeBytesAsync(stream);
			Assert.True(verifier.IsValidSignature(signature));
		}
		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public async Task SignatureOverStreamVerificationOnCorruptedDataFails(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			using var stream = new MemoryStream(content);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			await generator.ConsumeBytesAsync(stream);
			var signature = generator.Sign();
			stream.Position = 1337;
			stream.WriteByte((byte)(content[1337] ^ (1 << 4)));
			stream.Position = 0;
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			await verifier.ConsumeBytesAsync(stream);
			Assert.False(verifier.IsValidSignature(signature));
		}
		[Theory]
		[MemberData(nameof(KeyDigestCombinations))]
		public async Task CorruptSignatureOverStreamFailsVerification(string keyPairName, SignatureDigest digest) {
			var keyPair = getKeyPairByName(keyPairName);
			using var stream = new MemoryStream(content);
			var generator = new SignatureGenerator(keyPair.Private, digest, fixture.Random);
			await generator.ConsumeBytesAsync(stream);
			var signature = generator.Sign();
			signature[42] ^= (1 << 3); // Flip a single bit
			stream.Position = 0;
			output.WriteLine(Convert.ToBase64String(signature, Base64FormattingOptions.InsertLineBreaks));
			var verifier = new SignatureVerifier(keyPair.Public, digest);
			await verifier.ConsumeBytesAsync(stream);
			Assert.False(verifier.IsValidSignature(signature));
		}

	}
}
