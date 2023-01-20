using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities {

	/// <summary>
	/// Provides utility extension methods for working with enumerables.
	/// </summary>
	public static class EnumerableExtensions {
		/// <summary>
		/// Converts <paramref name="source"/> into an <see cref="IEnumerable{T}"/> of arrays that contain consecutive partitions of <paramref name="source"/> of the given <paramref name="batchSize"/>.
		/// The last array is shorter if the element count of <paramref name="source"/> is not an integer multiple of <paramref name="batchSize"/>.
		/// </summary>
		/// <typeparam name="T">The type of the elements.</typeparam>
		/// <param name="source">An enumerable containing the input elements.</param>
		/// <param name="batchSize">The desired size for the batch arrays.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> of the batched arrays.</returns>
		public static IEnumerable<T[]> AsArrayBatches<T>(this IEnumerable<T> source, int batchSize) {
			if (batchSize <= 0) {
				throw new ArgumentOutOfRangeException(nameof(batchSize), "The size of the batches must be positive.");
			}
			while (source.Count() > 0) {
				var batch = source.Take(batchSize).ToArray();
				source.Skip(batchSize);
				yield return batch;
			}
		}

		/// <summary>
		/// Processes the elements of <c>source</c> in batches of size <c>batchSize</c> by invoking <c>batchFunc</c> for each batch and enables asynchronous enumeration of the results.
		/// </summary>
		/// <typeparam name="TSource">The type of the inputs.</typeparam>
		/// <typeparam name="TResult">The type of the produced results.</typeparam>
		/// <param name="source">An enumerable containing the input elements.</param>
		/// <param name="batchSize">The maximum size of the batches passed to <c>batchFunc</c>.</param>
		/// <param name="batchFunc">A function object that is callable with an <c><![CDATA[IEnumerable<TSource>]]></c> and that returns an <c><![CDATA[IEnumerable<Task<TResult>>]]></c> representing the asynchronous processing results for the elements.</param>
		/// <param name="ct">A cancellation token to cancel the batching logic.
		/// Usually, this token should also be used in the lambda passed to <c>batchFunc</c> to also cancel the asynchronous operations initiated there.</param>
		/// <returns>An <c><![CDATA[IAsyncEnumerable<TResult>]]></c> allowing asynchronous enumeration of the awaited results of the operations started in the batches.</returns>
		/// <remarks>This method is useful to concurrently perform asynchronous operations on many elements while enforcing a maximum concurreny limit to prevent ressource exhaustion.</remarks>
		public static async IAsyncEnumerable<TResult> BatchAsync<TSource, TResult>(this IEnumerable<TSource> source, int batchSize,
			Func<IEnumerable<TSource>, IEnumerable<Task<TResult>>> batchFunc, [EnumeratorCancellation] CancellationToken ct = default) {
			if (batchSize <= 0) {
				throw new ArgumentOutOfRangeException(nameof(batchSize), "The size of the batches must be positive.");
			}
			static bool getBatch(ref IEnumerable<TSource> source, int batchSize, out IEnumerable<TSource> batch, CancellationToken ct = default) {
				batch = source.Take(batchSize);
				source = source.Skip(batchSize);
				ct.ThrowIfCancellationRequested();
				return batch.Any();
			}

			IEnumerable<TResult>? results = null;
			while (getBatch(ref source, batchSize, out var batch, ct)) {
				var batchTask = Task.WhenAll(batchFunc(batch));
				if (results != null) {
					foreach (var res in results) {
						ct.ThrowIfCancellationRequested();
						yield return res;
					}
				}
				ct.ThrowIfCancellationRequested();
				results = await batchTask;
			}
			if (results != null) {
				foreach (var res in results) {
					ct.ThrowIfCancellationRequested();
					yield return res;
				}
			}
		}
		/// <summary>
		/// Maps the elemets from <c>source</c> using an asynchronous operation by buffering pending tasks up to a given buffer size.
		/// When the buffer is full, pending tasks are awaited in their input order and their results are transferred to an output buffer to make room for the next operations.
		/// The returned object allows asynchronously enumerating the output buffer to consume completed results when they become available.
		/// </summary>
		/// <typeparam name="TSource">The type of the inputs</typeparam>
		/// <typeparam name="TResult">The type of the outputs</typeparam>
		/// <param name="source">An enumerable containing the input elements.</param>
		/// <param name="bufferSize">The size of the buffers, limiting the concurrency.</param>
		/// <param name="mapFunc">A function object implementing the asynchronous operation, taking an input element and returning a <see cref="Task{TResult}"/> for waiting for the result.</param>
		/// <param name="ct">A cancellation token to cancel the mapping logic.
		/// Usually, this token should also be used in the lambda passed to <c>mapFunc</c> to also cancel the asynchronous operations initiated there.</param>
		/// <returns>An <c><![CDATA[IAsyncEnumerable<TResult>]]></c> allowing asynchronous enumeration of the awaited results of the operations started in <c>mapFunc</c>.</returns>
		/// <remarks>This method is useful to concurrently perform asynchronous operations on many elements while enforcing a maximum concurreny limit to prevent ressource exhaustion.</remarks>
		public static async IAsyncEnumerable<TResult> MapBufferedAsync<TSource, TResult>(this IEnumerable<TSource> source, int bufferSize, Func<TSource, Task<TResult>> mapFunc, [EnumeratorCancellation] CancellationToken ct = default) {
			if (bufferSize <= 0) {
				throw new ArgumentOutOfRangeException(nameof(bufferSize), "The size of the buffer must be positive.");
			}
			var exceptionList = new List<Exception>();
			var mapBuffer = new Queue<Task<TResult>>();
			var yieldBuffer = new Queue<TResult>();
			using var sourceEnumerator = source.GetEnumerator();
			var sourceAvailable = sourceEnumerator.MoveNext();
			// Iterate as long as we have anything that will eventually be yield returned.
			while (sourceAvailable || mapBuffer.Count > 0 || yieldBuffer.Count > 0) {
				ct.ThrowIfCancellationRequested();
				bool progress = false;
				try {
					// Map source elements if available and mapBuffer is not full.
					while (sourceAvailable && mapBuffer.Count < bufferSize) {
						progress = true;
						mapBuffer.Enqueue(mapFunc(sourceEnumerator.Current));
						sourceAvailable = sourceEnumerator.MoveNext();
					}
				}
				catch (Exception ex) {
					// Wrap exception and place it at end of mapBuffer for the next loop to find it:
					mapBuffer.Enqueue(Task.FromException<TResult>(ex));
					// Stop invoking new mapFunc calls by pretending there is no more input,
					// as we will terminate after the throwing input anyway:
					sourceAvailable = false;
				}
				try {
					// Transfer completed Tasks to yieldBuffer if it isn't full yet.
					while (yieldBuffer.Count < bufferSize && mapBuffer.TryPeek(out var peekTask) && peekTask.IsCompleted) {
						progress = true;
						yieldBuffer.Enqueue(await mapBuffer.Dequeue()); // Unwrap using await, doesn't suspend beacuse the task is ready.
					}
				}
				catch (Exception ex) {
					exceptionList.Add(ex);
					break;
				}
				// yield return element from yieldBuffer if available.
				// No loop, because we should re-evaluate the above loops after the suspension by yield return.
				// So we simply do another round of the outer loop to get to the next yield return.
				if (yieldBuffer.TryDequeue(out var result)) {
					progress = true;
					yield return result;
				}
				try {
					// if we made no progress at all in this iteration and the yieldBuffer is not full, await unfinished Task from mapBuffer if available.
					// This is the point where suspension for waiting for results happens.
					if (!progress && yieldBuffer.Count < bufferSize && mapBuffer.TryDequeue(out var dequeuedTask)) yieldBuffer.Enqueue(await dequeuedTask);
				}
				catch (Exception ex) {
					exceptionList.Add(ex);
					break;
				}
			}
			// If we left the main loop due to an exception:
			if (exceptionList.Count > 0) {
				// Drain yieldBuffer of results finished before the exception happened:
				while (yieldBuffer.Count > 0) {
					yield return yieldBuffer.Dequeue();
				}
				// Drain mapBuffer and collect other exceptions:
				while (mapBuffer.Count > 0) {
					try {
						// The results of this are not yielded to not violate ordering
						await mapBuffer.Dequeue();
					}
					catch (Exception ex) {
						exceptionList.Add(ex);
					}
				}
				if (exceptionList.Count == 1) {
					throw exceptionList.First();
				}
				else if (exceptionList.All(ex => ex is OperationCanceledException)) {
					throw new OperationCanceledException("At least one map task was canceled.", exceptionList.First());
				}
				else {
					throw new AggregateException("Multiple map tasks threw an exception.", exceptionList);
				}
			}
		}
		/// <summary>
		/// Provides the asynchronous counterpart of <see cref="Enumerable.ToList{TSource}(IEnumerable{TSource})"/> by collecting all elements from the
		/// <c><![CDATA[IAsyncEnumerable<TResult> source]]></c> into a <see cref="List{T}"/> and finishing when all elements have been collected.
		/// </summary>
		/// <typeparam name="T">The type of the elements.</typeparam>
		/// <param name="source">The async enumerable to collect.</param>
		/// <param name="ct">A cancellation token to cancel the collection process.</param>
		/// <returns>A <see cref="Task{TResult}"/> representing the resulting list, allowing calling code to wait for the completion of the collection process.</returns>
		public static async Task<IList<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default) {
			var list = new List<T>();
			await foreach (var val in source.WithCancellation(ct)) {
				list.Add(val);
			}
			return list;
		}
		/// <summary>
		/// Behaves similarly to SingleOrDefault known from Linq, but does not throw an exception if source has more than one element, but instead returns the default value as well, like for the empty source case.
		/// </summary>
		/// <typeparam name="TSource">The type of the elements of the source.</typeparam>
		/// <param name="source">A sequence of values</param>
		/// <returns>The element of source if source has exactly one element, default(TSource) otherwise.</returns>
		public static TSource? SingleOrDefaultNoExcept<TSource>(this IEnumerable<TSource> source) {
			var enumerator = source.GetEnumerator();
			if (!enumerator.MoveNext()) return default;
			var value = enumerator.Current;
			if (enumerator.MoveNext()) return default;
			return value;
		}

		/// <summary>
		/// Takes a key-value-pair collection and generates a new dictionary with the same keys as the input and values that are projected from the original values.
		/// </summary>
		/// <typeparam name="TKey">The type of the keys.</typeparam>
		/// <typeparam name="TInValue">The type of the projected values.</typeparam>
		/// <typeparam name="TOutValue">The type of the original values.</typeparam>
		/// <param name="dict">The input dictionary.</param>
		/// <param name="projection">The projection function to apply to the input values.</param>
		/// <returns>The projected dictionary.</returns>
		public static Dictionary<TKey, TOutValue> ToProjectedDictionary<TKey, TInValue, TOutValue>(this IEnumerable<KeyValuePair<TKey, TInValue>> dict, Func<TInValue, TOutValue> projection) where TKey : notnull {
			return dict.ToDictionary(kvp => kvp.Key, kvp => projection(kvp.Value));
		}

		/// <summary>
		/// Provides a counter-part for <see cref="Enumerable.GroupJoin{TOuter, TInner, TKey, TResult}(IEnumerable{TOuter}, IEnumerable{TInner}, Func{TOuter, TKey}, Func{TInner, TKey},
		/// Func{TOuter, IEnumerable{TInner}, TResult}, IEqualityComparer{TKey}?)"/> that does not only include the matching entries and the unmatched entries from <paramref name="leftIn"/>,
		/// but also the unmatched entries from <paramref name="rightIn"/>.
		/// Thus, it finds all keys from both inputs (projected using <paramref name="leftKey"/> and <paramref name="rightKey"/>) and returns the results of invoking <paramref name="resultSelector"/>
		/// with each distinct key, passing the corresponding entries from <paramref name="leftIn"/> and <paramref name="rightIn"/> with that key.
		/// </summary>
		/// <param name="leftIn">The left input sequence.</param>
		/// <param name="rightIn">The right input sequence.</param>
		/// <param name="leftKey">The projection function to get the key of the elements from <paramref name="leftIn"/>.</param>
		/// <param name="rightKey">The projection function to get the key of the elements from <paramref name="rightIn"/>.</param>
		/// <param name="resultSelector">
		/// A function to produce the output elements.
		/// It is invoked once for each key value appearing in <paramref name="leftIn"/>, in <paramref name="rightIn"/>, or in both.
		/// A collection of all elements from <paramref name="leftIn"/> with the key is passed in the second argument and
		/// one with all elements from <paramref name="rightIn"/> with the key is passed in the third argument.
		/// </param>
		/// <param name="keyComparer">The comparer used to compare the projected keys.</param>
		/// <returns>The elements generated by <paramref name="resultSelector"/>.</returns>
		public static IEnumerable<TResult> FullGroupJoin<TLeft, TRight, TKey, TResult>(this IEnumerable<TLeft> leftIn, IEnumerable<TRight> rightIn,
				Func<TLeft, TKey> leftKey, Func<TRight, TKey> rightKey, Func<TKey, IEnumerable<TLeft>, IEnumerable<TRight>, TResult> resultSelector, IEqualityComparer<TKey>? keyComparer = null) {
			keyComparer ??= EqualityComparer<TKey>.Default;
			var left = leftIn.ToLookup(leftKey, keyComparer);
			var right = rightIn.ToLookup(rightKey, keyComparer);
			var keys = leftIn.Select(leftKey).Concat(rightIn.Select(rightKey).Where(k => !left.Contains(k))).Distinct(keyComparer);
			return keys.Select(k => resultSelector(k, left[k], right[k]));
		}
	}
}
