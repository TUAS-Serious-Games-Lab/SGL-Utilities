using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X9;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SGL.Utilities.Crypto.KeyDerivation;

namespace SGL.Utilities.Crypto.Signatures {
	internal class SignatureHelper {
		internal static string GetSignatureAlgorithmName(KeyType keyType, SignatureDigest digest) => keyType switch {
			KeyType.RSA => digest switch {
				SignatureDigest.Sha256 => PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id,
				SignatureDigest.Sha384 => PkcsObjectIdentifiers.Sha384WithRsaEncryption.Id,
				SignatureDigest.Sha512 => PkcsObjectIdentifiers.Sha512WithRsaEncryption.Id,
				_ => throw new CertificateException($"Unsupported digest {digest}")
			},
			KeyType.EllipticCurves => digest switch {
				SignatureDigest.Sha256 => X9ObjectIdentifiers.ECDsaWithSha256.Id,
				SignatureDigest.Sha384 => X9ObjectIdentifiers.ECDsaWithSha384.Id,
				SignatureDigest.Sha512 => X9ObjectIdentifiers.ECDsaWithSha512.Id,
				_ => throw new CertificateException($"Unsupported digest {digest}")
			},
			_ => throw new CertificateException($"Unsupported key type {keyType}")
		};
	}
}
