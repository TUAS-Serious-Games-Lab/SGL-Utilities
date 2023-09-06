using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Provides generic functionality to segment (long) text (files) into blocks of lines for further processing,
	/// optionally also supporting reading lines that satisfy certain criteria as separators.
	/// </summary>
	public static class TextSegmenter {
		/// <summary>
		/// Reads through the content of <paramref name="reader"/> and splits it into segments of <paramref name="maxLinesPerSegment"/> lines or fewer.
		/// For each segment, <paramref name="onSegment"/> is invoked once.
		/// Each line is also passed to <paramref name="isSeparator"/> and if <see langword="true"/> is returned,
		/// the current segment is ended and <paramref name="onSeparator"/> is called afterwards, before beginning the next segment.
		/// The lines that are deemed to be separator are not part of the segment content, but the line's content is instead provided to <paramref name="onSeparator"/>.
		/// </summary>
		/// <param name="reader">The reader through which to read.</param>
		/// <param name="maxLinesPerSegment">The maximum number of lines in a segment, i.e. segments are completed after this many lines, before a separator, or at the end of the input.</param>
		/// <param name="onSegment">A delegate to invoke for each read segment. The segment's content is passed as a string.</param>
		/// <param name="isSeparator">A delegate invoked to decide whether a line is a separator.</param>
		/// <param name="onSeparator">A delegate to invoke for each separator. The line's content is passed as a string.</param>
		public static void ReadTextSegments(TextReader reader, int maxLinesPerSegment, Action<string> onSegment,
				Func<string, bool> isSeparator, Action<string> onSeparator) {
			string line;
			int segmentLineCount = 0;
			var lines = new StringBuilder();
			while ((line = reader.ReadLine()) != null) {
				if (isSeparator(line)) {
					if (lines.Length > 0) {
						onSegment(lines.ToString());
						lines.Clear();
						segmentLineCount = 0;
					}
					onSeparator(line);
				}
				else {
					if (lines.Length > 0) {
						lines.AppendLine();
					}
					lines.Append(line);
					segmentLineCount++;
					if (segmentLineCount >= maxLinesPerSegment) {
						onSegment(lines.ToString());
						lines.Clear();
						segmentLineCount = 0;
					}
				}
			}
			if (lines.Length > 0) {
				onSegment(lines.ToString());
			}
		}
		/// <summary>
		/// Reads through the content of <paramref name="reader"/> and splits it into segments of <paramref name="maxLinesPerSegment"/> lines or fewer.
		/// For each segment, <paramref name="onSegment"/> is invoked once.
		/// </summary>
		/// <param name="reader">The reader through which to read.</param>
		/// <param name="maxLinesPerSegment">The maximum number of lines in a segment, i.e. segments are completed after this many lines or at the end of the input.</param>
		/// <param name="onSegment">A delegate to invoke for each read segment. The segment's content is passed as a string.</param>
		public static void ReadTextSegments(TextReader reader, int maxLinesPerSegment, Action<string> onSegment) {
			string line;
			int segmentLineCount = 0;
			var lines = new StringBuilder();
			while ((line = reader.ReadLine()) != null) {
				if (lines.Length > 0) {
					lines.AppendLine();
				}
				lines.Append(line);
				segmentLineCount++;
				if (segmentLineCount >= maxLinesPerSegment) {
					onSegment(lines.ToString());
					lines.Clear();
					segmentLineCount = 0;
				}
			}
			if (lines.Length > 0) {
				onSegment(lines.ToString());
			}
		}
		/// <summary>
		/// Reads through the content of <paramref name="reader"/> and splits it into segments of <paramref name="maxLinesPerSegment"/> lines or fewer.
		/// Each line is also passed to <paramref name="isSeparator"/> and if <see langword="true"/> is returned,
		/// the current segment is ended and the line is treated as a separator.
		/// The lines that are deemed to be separator are not part of the segment content.
		/// Instead, the returned items either contain a segment content or the content of a separator line.
		/// </summary>
		/// <param name="reader">The reader through which to read.</param>
		/// <param name="maxLinesPerSegment">The maximum number of lines in a segment, i.e. segments are completed after this many lines, before a separator, or at the end of the input.</param>
		/// <param name="isSeparator">A delegate invoked to decide whether a line is a separator.</param>
		/// <returns>
		/// An enumerable providing tuples where the first element indicates whether it represents a separator.
		/// Separator tuples have the line's content as the second element and segment tuples have the all lines in the segment as the second element.
		/// </returns>
		public static IEnumerable<(bool IsSeparator, string Content)> ReadTextSegments(TextReader reader, int maxLinesPerSegment, Func<string, bool> isSeparator) {
			string line;
			int segmentLineCount = 0;
			var lines = new StringBuilder();
			while ((line = reader.ReadLine()) != null) {
				if (isSeparator(line)) {
					if (lines.Length > 0) {
						yield return (IsSeparator: false, lines.ToString());
						lines.Clear();
						segmentLineCount = 0;
					}
					yield return (IsSeparator: true, line);
				}
				else {
					if (lines.Length > 0) {
						lines.AppendLine();
					}
					lines.Append(line);
					segmentLineCount++;
					if (segmentLineCount >= maxLinesPerSegment) {
						yield return (IsSeparator: false, lines.ToString());
						lines.Clear();
						segmentLineCount = 0;
					}
				}
			}
			if (lines.Length > 0) {
				yield return (IsSeparator: false, lines.ToString());
			}
		}
		/// <summary>
		/// Asynchronously reads through the content of <paramref name="reader"/> and splits it into segments of <paramref name="maxLinesPerSegment"/> lines or fewer.
		/// Each line is also passed to <paramref name="isSeparator"/> and if <see langword="true"/> is returned,
		/// the current segment is ended and the line is treated as a separator.
		/// The lines that are deemed to be separator are not part of the segment content.
		/// Instead, the returned items either contain a segment content or the content of a separator line.
		/// </summary>
		/// <param name="reader">The reader through which to read.</param>
		/// <param name="maxLinesPerSegment">The maximum number of lines in a segment, i.e. segments are completed after this many lines, before a separator, or at the end of the input.</param>
		/// <param name="isSeparator">
		/// A delegate invoked to decide whether a line is a separator.
		/// Note: This delegate is NOT invoked on the original context but on the thread poll. 
		/// It should therefore not perform thread-unsafe operations on captured data.
		/// Usually it should just operate on the passed line content.
		/// </param>
		/// <param name="ct">A cancellation token to cancel the asynchronous operation.</param>
		/// <returns>
		/// An async enumerable providing tuples where the first element indicates whether it represents a separator.
		/// Separator tuples have the line's content as the second element and segment tuples have the all lines in the segment as the second element.
		/// </returns>
		public static async IAsyncEnumerable<(bool IsSeparator, string Content)> ReadTextSegmentsAsync(TextReader reader, int maxLinesPerSegment,
				Func<string, bool> isSeparator, [EnumeratorCancellation] CancellationToken ct = default) {
			string line;
			int segmentLineCount = 0;
			var lines = new StringBuilder();
#if NET7_0_OR_GREATER
			while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null) {
#else
			while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
				ct.ThrowIfCancellationRequested();
#endif
				if (isSeparator(line)) {
					if (lines.Length > 0) {
						yield return (IsSeparator: false, lines.ToString());
						lines.Clear();
						segmentLineCount = 0;
					}
					yield return (IsSeparator: true, line);
				}
				else {
					if (lines.Length > 0) {
						lines.AppendLine();
					}
					lines.Append(line);
					segmentLineCount++;
					if (segmentLineCount >= maxLinesPerSegment) {
						yield return (IsSeparator: false, lines.ToString());
						lines.Clear();
						segmentLineCount = 0;
					}
				}
			}
			if (lines.Length > 0) {
				yield return (IsSeparator: false, lines.ToString());
			}
		}
		/// <summary>
		/// Asynchronously reads through the content of <paramref name="reader"/> and splits it into segments of <paramref name="maxLinesPerSegment"/> lines or fewer.
		/// </summary>
		/// <param name="reader">The reader through which to read.</param>
		/// <param name="maxLinesPerSegment">The maximum number of lines in a segment, i.e. segments are completed after this many lines or at the end of the input.</param>
		/// <param name="ct">A cancellation token to cancel the asynchronous operation.</param>
		/// <returns>
		/// An async enumerable providing the segments as strings.
		/// </returns>
		public static async IAsyncEnumerable<string> ReadTextSegmentsAsync(TextReader reader, int maxLinesPerSegment,
				[EnumeratorCancellation] CancellationToken ct = default) {
			string line;
			int segmentLineCount = 0;
			var lines = new StringBuilder();
#if NET7_0_OR_GREATER
			while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null) {
#else
			while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null) {
				ct.ThrowIfCancellationRequested();
#endif
				if (lines.Length > 0) {
					lines.AppendLine();
				}
				lines.Append(line);
				segmentLineCount++;
				if (segmentLineCount >= maxLinesPerSegment) {
					yield return lines.ToString();
					lines.Clear();
					segmentLineCount = 0;
				}
			}
			if (lines.Length > 0) {
				yield return lines.ToString();
			}
		}
	}
}
