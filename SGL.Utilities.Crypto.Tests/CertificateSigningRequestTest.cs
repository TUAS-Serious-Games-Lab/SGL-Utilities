﻿using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {
	public class CertificateSigningRequestTest {
		public RandomGenerator Random { get; }
		public KeyPair SignerKeyPair { get; }
		public DistinguishedName SignerDN { get; }
		public Certificate SignerCertificate { get; }
		public KeyPair SubjectKeyPair { get; }
		public DistinguishedName SubjectDN { get; }
		public ITestOutputHelper Output { get; }

		public CertificateSigningRequestTest(ITestOutputHelper output) {
			Random = new RandomGenerator();
			SignerKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			SignerDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test Signer") });
			SignerCertificate = Certificate.Generate(SignerDN, SignerKeyPair.Private, SignerDN, SignerKeyPair.Public, TimeSpan.FromHours(1), Random, 128,
				authorityKeyIdentifier: new KeyIdentifier(SignerKeyPair.Public), generateSubjectKeyIdentifier: true, keyUsages: KeyUsages.KeyCertSign, caConstraint: (true, 0));
			SubjectKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			SubjectDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test 1") });
			Output = output;
		}

		private void DumpToOutput(object obj) {
			using var strWriter = new StringWriter();
			var writer = new PemObjectWriter(strWriter);
			writer.WriteObject(obj);
			Output.WriteLine(strWriter.ToString());
		}

		[Fact]
		public void BasicCsrCanBeGeneratedAndSignedIntoCertificateSuccessfully() {
			var csr = CertificateSigningRequest.Generate(SubjectDN, SubjectKeyPair);
			Output.WriteLine("CSR:");
			DumpToOutput(csr);
			var policy = new CsrSigningPolicy(Random);
			var cert = csr.GenerateCertificate(SignerCertificate, SignerKeyPair, policy);
			Output.WriteLine("Certificate:");
			DumpToOutput(cert);
			Assert.Equal(SignerDN, cert.IssuerDN);
			Assert.Equal(SubjectDN, cert.SubjectDN);
			Assert.Equal(SubjectKeyPair.Public, cert.PublicKey);
			Assert.Equal(CertificateCheckOutcome.Valid, cert.Verify(SignerCertificate.PublicKey));
		}
		[Fact]
		public void CsrWithExtensionsCanBeGeneratedAndSignedIntoCertificateSuccessfully() {
			var csr = CertificateSigningRequest.Generate(SubjectDN, SubjectKeyPair, requestSubjectKeyIdentifier: true, requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.KeyEncipherment, requestCABasicConstraints: (false, null));
			Output.WriteLine("CSR:");
			DumpToOutput(csr);
			var policy = new CsrSigningPolicy(Random);
			var cert = csr.GenerateCertificate(SignerCertificate, SignerKeyPair, policy);
			Output.WriteLine("Certificate:");
			DumpToOutput(cert);
			Assert.Equal(SignerDN, cert.IssuerDN);
			Assert.Equal(SubjectDN, cert.SubjectDN);
			Assert.Equal(SubjectKeyPair.Public, cert.PublicKey);
			Assert.True(cert.AuthorityIdentifier.HasValue);
			Assert.Equal(SignerCertificate.SubjectKeyIdentifier, cert.AuthorityIdentifier.Value.KeyIdentifier);
			Assert.Equal(CertificateCheckOutcome.Valid, cert.Verify(SignerCertificate.PublicKey));
		}
	}
}
