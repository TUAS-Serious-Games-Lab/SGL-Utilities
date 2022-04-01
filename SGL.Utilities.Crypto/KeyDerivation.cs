using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Text;

namespace SGL.Utilities.Crypto {
	/// <summary>
	/// Provides methods to derive secret byte arrays of a given length from a (user-provided) secret and a salt value.
	/// </summary>
	public static class KeyDerivation {
		/// <summary>
		/// Describes the digest algorithms available for the key derivation.
		/// </summary>
		public enum Digest {
			/// <summary>
			/// Represents SHA-256.
			/// </summary>
			Sha256,
			/// <summary>
			/// Represents SHA-384.
			/// </summary>
			Sha384,
			/// <summary>
			/// Represents SHA-512.
			/// </summary>
			Sha512
		}

		private static IDigest getDigest(Digest digest)
			=> digest switch {
				Digest.Sha256 => new Sha256Digest(),
				Digest.Sha384 => new Sha384Digest(),
				Digest.Sha512 => new Sha512Digest(),
				_ => throw new CryptographyException("Unknown digest algorithm.", new ArgumentException("Unknown digest algorithm.", nameof(digest)))
			};

		/// <summary>
		/// Derives a given amount of secret bytes from the given input <paramref name="secret"/> and <paramref name="salt"/>, using the given <paramref name="digest"/> algorithm.
		/// </summary>
		/// <param name="secret">The input secret value of arbitrary length. This can be provided by the user or from the system environment, or can be obtained from a key aggreement algorithm.</param>
		/// <param name="salt">A salt parameter to modify the derivation with. Using different salts with equal <paramref name="secret"/>s  allows deriving different secret values from the same source value.</param>
		/// <param name="numBytes">The number of bytes to derive.</param>
		/// <param name="digest">The digest algorithm to use for derivation.</param>
		/// <returns>The derived bytes.</returns>
		public static byte[] DeriveBytes(byte[] secret, byte[] salt, int numBytes, Digest digest = Digest.Sha256) {
			try {
				var kdf = new Kdf2BytesGenerator(getDigest(digest));
				kdf.Init(new KdfParameters(secret, salt));
				byte[] bytes = new byte[numBytes];
				kdf.GenerateBytes(bytes, 0, numBytes);
				return bytes;
			}
			catch (Exception ex) {
				throw new CryptographyException("Failed to derive bytes from secret.", ex);
			}
		}

		/// <summary>
		/// Derives a given amount of secret bytes from the given input <paramref name="secret"/> and <paramref name="salt"/>, using the given <paramref name="digest"/> algorithm.
		/// This overload acts as a convenience shortcut for <see cref="DeriveBytes(byte[], byte[], int, Digest)"/>, where <paramref name="secret"/> and <paramref name="salt"/> are encoded using UTF8.
		/// </summary>
		/// <param name="secret">The input secret value of arbitrary length. This can be provided by the user or from the system environment, or can be obtained from a key aggreement algorithm.</param>
		/// <param name="salt">A salt parameter to modify the derivation with. Using different salts with equal <paramref name="secret"/>s  allows deriving different secret values from the same source value.</param>
		/// <param name="numBytes">The number of bytes to derive.</param>
		/// <param name="digest">The digest algorithm to use for derivation.</param>
		/// <returns>The derived bytes.</returns>
		public static byte[] DeriveBytes(string secret, string salt, int numBytes, Digest digest = Digest.Sha256) =>
			DeriveBytes(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(salt), numBytes, digest);

		/// <summary>
		/// Derives a given amount of secret bytes from the given input <paramref name="secret"/> and <paramref name="salt"/>, using the given <paramref name="digest"/> algorithm.
		/// This overload acts as a convenience shortcut for <see cref="DeriveBytes(byte[], byte[], int, Digest)"/>, where <paramref name="secret"/> and <paramref name="salt"/> are encoded using UTF8.
		/// </summary>
		/// <param name="secret">The input secret value of arbitrary length. This can be provided by the user or from the system environment, or can be obtained from a key aggreement algorithm.</param>
		/// <param name="salt">A salt parameter to modify the derivation with. Using different salts with equal <paramref name="secret"/>s  allows deriving different secret values from the same source value.</param>
		/// <param name="numBytes">The number of bytes to derive.</param>
		/// <param name="digest">The digest algorithm to use for derivation.</param>
		/// <returns>The derived bytes.</returns>
		public static byte[] DeriveBytes(char[] secret, char[] salt, int numBytes, Digest digest = Digest.Sha256) =>
			DeriveBytes(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(salt), numBytes, digest);
	}
}
