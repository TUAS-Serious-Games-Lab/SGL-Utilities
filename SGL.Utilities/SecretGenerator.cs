using System;
using System.Security.Cryptography;

namespace SGL.Utilities {

	/// <summary>
	/// Provides a generator for cryptographically random base64 strings.
	/// </summary>
	public class SecretGenerator {
#if NETSTANDARD
		private RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
		private object lockObject = new object();
#endif
		private SecretGenerator() { }

		/// <summary>
		/// Provides a singleton instance of the <see cref="SecretGenerator"/>.
		/// </summary>
		public static SecretGenerator Instance = new SecretGenerator();

		/// <summary>
		/// Generates the given number of bytes of randomness and returns them encoded using base64.
		/// </summary>
		/// <param name="bytes">The number of random bytes to draw from the underlying RNG.</param>
		/// <returns>A base64 string encoding the random bytes.</returns>
		/// <remarks>Note: The returned string is longer than the number given in <c>bytes</c>, because the given number of random bytes are base64-encoded after drawing them from the RNG.</remarks>
		public string GenerateSecret(int bytes) {
#if NETSTANDARD
			byte[] buff = new byte[bytes];
			lock (lockObject) {
				rngCsp.GetBytes(buff);
			}
#else
			byte[] buff = RandomNumberGenerator.GetBytes(bytes);
#endif
			return Convert.ToBase64String(buff);
		}
	}
}
