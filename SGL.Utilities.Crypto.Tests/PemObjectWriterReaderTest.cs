using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {
	public class PemObjectWriterReaderTestFixture {
		public RandomGenerator Random { get; }
		public KeyPair RsaKeyPair { get; }
		public KeyPair EcKeyPair { get; }
		public DistinguishedName Subj1DN { get; }
		public DistinguishedName Subj2DN { get; }
		public Certificate Certificate1 { get; }
		public Certificate Certificate2 { get; }
		public Func<char[]> PasswordGetter { get; }

		public PemObjectWriterReaderTestFixture() {
			Random = new RandomGenerator();
			RsaKeyPair = KeyPair.GenerateRSA(Random, 4096);
			EcKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			Subj1DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 1") });
			Subj2DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 2") });
			Certificate1 = Certificate.Generate(Subj1DN, RsaKeyPair.Private, Subj2DN, EcKeyPair.Public, TimeSpan.FromMinutes(5), Random, 128, signatureDigest: CertificateSignatureDigest.Sha256, generateSubjectKeyIdentifier: true, keyUsages: KeyUsages.KeyEncipherment);
			Certificate2 = Certificate.Generate(Subj2DN, EcKeyPair.Private, Subj1DN, RsaKeyPair.Public, TimeSpan.FromMinutes(5), Random, 128, signatureDigest: CertificateSignatureDigest.Sha384, generateSubjectKeyIdentifier: true, keyUsages: KeyUsages.KeyEncipherment);
			PasswordGetter = () => "ThisI5AT3st".ToCharArray();
		}
	}

	public class PemObjectWriterReaderTest : IClassFixture<PemObjectWriterReaderTestFixture> {
		private readonly PemObjectWriterReaderTestFixture fixture;
		private readonly ITestOutputHelper output;

		public PemObjectWriterReaderTest(PemObjectWriterReaderTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
		}

		[Fact]
		public void PublicKeyWrittenByPemObjectWriterIsCorrectlyReadBackUsingPemObjectReader() {
			using StringWriter writeBuffer = new StringWriter();
			PemObjectWriter writer = new PemObjectWriter(writeBuffer);
			writer.WriteObject(fixture.RsaKeyPair.Public);
			writer.WriteObject(fixture.EcKeyPair.Public);
			output.WriteLine(writeBuffer.ToString());
			using StringReader readBuffer = new StringReader(writeBuffer.ToString());
			PemObjectReader reader = new PemObjectReader(readBuffer);
			Assert.Equal(fixture.RsaKeyPair.Public, Assert.IsType<PublicKey>(reader.ReadNextObject()));
			Assert.Equal(fixture.EcKeyPair.Public, Assert.IsType<PublicKey>(reader.ReadNextObject()));
		}
		[Fact]
		public void PrivateKeyWrittenByPemObjectWriterIsCorrectlyReadBackAsCorrespondingKeyPairUsingPemObjectReader() {
			using StringWriter writeBuffer = new StringWriter();
			PemObjectWriter writer = new PemObjectWriter(writeBuffer, PemEncryptionMode.AES_256_CBC, fixture.PasswordGetter, fixture.Random);
			writer.WriteObject(fixture.RsaKeyPair.Private);
			writer.WriteObject(fixture.EcKeyPair.Private);
			output.WriteLine(writeBuffer.ToString());
			using StringReader readBuffer = new StringReader(writeBuffer.ToString());
			PemObjectReader reader = new PemObjectReader(readBuffer, fixture.PasswordGetter);
			Assert.Equal(fixture.RsaKeyPair, Assert.IsType<KeyPair>(reader.ReadNextObject()));
			Assert.Equal(fixture.EcKeyPair, Assert.IsType<KeyPair>(reader.ReadNextObject()));
		}
		[Fact]
		public void KeyPairWrittenByPemObjectWriterIsCorrectlyReadBackUsingPemObjectReader() {
			using StringWriter writeBuffer = new StringWriter();
			PemObjectWriter writer = new PemObjectWriter(writeBuffer, PemEncryptionMode.AES_256_CBC, fixture.PasswordGetter, fixture.Random);
			writer.WriteObject(fixture.RsaKeyPair);
			writer.WriteObject(fixture.EcKeyPair);
			output.WriteLine(writeBuffer.ToString());
			using StringReader readBuffer = new StringReader(writeBuffer.ToString());
			PemObjectReader reader = new PemObjectReader(readBuffer, fixture.PasswordGetter);
			Assert.Equal(fixture.RsaKeyPair, Assert.IsType<KeyPair>(reader.ReadNextObject()));
			Assert.Equal(fixture.EcKeyPair, Assert.IsType<KeyPair>(reader.ReadNextObject()));
		}
		[Fact]
		public void CertificateWrittenByPemObjectWriterIsCorrectlyReadBackUsingPemObjectReader() {
			using StringWriter writeBuffer = new StringWriter();
			PemObjectWriter writer = new PemObjectWriter(writeBuffer);
			writer.WriteObject(fixture.Certificate1);
			writer.WriteObject(fixture.Certificate2);
			output.WriteLine(writeBuffer.ToString());
			using StringReader readBuffer = new StringReader(writeBuffer.ToString());
			PemObjectReader reader = new PemObjectReader(readBuffer);
			Assert.Equal(fixture.Certificate1, Assert.IsType<Certificate>(reader.ReadNextObject()));
			Assert.Equal(fixture.Certificate2, Assert.IsType<Certificate>(reader.ReadNextObject()));
		}
		[Fact]
		public void HeterogenouslyTypedSequenceOfObjectsCanBeWrittenWithPemObjectWriterAndReadBackUsingPemObjectReader() {
			using StringWriter writeBuffer = new StringWriter();
			PemObjectWriter writer = new PemObjectWriter(writeBuffer, PemEncryptionMode.AES_256_CBC, fixture.PasswordGetter, fixture.Random);
			var origSequence = new object[] { fixture.Certificate1, fixture.EcKeyPair, fixture.RsaKeyPair.Public, fixture.Certificate2, fixture.RsaKeyPair, fixture.EcKeyPair.Public };
			writer.WriteAllObjects(origSequence);
			output.WriteLine(writeBuffer.ToString());
			using StringReader readBuffer = new StringReader(writeBuffer.ToString());
			PemObjectReader reader = new PemObjectReader(readBuffer, fixture.PasswordGetter);
			var readSequence = reader.ReadAllObjects().ToList();
			Assert.Equal(origSequence, readSequence);
		}
		[Fact]
		public void CommentsCanBeWrittenAndAreIgnoredWhenReading() {
			using StringWriter writeBuffer = new StringWriter();
			PemObjectWriter writer = new PemObjectWriter(writeBuffer, PemEncryptionMode.AES_256_CBC, fixture.PasswordGetter, fixture.Random);
			var origSequence = new object[] { "Comment 1", fixture.Certificate1, "Comment 2", fixture.EcKeyPair, "Comment 3", fixture.Certificate2, "Comment 4", fixture.EcKeyPair.Public, "Comment 5" };
			writer.WriteAllObjects(origSequence);
			output.WriteLine(writeBuffer.ToString());
			Assert.Contains("Comment 1", writeBuffer.ToString());
			Assert.Contains("Comment 2", writeBuffer.ToString());
			Assert.Contains("Comment 3", writeBuffer.ToString());
			Assert.Contains("Comment 4", writeBuffer.ToString());
			Assert.Contains("Comment 5", writeBuffer.ToString());
			using StringReader readBuffer = new StringReader(writeBuffer.ToString());
			PemObjectReader reader = new PemObjectReader(readBuffer, fixture.PasswordGetter);
			var expectedSequence = new object[] { fixture.Certificate1, fixture.EcKeyPair, fixture.Certificate2, fixture.EcKeyPair.Public };
			var readSequence = reader.ReadAllObjects().ToList();
			Assert.Equal(expectedSequence, readSequence);
		}
	}
}
