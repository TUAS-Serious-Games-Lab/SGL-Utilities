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
				.Prepend(string.Join(";", columns));
			foreach (var line in lines) {
				await output.WriteLineAsync(line.AsMemory(), ct);
			}
		}

		private static char[] charsToEscape = Environment.NewLine.ToCharArray().Append('"').Append(';').ToArray();
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
			var headerLine = await input.ReadLineAsync();
			ct.ThrowIfCancellationRequested();
			if (headerLine == null) {
				throw new InvalidDataException("Couldn't read header from CSV.");
			}
			var sb = new StringBuilder();
			var columnNames = (await ParseLine(headerLine, sb, () => input.ReadLineAsync()).ToListAsync(ct)).ToList();
			string? line;
			while ((line = await input.ReadLineAsync()) != null) {
				ct.ThrowIfCancellationRequested();
				var fields = await ParseLine(line, sb, () => input.ReadLineAsync()).ToListAsync(ct);
				if (fields.Count != columnNames.Count) {
					throw new InvalidDataException("Encountered line with incorrect field count.");
				}
				var entry = new Dictionary<string, string>(columnNames.Zip(fields, KeyValuePair.Create));
				entries.Add(entry);
			}
			return (columnNames, entries);
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
							var nextLine = await readNextLine();
							if (nextLine == null) {
								throw new InvalidDataException("Unexpected end of quoted field");
							}
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