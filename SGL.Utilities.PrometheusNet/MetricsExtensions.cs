using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.PrometheusNet {

	/// <summary>
	/// Specifies the policy for what methods like <see cref="MetricsExtensions.UpdateLabeledValues{TValue}(Gauge, IDictionary{string[], TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/>
	/// that update metric values from a map of current values should do when the metric object contains labels for value entries that are not present in the current map.
	/// </summary>
	public enum DisappearedMetricsLabelPolicy {
		/// <summary>
		/// Indicates that the updating method should remove the existing labeled metric child from the metric object.
		/// </summary>
		Remove,
		/// <summary>
		/// Indicates that the updating method should unpublish the existing labeled metric child in the metric object.
		/// This keeps the child object in place, but removes it from the exported list of metric values.
		/// </summary>
		Unpublish,
		/// <summary>
		/// Indicates that the updating method should set the existing metric childs's value to 0.
		/// This is useful for gauge metrics that meassure a count of some entries associated with the label keys where a key disappears from the map if the entry count goes to zero.
		/// </summary>
		SetZero,
		/// <summary>
		/// Ignores the disappearance and keeps the stale label present.
		/// </summary>
		Ignore
	}

	/// <summary>
	/// Provides extension methods to reduce code duplication when working with prometheus metrics, especially application-level metrics.
	/// </summary>
	public static class MetricsExtensions {

		private static Action<Gauge.Child>? getDisappearedGaugeLabelFunc(DisappearedMetricsLabelPolicy disappearedLabelPolicy) => disappearedLabelPolicy switch {
			DisappearedMetricsLabelPolicy.Remove => g => g.Remove(),
			DisappearedMetricsLabelPolicy.Unpublish => g => g.Unpublish(),
			DisappearedMetricsLabelPolicy.SetZero => g => g.Set(0),
			_ => null
		};

		/// <summary>
		/// Updates the single-labeled values of <paramref name="gauge"/> with the entries from <paramref name="currentValues"/>, using the key of each entry as the label value and the value of the entry as the new value of the associated metric child object.
		/// Any value type can be used for the map itself, but it must be convertible to <see cref="double"/> using <paramref name="convertValue"/>.
		/// </summary>
		/// <typeparam name="TValue">The value type of <paramref name="currentValues"/>.</typeparam>
		/// <param name="gauge">The gauge parent object to update with the current values.</param>
		/// <param name="currentValues">The dictionary providing a mapping from label values to their associated current metric values.</param>
		/// <param name="convertValue">A function delegate to convert the map values to the actual metric values to set.</param>
		/// <param name="disappearedLabelPolicy">Specifies how gauge child objects with labels that are not present as keys in <paramref name="currentValues"/> should be handled, see <see cref="DisappearedMetricsLabelPolicy"/>.</param>
		/// <example>
		/// Given a gauge object named <c>example</c> with a label named <c>foo</c>, a dictionary <c><![CDATA[new Dictionary<string, int>() { ["bar"] = 123, ["baz"] = 456 }]]></c> updating the gauge using the dictionary and <c>v=>v</c> as <paramref name="convertValue"/>, the following metric values will be exported from the gauge:
		/// <code>
		/// example{foo="bar"} 123
		/// example{foo="baz"} 456
		/// </code>
		/// </example>
		public static void UpdateLabeledValues<TValue>(this Gauge gauge, IDictionary<string, TValue> currentValues, Func<TValue, double> convertValue, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			foreach (var entry in currentValues) {
				if (entry.Key == null || entry.Value == null) continue;
				gauge.WithLabels(entry.Key).Set(convertValue(entry.Value));
			}
			Action<Gauge.Child>? handleDisappearedLabel = getDisappearedGaugeLabelFunc(disappearedLabelPolicy);
			if (handleDisappearedLabel == null) return;
			var disappeared_app_labels = gauge.GetAllLabelValues().Select(e => e.SingleOrDefault()).Where(label => label != null && !currentValues.ContainsKey(label));
			foreach (var label in disappeared_app_labels) {
				if (label == null) continue;
				handleDisappearedLabel(gauge.WithLabels(label));
			}
		}
		/// <summary>
		/// Provides the same functionality as <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string, TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/>, except that arrays of strings are used as the keys, corresponding to multiple labels on <paramref name="gauge"/> to identify a child object.
		/// </summary>
		public static void UpdateLabeledValues<TValue>(this Gauge gauge, IDictionary<string[], TValue> currentValues, Func<TValue, double> convertValue, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			foreach (var entry in currentValues) {
				if (entry.Key == null || entry.Value == null) continue;
				gauge.WithLabels(entry.Key).Set(convertValue(entry.Value));
			}
			Action<Gauge.Child>? handleDisappearedLabel = getDisappearedGaugeLabelFunc(disappearedLabelPolicy);
			if (handleDisappearedLabel == null) return;
			var disappeared_app_labels = gauge.GetAllLabelValues().Where(label => !currentValues.ContainsKey(label));
			foreach (var label in disappeared_app_labels) {
				if (label == null) continue;
				handleDisappearedLabel(gauge.WithLabels(label));
			}
		}
		/// <summary>
		/// Acts as a convenience overload of <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string, TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/> to avoid the need to specify the identity lambda <c>v=>v</c> as the conversion function.
		/// </summary>
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string, int> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		/// <summary>
		/// Acts as a convenience overload of <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string, TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/> to avoid the need to specify the identity lambda <c>v=>v</c> as the conversion function.
		/// </summary>
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string, long> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		/// <summary>
		/// Acts as a convenience overload of <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string, TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/> to avoid the need to specify the identity lambda <c>v=>v</c> as the conversion function.
		/// </summary>
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string, double> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		/// <summary>
		/// Acts as a convenience overload of <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string[], TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/> to avoid the need to specify the identity lambda <c>v=>v</c> as the conversion function.
		/// </summary>
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string[], int> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		/// <summary>
		/// Acts as a convenience overload of <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string[], TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/> to avoid the need to specify the identity lambda <c>v=>v</c> as the conversion function.
		/// </summary>
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string[], long> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		/// <summary>
		/// Acts as a convenience overload of <see cref="UpdateLabeledValues{TValue}(Gauge, IDictionary{string[], TValue}, Func{TValue, double}, DisappearedMetricsLabelPolicy)"/> to avoid the need to specify the identity lambda <c>v=>v</c> as the conversion function.
		/// </summary>
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string[], double> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
	}
}
