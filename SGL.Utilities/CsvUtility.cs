using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides static methods for reading and writing CSV data (with <c>;</c> as the separator) into / from an in-memory representation where the column names are given as a list of string and
	/// the entries are given as a list of dictionaries, one dictionary per entry, with the column names as keys and the fields of the entry as string values.
	/// When reading, the number of fields in each entry must match the number of columns in the header, shorter or longer lines are not supported.
	/// When writing, entry dictionaries don't need to have a key-value pair for each specified column, missing fields are written as an empty string.
	/// Key-value pairs in entry dictionaries whose key doesn't correspond to a column are ignored during writing.
	/// </summary>
	public static class CsvUtility {
		/// <summary>
		/// Writes the given <paramref name="entries"/> to <paramref name="output"/> in CSV format using the given <paramref name="columns"/>.
		/// </summary>
		public static void WriteCsv(TextWriter output, List<string> columns, List<Dictionary<string, string>> entries) {
			Task.Run(() => WriteCsvAsync(output, columns, entries)).Wait();
		}
		/// <summary>
		/// Asynchronously writes the given <paramref name="entries"/> to <paramref name="output"/> in CSV format using the given <paramref name="columns"/>.
		/// </summary>
		public static async Task WriteCsvAsync(TextWriter output, List<string> columns, List<Dictionary<string, string>> entries, CancellationToken ct = default) {
			var lines = entries
				.Select(entry => string.Join(";", columns.Select(col => EscapeString(entry.TryGetValue(col, out var val) ? val : ""))))
				.Prepend(string.Join(";", columns.Select(EscapeString)));
			foreach (var line in lines) {
				await output.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Asynchronously writes the given <paramref name="columns"/> to <paramref name="output"/> as a CSV header line.
		/// </summary>
		public static async Task WriteCsvHeaderAsync(TextWriter output, List<string> columns, CancellationToken ct = default) {
			var line = string.Join(";", columns.Select(EscapeString));
			await output.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
		}

		/// <summary>
		/// Asynchronously writes the given <paramref name="entry"/> to <paramref name="output"/> as a CSV entry line.
		/// The fields (keyed by column name) are writen in the order represented in <paramref name="columns"/>.
		///	Fields not mentioned in <paramref name="columns"/> are not written.
		/// </summary>
		public static Task WriteCsvEntryAsync(TextWriter output, List<string> columns, Dictionary<string, string> entry, CancellationToken ct = default) {
			return WriteCsvEntryAsync(output, columns.Select(col => entry.TryGetValue(col, out var val) ? val : ""), ct);
		}

		/// <summary>
		/// Asynchronously writes the given <paramref name="entryFields"/> to <paramref name="output"/> as a CSV entry in the given field order.
		/// </summary>
		public static async Task WriteCsvEntryAsync(TextWriter output, IEnumerable<string> entryFields, CancellationToken ct = default) {
			var line = string.Join(";", entryFields.Select(EscapeString));
			await output.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
		}

		private static readonly char[] charsToEscape = Environment.NewLine.ToCharArray().Append('"').Append(';').ToArray();
		private static string EscapeString(string str) {
			if (str.IndexOfAny(charsToEscape) == -1) {
				return str;
			}
			else {
				return $"\"{str.Replace("\"", "\"\"")}\"";
			}
		}
		/// <summary>
		/// Reads <paramref name="input"/> as CSV data.
		/// </summary>
		/// <returns>A tuple consisting of the columns from the CSV header and of a list of dictionaries representing the entries.</returns>
		public static (List<string> Columns, List<Dictionary<string, string>> Entries) ReadCsv(TextReader input) {
			return Task.Run(() => ReadCsvAsync(input)).Result;
		}
		/// <summary>
		/// Asynchronously reads <paramref name="input"/> as CSV data.
		/// </summary>
		/// <returns>A task representing the async operation, 
		/// providing a tuple consisting of the columns from the CSV header and of a list of dictionaries representing the entries.</returns>
		public static async Task<(List<string> Columns, List<Dictionary<string, string>> Entries)> ReadCsvAsync(TextReader input, CancellationToken ct = default) {
			var entries = new List<Dictionary<string, string>>();
			var headerLine = await input.ReadLineAsync().ConfigureAwait(false);
			ct.ThrowIfCancellationRequested();
			if (headerLine == null) {
				throw new InvalidDataException("Couldn't read header from CSV.");
			}
			var sb = new StringBuilder();
			var columnNames = (await ParseLine(headerLine, sb, input.ReadLineAsync, ct).ToListAsync(ct).ConfigureAwait(false)).ToList();
			string? line;
			while ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null) {
				ct.ThrowIfCancellationRequested();
				var fields = await ParseLine(line, sb, input.ReadLineAsync, ct).ToListAsync(ct).ConfigureAwait(false);
				if (fields.Count != columnNames.Count) {
					throw new InvalidDataException("Encountered line with incorrect field count.");
				}
				var entry = new Dictionary<string, string>(columnNames.Zip(fields, KeyValuePair.Create));
				entries.Add(entry);
			}
			return (columnNames, entries);
		}

		/// <summary>
		/// Reads a CSV header (line) from <paramref name="input"/> and returns the column names.
		/// </summary>
		/// <exception cref="InvalidDataException">If no line could be read.</exception>
		public static async Task<List<string>> ReadCsvHeaderAsync(TextReader input, CancellationToken ct = default) {
			var headerLine = await input.ReadLineAsync().ConfigureAwait(false);
			ct.ThrowIfCancellationRequested();
			if (headerLine == null) {
				throw new InvalidDataException("Couldn't read header from CSV.");
			}
			var sb = new StringBuilder();
			return (await ParseLine(headerLine, sb, input.ReadLineAsync, ct).ToListAsync(ct).ConfigureAwait(false)).ToList();
		}

		/// <summary>
		/// Reads a CSV entry from <paramref name="input"/> and returns its fields in the order they were in <paramref name="input"/>.
		/// If no entry could be read from <paramref name="input"/>, null is returned.
		/// </summary>
		public static async Task<List<string>?> ReadCsvEntryAsync(TextReader input, CancellationToken ct = default) {
			var sb = new StringBuilder();
			string? line;
			if ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null) {
				ct.ThrowIfCancellationRequested();
				return (await ParseLine(line, sb, input.ReadLineAsync, ct).ToListAsync(ct).ConfigureAwait(false)).ToList();
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Reads a CSV entry from <paramref name="input"/> with the column order specified in <paramref name="columnNames"/>.
		/// The fields of the entry are returned as a dictionary keyed by column names.
		/// If no entry could be read from <paramref name="input"/>, null is returned.
		/// </summary>
		/// <exception cref="InvalidDataException">The line(s) read from <paramref name="input"/> had a number of fields different
		/// from the number of columns indicated by <paramref name="columnNames"/>.</exception>
		public static async Task<Dictionary<string, string>?> ReadCsvEntryAsync(TextReader input, List<string> columnNames, CancellationToken ct = default) {
			var fields = await ReadCsvEntryAsync(input, ct);
			if (fields == null) {
				return null;
			}
			if (fields.Count != columnNames.Count) {
				throw new InvalidDataException("Encountered line with incorrect field count.");
			}
			return new Dictionary<string, string>(columnNames.Zip(fields, KeyValuePair.Create));
		}

		/// <summary>
		/// Asynchronously reads and enumerates all CSV entries from <paramref name="input"/>.
		/// It is assumed, that the header line was already read from <paramref name="input"/>,
		/// e.g. using <see cref="ReadCsvHeaderAsync(TextReader, CancellationToken)"/>.
		/// The entries are returned as string lists with their fields in the order present in <paramref name="input"/>.
		/// </summary>
		/// <exception cref="InvalidDataException">If an entry is encountered whose field count differs from previous entries.</exception>
		public static async IAsyncEnumerable<List<string>> ReadCsvEntriesAsync(TextReader input,
				[EnumeratorCancellation] CancellationToken ct = default) {
			var sb = new StringBuilder();
			string? line;
			int? fieldCount = null;
			while ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null) {
				ct.ThrowIfCancellationRequested();
				var fields = (await ParseLine(line, sb, input.ReadLineAsync, ct).ToListAsync(ct).ConfigureAwait(false)).ToList();
				if (!fieldCount.HasValue) {
					fieldCount = fields.Count;
				}
				else if (fields.Count != fieldCount.Value) {
					throw new InvalidDataException("Encountered line with differing field count.");
				}
				yield return fields;
			}
		}

		/// <summary>
		/// Asynchronously reads and enumerates all CSV entries from <paramref name="input"/>.
		/// It is assumed, that the header line was already read from <paramref name="input"/>,
		/// e.g. using <see cref="ReadCsvHeaderAsync(TextReader, CancellationToken)"/>.
		/// The entries are returned as dictionaries keyed by column name.
		/// The column names and their ordering is indicated by <paramref name="columnNames"/>.
		/// </summary>
		/// <exception cref="InvalidDataException">If an entry with a field count different from the number of columns is encountered.</exception>
		public static async IAsyncEnumerable<Dictionary<string, string>> ReadCsvEntriesAsync(TextReader input, List<string> columnNames,
				[EnumeratorCancellation] CancellationToken ct = default) {
			var sb = new StringBuilder();
			string? line;
			while ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null) {
				ct.ThrowIfCancellationRequested();
				var fields = await ParseLine(line, sb, input.ReadLineAsync, ct).ToListAsync(ct).ConfigureAwait(false);
				if (fields.Count != columnNames.Count) {
					throw new InvalidDataException("Encountered line with incorrect field count.");
				}
				yield return new Dictionary<string, string>(columnNames.Zip(fields, KeyValuePair.Create));
			}
		}

		private static async IAsyncEnumerable<string> ParseLine(string line, StringBuilder sb, Func<Task<string?>> readNextLine,
				[EnumeratorCancellation] CancellationToken ct = default) {
			sb.Clear();
			for (int pos = 0; pos < line.Length; pos++) {
				if (line[pos] == '"') {
					pos++;
					for (; pos < line.Length; pos++) {
						if (line[pos] == '"' && pos + 1 < line.Length && line[pos + 1] == '"') {
							sb.Append('"');
							pos++;
						}
						else if (line[pos] == '"') {
							pos++;
							break;
						}
						else {
							sb.Append(line[pos]);
						}
						if (pos + 1 == line.Length) { // This field spans multiple lines, read next line and continue
							ct.ThrowIfCancellationRequested();
							var nextLine = (await readNextLine().ConfigureAwait(false))
								?? throw new InvalidDataException("Unexpected end of quoted field");
							line = nextLine;
							sb.AppendLine();
							pos = -1; // Set to 0 with increment in for loop
						}
					}
					if (line[pos - 1] != '"' || line[pos] != ';') {
						throw new InvalidDataException("Unexpected end of quoted field");
					}
					else {
						yield return sb.ToString();
						sb.Clear();
					}
				}
				else if (line[pos] == ';') {
					yield return sb.ToString();
					sb.Clear();
				}
				else {
					sb.Append(line[pos]);
				}
			}
			yield return sb.ToString();
		}
	}
}