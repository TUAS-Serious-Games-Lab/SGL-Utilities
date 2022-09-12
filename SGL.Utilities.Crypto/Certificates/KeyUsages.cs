using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Crypto.Certificates {
	/// <summary>
	/// Represents the allowed usages indicated by a certificate for its subject key.
	/// The contained flags combine the values from the KeyUsage and the ExtendedKeyUsage extensions.
	/// </summary>
	[Flags]
	public enum KeyUsages : ulong {
		/// <summary>
		/// The empty value of the bit flag, indicating that no key usages are specified.
		/// </summary>
		NoneDefined = 0,
		// Indices in for non-extended usages follow those in BouncCastle's definition:
		/// <summary>
		/// The key may be used for digital signatures.
		/// </summary>
		DigitalSignature = (1 << 7),
		/// <summary>
		/// The key may be used for signatures for non-repudiation scenarios.
		/// </summary>
		NonRepudiation = (1 << 6),
		/// <summary>
		/// The key may be used to encrypt other keys.
		/// </summary>
		KeyEncipherment = (1 << 5),
		/// <summary>
		/// The key may be used to encrypt any data except keys.
		/// </summary>
		DataEncipherment = (1 << 4),
		/// <summary>
		/// The key may be used for key agreement / exchange.
		/// </summary>
		KeyAgreement = (1 << 3),
		/// <summary>
		/// The key may be used to sign certificates.
		/// </summary>
		KeyCertSign = (1 << 2),
		/// <summary>
		/// The key may be used to sign a certificate revocation list.
		/// </summary>
		CrlSign = (1 << 1),
		/// <summary>
		/// When using <see cref="KeyAgreement"/>, the derived key may only be used for encryption.
		/// </summary>
		EncipherOnly = (1 << 0),
		/// <summary>
		/// When using <see cref="KeyAgreement"/>, the derived key may only be used for decryption.
		/// </summary>
		DecipherOnly = (1 << 15),
		/// <summary>
		/// A bit flag that contains all supported basic key usages, 
		/// can e.g. be used to check whether a certificate needs the key usage extension, 
		/// when generating a certificate.
		/// </summary>
		AllBasic = DigitalSignature | NonRepudiation | KeyEncipherment | DataEncipherment | KeyAgreement |
			KeyCertSign | CrlSign | EncipherOnly | DecipherOnly,
		// Extended usages:
		/// <summary>
		/// The key may be used for authenticating a (TLS) server.
		/// </summary>
		ExtServerAuth = (1 << 16),
		/// <summary>
		/// The key may be used for authenticating a (TLS) client.
		/// </summary>
		ExtClientAuth = (1 << 17),
		/// <summary>
		/// The key may be used for signing software.
		/// </summary>
		ExtCodeSigning = (1 << 18),
		/// <summary>
		/// The key may be used for protecting E-Mail, i.e. for S/MIME.
		/// </summary>
		ExtEmailProtection = (1 << 19),
		/// <summary>
		/// The key may be used for IPSec on a server.
		/// </summary>
		ExtIpsecEndSystem = (1 << 20),
		/// <summary>
		/// The key may be used for IPSec for a tunnel.
		/// </summary>
		ExtIpsecTunnel = (1 << 21),
		/// <summary>
		/// The key may be used for IPSec for a user.
		/// </summary>
		ExtIpsecUser = (1 << 22),
		/// <summary>
		/// The key may be used for trusted time stamping.
		/// </summary>
		ExtTimeStamping = (1 << 23),
		/// <summary>
		/// The key may be used for the Online Certificate Status Protocol.
		/// </summary>
		ExtOcspSigning = (1 << 24),
		/// <summary>
		/// The key may be used for a smart card-based login protocol.
		/// </summary>
		ExtSmartCardLogon = (1 << 25),
		/// <summary>
		/// Indicates the special 'any' purpose value in the extended key purpose extension.
		/// </summary>
		ExtAnyPurpose = (1 << 32),
		/// <summary>
		/// A bit flag that contains all supported extended key usages, 
		/// can e.g. be used to check whether a certificate needs the extended key usage extension, 
		/// when generating a certificate.
		/// </summary>
		AllSupportedExt = ExtServerAuth | ExtClientAuth | ExtCodeSigning | ExtEmailProtection |
			ExtIpsecEndSystem | ExtIpsecTunnel | ExtIpsecUser | ExtTimeStamping | ExtOcspSigning | ExtSmartCardLogon | ExtAnyPurpose
	}
}
