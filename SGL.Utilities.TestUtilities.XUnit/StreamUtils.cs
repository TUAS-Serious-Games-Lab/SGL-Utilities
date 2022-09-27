using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Utilities.TestUtilities.XUnit {
	/// <summary>
	/// Provides utility methods and extension methods for working with streams in test code.
	/// </summary>
	public static class StreamUtils {
		/// <summary>
		/// Asserts that the two given Streams have equal content by reading them in blocks and asserting equality for each block.
		/// </summary>
		/// <param name="expected">The stream with the expected content.</param>
		/// <param name="actual">The stream with the content actually produced by the code under test.</param>
		public static void AssertEqualContent(Stream expected, Stream actual) {
			byte[] expBuff = new byte[32];
			byte[] actBuff = new byte[32];
			var readBytesExp = expected.Read(expBuff, 0, expBuff.Length);
			var readBytesAct = actual.Read(actBuff, 0, actBuff.Length);
			while (readBytesExp > 0 && readBytesAct > 0) {
				var expBytes = expBuff.Take(readBytesExp);
				var actBytes = expBuff.Take(readBytesAct);
				Assert.Equal(expBytes, actBytes);
				readBytesExp = expected.Read(expBuff, 0, expBuff.Length);
				readBytesAct = actual.Read(actBuff, 0, actBuff.Length);
			}
			Assert.Equal(readBytesExp, readBytesAct);
		}

		/// <summary>
		/// Write the content of the given <see cref="Stream"/> containing text to the test case output.
		/// </summary>
		/// <param name="output">The test case output to write to.</param>
		/// <param name="textStream">A stream containing bytes encoding text, to be written to the test case output.</param>
		public static void WriteStreamContents(this ITestOutputHelper output, Stream textStream) {
			using (var rdr = new StreamReader(textStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true)) {
				output.WriteTextReaderContents(rdr);
			}
		}

		/// <summary>
		/// Writes the content of the given <see cref="TextReader"/> to the test case output.
		/// </summary>
		/// <param name="output">The test case output to write to.</param>
		/// <param name="reader">A reader containing text to be written to the test case output.</param>
		public static void WriteTextReaderContents(this ITestOutputHelper output, TextReader reader) {
			foreach (var line in reader.EnumerateLines()) {
				output.WriteLine(line);
			}
		}

		/// <summary>
		/// Writes the given <paramref name="value"/> to <paramref name="output"/> as (indented) JSON text for inspection.
		/// </summary>
		/// <typeparam name="T">The type of the object to write.</typeparam>
		/// <param name="output">The test case output to write to.</param>
		/// <param name="value">The value to write as JSON.</param>
		public static void WriteAsJson<T>(this ITestOutputHelper output, T value) {
			using var buffer = new MemoryStream();
			JsonSerializer.Serialize(buffer, value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
			buffer.Position = 0;
			output.WriteStreamContents(buffer);
		}
	}
}
