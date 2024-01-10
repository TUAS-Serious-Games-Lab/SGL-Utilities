using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	public class ConcatReader : TextReader {
		private int currentReaderIndex = 0;
		private List<TextReader> readers = new List<TextReader>();

		public ConcatReader(IEnumerable<TextReader> readers) {
			this.readers = readers.ToList();
		}

		public override void Close() {
			foreach (TextReader reader in readers) {
				reader.Close();
			}
		}

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
