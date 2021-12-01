using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.PrometheusNet {

	public enum DisappearedMetricsLabelPolicy {
		Remove, Unpublish, SetZero, Ignore
	}

	public static class MetricsExtensions {

		private static Action<Gauge.Child>? getDisappearedLabelFunc(DisappearedMetricsLabelPolicy disappearedLabelPolicy) => disappearedLabelPolicy switch {
			DisappearedMetricsLabelPolicy.Remove => g => g.Remove(),
			DisappearedMetricsLabelPolicy.Unpublish => g => g.Unpublish(),
			DisappearedMetricsLabelPolicy.SetZero => g => g.Set(0),
			_ => null
		};

		public static void UpdateLabeledValues<TValue>(this Gauge gauge, IDictionary<string, TValue> currentValues, Func<TValue, double> convertValue, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			foreach (var entry in currentValues) {
				if (entry.Key == null || entry.Value == null) continue;
				gauge.WithLabels(entry.Key).Set(convertValue(entry.Value));
			}
			Action<Gauge.Child>? handleDisappearedLabel = getDisappearedLabelFunc(disappearedLabelPolicy);
			if (handleDisappearedLabel == null) return;
			var disappeared_app_labels = gauge.GetAllLabelValues().Select(e => e.SingleOrDefault()).Where(label => label != null && !currentValues.ContainsKey(label));
			foreach (var label in disappeared_app_labels) {
				if (label == null) continue;
				handleDisappearedLabel(gauge.WithLabels(label));
			}
		}
		public static void UpdateLabeledValues<TValue>(this Gauge gauge, IDictionary<string[], TValue> currentValues, Func<TValue, double> convertValue, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			foreach (var entry in currentValues) {
				if (entry.Key == null || entry.Value == null) continue;
				gauge.WithLabels(entry.Key).Set(convertValue(entry.Value));
			}
			Action<Gauge.Child>? handleDisappearedLabel = getDisappearedLabelFunc(disappearedLabelPolicy);
			if (handleDisappearedLabel == null) return;
			var disappeared_app_labels = gauge.GetAllLabelValues().Where(label => !currentValues.ContainsKey(label));
			foreach (var label in disappeared_app_labels) {
				if (label == null) continue;
				handleDisappearedLabel(gauge.WithLabels(label));
			}
		}
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string, int> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string, long> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string, double> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string[], int> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string[], long> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
		public static void UpdateLabeledValues(this Gauge gauge, IDictionary<string[], double> currentValues, DisappearedMetricsLabelPolicy disappearedLabelPolicy = DisappearedMetricsLabelPolicy.Remove) {
			gauge.UpdateLabeledValues(currentValues, v => v, disappearedLabelPolicy);
		}
	}
}
