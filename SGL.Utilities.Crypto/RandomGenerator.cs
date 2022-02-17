using Org.BouncyCastle.Security;

namespace SGL.Utilities.Crypto {
	public class RandomGenerator {
		internal SecureRandom wrapped;

		internal RandomGenerator(SecureRandom wrapped) {
			this.wrapped = wrapped;
		}

		public RandomGenerator() {
			wrapped = new SecureRandom();
		}

		public void NextBytes(byte[] buffer) => wrapped.NextBytes(buffer);

		public RandomGenerator DeriveGenerator(int seedSize, string algorithm) {
			var rnd = SecureRandom.GetInstance(algorithm, false);
			rnd.SetSeed(wrapped.GenerateSeed(seedSize));
			return new RandomGenerator(wrapped);
		}
		public RandomGenerator DeriveGenerator(int seedSize) => DeriveGenerator(seedSize, "SHA256PRNG");
	}
}
