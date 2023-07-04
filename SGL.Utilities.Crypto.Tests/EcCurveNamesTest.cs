using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.Crypto.Tests {
	public class EcCurveNamesTest {
		private ITestOutputHelper output;

		public EcCurveNamesTest(ITestOutputHelper output) {
			this.output = output;
		}

		[Fact]
		public void SupportedCurveNamesContainDefaultsForKeyLengths() {
			var supportedCurveNames = KeyPair.GetSupportedNamedEllipticCurveNames();
			output.WriteLine("Supported curves: " + string.Join(", ", supportedCurveNames));
			Assert.Contains("secp521r1", supportedCurveNames);
			Assert.Contains("secp384r1", supportedCurveNames);
			Assert.Contains("prime256v1", supportedCurveNames);
			Assert.Contains("prime239v1", supportedCurveNames);
			Assert.Contains("secp224r1", supportedCurveNames);
			Assert.Contains("prime192v1", supportedCurveNames);
		}

		[Fact]
		public void SupportedCurvesContainDefaultsForKeyLengths() {
			var supportedCurves = KeyPair.GetSupportedNamedEllipticCurves().OrderByDescending(curve => curve.KeyLength).ToList();
			output.WriteLine("Supported curves: " + string.Join(", ", supportedCurves.Select(curve => $"{curve.Name}({curve.KeyLength})")));
			Assert.Equal(521, Assert.Single(supportedCurves, curve => curve.Name == "secp521r1").KeyLength);
			Assert.Equal(384, Assert.Single(supportedCurves, curve => curve.Name == "secp384r1").KeyLength);
			Assert.Equal(256, Assert.Single(supportedCurves, curve => curve.Name == "prime256v1").KeyLength);
			Assert.Equal(239, Assert.Single(supportedCurves, curve => curve.Name == "prime239v1").KeyLength);
			Assert.Equal(224, Assert.Single(supportedCurves, curve => curve.Name == "secp224r1").KeyLength);
			Assert.Equal(192, Assert.Single(supportedCurves, curve => curve.Name == "prime192v1").KeyLength);
		}
	}
}
