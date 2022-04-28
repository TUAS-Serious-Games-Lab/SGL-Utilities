using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace SGL.Utilities.Crypto {
	/// <summary>
	/// Represents a random generator that is used for various cryptographic operations,
	/// such as generating key pairs, generating symmetric keys, generating initialization vectors, or
	/// generating randomized certificate serial numbers.
	/// </summary>
	public class RandomGenerator {
		internal SecureRandom wrapped;

		internal RandomGenerator(SecureRandom wrapped) {
			this.wrapped = wrapped;
		}

		/// <summary>
		/// Instantiates a new RandomGenerator.
		/// </summary>
		public RandomGenerator() {
			wrapped = new SecureRandom();
		}

		/// <summary>
		/// Generates enough random bytes to fill buffer and writes them to it.
		/// </summary>
		/// <param name="buffer">The buffer to fill with random data.</param>
		public void NextBytes(byte[] buffer) => wrapped.NextBytes(buffer);

		/// <summary>
		/// Generates the given number of random bytes and returns them as a byte array.
		/// </summary>
		/// <param name="size">The number of random bytes to generate.</param>
		/// <returns>The generated random bytes.</returns>
		public byte[] GetBytes(int size) {
			var bytes = new byte[size];
			wrapped.NextBytes(bytes);
			return bytes;
		}

		internal BigInteger GetRandomBigInteger(int size) => new BigInteger(size, wrapped);

		/// <summary>
		/// Derives a new RandomGenerator from this one.
		/// The new generator is seeded from the currente generator and has its own separate internal state.
		/// </summary>
		/// <param name="seedSize">Number of bytes to use for seeding the new generator.</param>
		/// <param name="algorithm">A pseudo-random number generator supported by the underlying cryptography implementation.
		/// This is ususally the name of a digest algorithm followed by <c>PRNG</c>, e.g. <c>SHA256PRNG</c>.</param>
		/// <returns>The derived random number generator.</returns>
		public RandomGenerator DeriveGenerator(int seedSize, string algorithm) {
			var rnd = SecureRandom.GetInstance(algorithm, false);
			rnd.SetSeed(wrapped.GenerateSeed(seedSize));
			return new RandomGenerator(wrapped);
		}
		/// <summary>
		/// Derives a new RandomGenerator from this one.
		/// The new generator is seeded from the currente generator and has its own separate internal state.
		/// This overload uses the <c>SHA256PRNG</c> algorithm.
		/// </summary>
		/// <param name="seedSize">Number of bytes to use for seeding the new generator.</param>
		/// <returns>The derived random number generator.</returns>
		public RandomGenerator DeriveGenerator(int seedSize) => DeriveGenerator(seedSize, "SHA256PRNG");
	}
}
