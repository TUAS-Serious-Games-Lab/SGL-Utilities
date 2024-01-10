using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides a <see cref="TextReader"/> implementation that reads through the contents of multiple other <see cref="TextReader"/>s and 
	/// provides as its content a concatenation of the other reader's contents.
	/// </summary>
	public class ConcatReader : TextReader {
		private int currentReaderIndex = 0;
		private List<TextReader> readers = new List<TextReader>();

		/// <summary>
		/// Constructs a <see cref="ConcatReader"/> that represents the concatenated contents of the given <paramref name="readers"/>.
		/// </summary>
		/// <param name="readers">The readers of which to concatenate the contents.</param>
		public ConcatReader(IEnumerable<TextReader> readers) {
			this.readers = readers.ToList();
		}

		/// <summary>
		/// Implements closing by closing all constituent readers.
		/// </summary>
		public override void Close() {
			foreach (TextReader reader in readers) {
				reader.Close();
			}
		}

		/// <summary>
		/// Reads the next character without consuming it from the input sequence.
		/// </summary>
		/// <returns>The result of the next <see cref="Read()"/> call.</returns>
		public override int Peek() {
			int scanReaderIndex = currentReaderIndex;
			while (true) {
				var rdr = GetInnerReader(scanReaderIndex);
				if (rdr == null) {
					return -1;
				}
				var res = rdr.Peek();
				if (res != -1) {
					return res;
				}
				scanReaderIndex++;
			}
		}

		/// <summary>
		/// Reads the next character and consumes it from the input sequence.
		/// </summary>
		/// <returns>The character value of the next character, or <c>-1</c> if the end of the content was reached.</returns>
		public override int Read() {
			while (true) {
				var rdr = GetInnerReader(currentReaderIndex);
				if (rdr == null) {
					return -1;
				}
				var res = rdr.Read();
				if (res != -1) {
					return res;
				}
				currentReaderIndex++;
			}
		}

		/// <summary>
		/// Disposes this reader and if <paramref name="disposing"/> is true, also all constituent readers.
		/// </summary>
		/// <param name="disposing">False to only dispose unmanaged ressources of this reader, 
		/// true to also dispose the managed ressources, i.e. the constituent readers.</param>
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (disposing) {
				foreach (var reader in readers) {
					reader.Dispose();
				}
			}
		}

		private TextReader? GetInnerReader(int readerIndex) {
			if (readerIndex < readers.Count) {
				return readers[readerIndex];
			}
			else {
				return null;
			}
		}
	}
}
