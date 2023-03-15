using Org.BouncyCastle.Asn1.X509;

namespace SGL.Utilities.Crypto.Certificates {
	internal static class CertificateHelpers {

		internal static void MapBasicKeyUsageFlags(ref KeyUsages usages, int keyUsageBitFlags) {
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.DigitalSignature, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.NonRepudiation, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.KeyEncipherment, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.DataEncipherment, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.KeyAgreement, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.KeyCertSign, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.CrlSign, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.EncipherOnly, ref usages);
			SetBitIfUsagePresent(keyUsageBitFlags, KeyUsages.DecipherOnly, ref usages);
		}

		internal static void MapExtendedKeyUsageFlags(ref KeyUsages usages, ExtendedKeyUsage extKeyUsageExtension) {
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_serverAuth, KeyUsages.ExtServerAuth, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_clientAuth, KeyUsages.ExtClientAuth, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_codeSigning, KeyUsages.ExtCodeSigning, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_emailProtection, KeyUsages.ExtEmailProtection, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_ipsecEndSystem, KeyUsages.ExtIpsecEndSystem, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_ipsecTunnel, KeyUsages.ExtIpsecTunnel, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_ipsecUser, KeyUsages.ExtIpsecUser, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_timeStamping, KeyUsages.ExtTimeStamping, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_OCSPSigning, KeyUsages.ExtOcspSigning, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.id_kp_smartcardlogon, KeyUsages.ExtSmartCardLogon, ref usages);
			SetBitIfUsagePresent(extKeyUsageExtension, KeyPurposeID.AnyExtendedKeyUsage, KeyUsages.ExtAnyPurpose, ref usages);
		}

		internal static void SetBitIfUsagePresent(ExtendedKeyUsage extKeyUsages, KeyPurposeID keyPurposeId, KeyUsages usage, ref KeyUsages flagsToSet) {
			if (extKeyUsages.HasKeyPurposeId(keyPurposeId)) {
				flagsToSet |= usage;
			}
		}
		internal static void SetBitIfUsagePresent(int keyUsageBitFlags, KeyUsages usage, ref KeyUsages flagsToSet) {
			if ((keyUsageBitFlags & (int)(usage & ~KeyUsages.AllSupportedExt)) != 0) {
				flagsToSet |= usage;
			}
		}
	}
}
