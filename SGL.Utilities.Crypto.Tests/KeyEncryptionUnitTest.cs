using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SGL.Utilities.Crypto.Tests {
	public class KeyEncryptionUnitTest {
		private const string rsaRecipient1PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
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
-----END ENCRYPTED PRIVATE KEY-----
";
		private const string rsaRecipient1PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAyAdt4aQTYEpT2BDh+J3n
pw5L1mVtXzJGwxS3CZxxRp0bv7QaT+Nz4gcMZXEjqvNgjmPXRs9qvWVEOHPkU70O
L89Hg30WuYcOsPmkTwxnwchFM8FG9bGZjuR0bZFx01mO+cTpF8V+crfyN8gmmYP4
UqVlIOi1tPE1knIx1xF2DEZWByMEeb51FZ3lbnkGOiNsfQM0dEtjDWIHLlVh7zFH
afqSljZsde7Rep9wsUJr+aMSMqg7i2XAavThFutb625zK3e0Eq+M0Z76RHTz2TP2
bH9xdXHl2nhvXUrgrBcdZSe3HEPPhebM1WqEHZirShxXvRk4jaee4WbD18mLBhEh
3kn+b1g03IxA4pbXRXmFVwJcxlA3/aYvCmVUY5+W1wxpxEI2unVCLWEmWYR+svsq
Nyf7lIiOx/48yYyLhW5XqH3R6HGEmkisyqVS0s2DVAajb9fzlm2wEEIWkeMCr8rn
t9oTPHdUL96wc4oefbwijEQeASee2AI4N1wuXijF8bej9kChJimZRtslmH9nh+ko
Dm7Q4IdeNiq/+XOX1C/dBjIpxd3mgpF4E5gRj2Zc0Iuha7CraGRwsbsrVlRgGpFE
RYnVzX33qmCMY6raKOO3JCL/3b9hgi7T9q9FGT9e3101/SirWnWRHeqNFSI8YKOF
P3DMbsG8jTqJjXxmeJhvI/UCAwEAAQ==
-----END PUBLIC KEY-----
";
		private const string rsaRecipient2PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
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
-----END ENCRYPTED PRIVATE KEY-----
";
		private const string rsaRecipient2PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAwuxJtSsRqRsrSN2hdISn
JvuI5ULbNAbGuWXT8kUj++Cwblu5/plkk9FLnVZCnjp8iful+UScEZ9tpZGekqg9
KI6jnz/ulCn1bHSaxUXkxhmAlBWcez9CE1jDGXc0Jki3e8Co+dL9i/a+nRX1tfD+
2Jz51erkwdvZtATKTO4Kkfr7lp75iUvtfdyGbVrck+x7b/sOOanHN+ohmvNrKT2/
tgiyFXl6sV2MZcjXZi3MFgn/TrQMKwEwD/SfO918/leNO2h8WD2TezUTYvUtSCg5
OzREUHPhusnHU6G8LvowdHks523OjLdYSeINMC9CXRemQpttWCslWEDh+T2WAVwn
qgZ6rZ6JoT9P5Wl19GcY/kdJXchg7qMcCKjnhKFOF/JMMZ7SkbmcwQzO5YrquJUn
nH2motm1DIURPL0rsO1OfranYIMlk3VTsDT3kkxEk5Hnf+wEwPiGgz62gu4FQnWu
nsgAUMbx2bUD4uF6Zlx/M4eFTftMVFi9BN7WudjkqhECT2ACn3oXpPmmS/0fpDv7
KhbIZ77XzRMouyqJysoznJvPkfNUsJZhREIE5kKcFJEOoWOGYwexYiA1RIIyHAua
dqyzBbMj3b2PRR9Ub3sIEaTmihEvbkr5KRvi/OQZqxgN1YaoaLFxZ8fFgoTWVkuj
5kCmdx0jKRqvP42y7W58UuECAwEAAQ==
-----END PUBLIC KEY-----
";
		private const string ecRecipient1PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
MIIBXTBXBgkqhkiG9w0BBQ0wSjApBgkqhkiG9w0BBQwwHAQIrsTMT1VlDwoCAggA
MAwGCCqGSIb3DQIJBQAwHQYJYIZIAWUDBAEqBBAS56EfZrpAGNQXZrBC2iXgBIIB
AA+t1iAAhnxpZq4tMwm3+WND0wsgwg8+gp55cDUh6fPtC7OLl9L3AFQnUWIyFzNj
MwjDBhKFSdfJFj5txiMBQaggUBxQf1yj4Xa3gBBg7DvBwKuFRVZ9DetHNU0wn2Ub
Rhb0JV/Q+et8IIwxcRHpvr+zqBRWbXG/wlGpwA4KzIuKR84uue33AosGNo+Rauj3
EjWitU0yWSwxlXhnutJeyMRewJABSBPPEe7fNpfjM5TPSsOglL8PhU3R5mAuie4d
4tXIj4zbCRVTRSeIMjZgK3yivJZ86ANzJ+htC3es3rHP88hYnKN7XTB+MliX+TAR
0dHL470cSRFGG9/ZHPi/yNQ=
-----END ENCRYPTED PRIVATE KEY-----
";
		private const string ecRecipient1PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQAnLHWybcrZGw7nANLkfcHYZGctGdY
SsB8+Zwgudt1P1gHXRbXk2wVCNst9usOAP11H93S3tnQznx/Mii/7J10Hr4AX3ce
PDk0JycfMCk2wjnZGDDsBQ7uXKPkrFsOOACjp1BrrW7z591Gj3a06GKCetyRoyHu
z/3m9b1w08h3PH7RRck=
-----END PUBLIC KEY-----
";
		private const string ecRecipient2PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
MIIBXTBXBgkqhkiG9w0BBQ0wSjApBgkqhkiG9w0BBQwwHAQIGoErsp/H22oCAggA
MAwGCCqGSIb3DQIJBQAwHQYJYIZIAWUDBAEqBBDHKFjnYMBlClnAF+DFTj/IBIIB
ALYhpU7LqIlOHx7/22vuoSVHY9g+GZpjZ0wqS7ZMEXgFaoIrnHg3hSh6bAvkmXRv
WOjcIokjlF34+GWn3lC9b9oLsPMXIVS2X96CL/C/ENz/N4GBODi7+8aH0Jc0JxeB
8YiGM3VSEUKtd8b9lu6pJ4321riynQSbgdMkYzoV/+fL7d+/wbDfHucGqh7/m3X7
GQjSRHj8IbftOgH4Cdyv5+jO4AnBkls2iowSBt/UoFz0BGHmQ2N5s1UMEG9gzqrs
oXkh4IaU3a9qDiz4dDk+7L1IPu9UOKLXQ4pbIWB0EmK+o9kOKoHJmmIfSDOW9Wpg
XSyJfLJibaK3LhdBfFnsX70=
-----END ENCRYPTED PRIVATE KEY-----
";
		private const string ecRecipient2PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQA0e7WPCg8izLTJvk4n7HkShK6pFTN
4RGE/yYP7lJJpkztRVXWW3/3Df0TfPyENlpwLQ+JuB7DE8DobqNPTECBbHoBWYlN
zecgX3FH6ZUpnqIhRjIyGRT86Zhud+V54c/C80MHGQQ2yXCvrXKu119ISc/4zGDl
Q6hGQAV2/+lnetYF0Zk=
-----END PUBLIC KEY-----
";
		private const string ecRecipient3PrivateKeyPem = @"-----BEGIN ENCRYPTED PRIVATE KEY-----
MIIBXTBXBgkqhkiG9w0BBQ0wSjApBgkqhkiG9w0BBQwwHAQIu3VCFkxAsfUCAggA
MAwGCCqGSIb3DQIJBQAwHQYJYIZIAWUDBAEqBBD4G/QRkbci8SKWJLfkfBSdBIIB
AN/4igPN7FBR3Ruqt3+K85i9ygH9fanB73ztpE9NJG0Ktrcl0Xeyc6yGj220NQ60
mfVOEJ/yT7+n+Slj/X2/5AWTom7DCbsPKIizQhCtB+/g/lRejT7chgdeicuOoLCu
/t7iBO0QI+iBpvCgXQAyM8AfTwV2wkRivVqzUI9VFM0wujok/XPN/8TcJV+di64g
YxJaLukft4dCKgZVswA0vM6uOS5ukQDV4m0wAt627eUlXzgVHAi55zWPKk8d2FJB
TwNIXmLo9tm0vh2cY5UAVOgDDPTJ/Tvz8IsRv6loXnS/txtYPZdRBDsaNdZN9mU0
KL8V4c78A/Lcni5a1Zeq+NE=
-----END ENCRYPTED PRIVATE KEY-----
";
		private const string ecRecipient3PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQB6xVvYWSDcTikNdNFPps9AUVZg2jX
b0EIAr3Uhy9sqHraHNWXUtZ3v/uBxWpQet2c98Ba+Gu3jTVu/+mJ97KxMksAvHWM
b7qZIq+EKADZHDgbuQ0ZvK2dZswsQwRMNDnWgmGOci0MdLcMpQxrOalkYr47ZvaL
44byMVvCnlOwC91Xcbk=
-----END PUBLIC KEY-----
";

		private static readonly Func<char[]> password = () => new char[] { 't', 'e', 's', 't', 'p', 'w' };

		private static KeyPair rsaRecipient1KeyPair;
		private static KeyPair rsaRecipient2KeyPair;
		private static KeyPair ecRecipient1KeyPair;
		private static KeyPair ecRecipient2KeyPair;
		private static KeyPair ecRecipient3KeyPair;
		private static RandomGenerator random = new RandomGenerator();

		private static List<KeyPair> allValidKeyPairs;
		public static IEnumerable<object[]> allValidRecipientKeyPairsSharedAndUnshared;
		private static List<KeyPair> subsetValidKeyPairs;
		private static List<KeyPair> subsetInvalidKeyPairs;
		public static IEnumerable<object[]> subsetInvalidRecipientKeyPairsSharedAndUnshared;

		private static KeyPair LoadKeyPair(string privateKeyPem, string publicKeyPem) {
			using var privStrRdr = new StringReader(privateKeyPem);
			using var pubStrRdr = new StringReader(publicKeyPem);
			return new KeyPair(PublicKey.LoadOneFromPem(pubStrRdr), PrivateKey.LoadOneFromPem(privStrRdr, password));
		}

		static KeyEncryptionUnitTest() {
			rsaRecipient1KeyPair = LoadKeyPair(rsaRecipient1PrivateKeyPem, rsaRecipient1PublicKeyPem);
			rsaRecipient2KeyPair = LoadKeyPair(rsaRecipient2PrivateKeyPem, rsaRecipient2PublicKeyPem);
			ecRecipient1KeyPair = LoadKeyPair(ecRecipient1PrivateKeyPem, ecRecipient1PublicKeyPem);
			ecRecipient2KeyPair = LoadKeyPair(ecRecipient2PrivateKeyPem, ecRecipient2PublicKeyPem);
			ecRecipient3KeyPair = LoadKeyPair(ecRecipient3PrivateKeyPem, ecRecipient3PublicKeyPem);

			allValidKeyPairs = new List<KeyPair>() { rsaRecipient1KeyPair, rsaRecipient2KeyPair, ecRecipient1KeyPair, ecRecipient2KeyPair, ecRecipient3KeyPair };
			subsetValidKeyPairs = new List<KeyPair>() { rsaRecipient1KeyPair, ecRecipient1KeyPair, ecRecipient2KeyPair };
			subsetInvalidKeyPairs = new List<KeyPair>() { rsaRecipient2KeyPair, ecRecipient3KeyPair };
			allValidRecipientKeyPairsSharedAndUnshared = Enumerable.Range(0, 7).SelectMany(dks => allValidKeyPairs.Select(kp => new object[] { kp, true, 1 << dks }).Concat(allValidKeyPairs.Select(kp => new object[] { kp, false, 1 << dks }))).ToList();
			subsetInvalidRecipientKeyPairsSharedAndUnshared = Enumerable.Range(0, 7).SelectMany(dks => subsetInvalidKeyPairs.Select(kp => new object[] { kp, true, 1 << dks }).Concat(subsetInvalidKeyPairs.Select(kp => new object[] { kp, false, 1 << dks }))).ToList();
		}

		[Theory]
		[MemberData(nameof(allValidRecipientKeyPairsSharedAndUnshared))]
		public void AllValidRecipientsCanDecryptEncryptedKey(KeyPair recipient, bool shared, int dataKeySize) {
			byte[] inputDataKey = new byte[dataKeySize];
			random.NextBytes(inputDataKey);

			var encryptor = new KeyEncryptor(allValidKeyPairs.Select(kp => new KeyValuePair<KeyId, PublicKey>(kp.Public.CalculateId(), kp.Public)).ToList(), random, shared);
			var (encryptedKeys, sharedMessageKey) = encryptor.EncryptDataKey(inputDataKey);
			Assert.All(encryptedKeys.Values, k => Assert.NotEqual(inputDataKey, k.EncryptedKey));
			Assert.Equal(shared, sharedMessageKey != null);

			var decryptor = new KeyDecryptor(recipient);
			var outputDataKey = decryptor.DecryptKey(encryptedKeys[recipient.Public.CalculateId()], sharedMessageKey);
			Assert.Equal(inputDataKey, outputDataKey);
		}

		[Theory]
		[MemberData(nameof(subsetInvalidRecipientKeyPairsSharedAndUnshared))]
		public void DecryptingAsUnauthorizedRecipientReturnsNull(KeyPair recipient, bool shared, int dataKeySize) {
			byte[] inputDataKey = new byte[dataKeySize];
			random.NextBytes(inputDataKey);

			var encryptor = new KeyEncryptor(subsetValidKeyPairs.Select(kp => new KeyValuePair<KeyId, PublicKey>(kp.Public.CalculateId(), kp.Public)).ToList(), random, shared);
			var (encryptedKeys, sharedMessageKey) = encryptor.EncryptDataKey(inputDataKey);
			Assert.All(encryptedKeys.Values, k => Assert.NotEqual(inputDataKey, k.EncryptedKey));
			Assert.Equal(shared, sharedMessageKey != null);
			var encInfo = new EncryptionInfo() { IVs = new List<byte[]> { }, DataMode = DataEncryptionMode.AES_256_CCM, DataKeys = encryptedKeys, MessagePublicKey = sharedMessageKey };

			var decryptor = new KeyDecryptor(recipient);
			var outputDataKey = decryptor.DecryptKey(encInfo);
			Assert.Null(outputDataKey);
		}
	}
}
