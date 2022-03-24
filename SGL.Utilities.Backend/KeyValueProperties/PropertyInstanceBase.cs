using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public class PropertyInstanceBase<TInstanceOwner, TInstanceOwnerId, TDefinition> where TInstanceOwner : class where TInstanceOwnerId : struct where TDefinition : PropertyDefinitionBase {
		private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions { Converters = { new ObjectDictionaryValueJsonConverter() } };

		/// <summary>
		/// The unique database id of the property instance.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// The id of the propery definition that this instance instantiates.
		/// </summary>
		public Guid DefinitionId { get; set; }
		/// <summary>
		/// The id of the user to which this instance belongs.
		/// </summary>
		public Guid UserId { get; set; }
		/// <summary>
		/// The propery definition that this instance instantiates.
		/// </summary>
		public TDefinition Definition { get; set; } = null!;
		/// <summary>
		/// The user to which this instance belongs.
		/// </summary>
		public TInstanceOwner Owner { get; set; } = null!;
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="PropertyType.Integer"/>-typed.
		/// </summary>
		public int? IntegerValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="PropertyType.FloatingPoint"/>-typed.
		/// </summary>
		public double? FloatingPointValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="PropertyType.String"/>-typed.
		/// </summary>
		public string? StringValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="PropertyType.DateTime"/>-typed.
		/// </summary>
		public DateTime? DateTimeValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="PropertyType.Guid"/>-typed.
		/// </summary>
		public Guid? GuidValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains a string representation of the value of the property instance if it is <see cref="PropertyType.Json"/>-typed.
		/// </summary>
		public string? JsonValue { get; set; }

		/// <summary>
		/// Indicates whether the value represents an empty state.
		/// </summary>
		public bool IsNull() => Definition.Type switch {
			PropertyType.Integer => IntegerValue is null,
			PropertyType.FloatingPoint => FloatingPointValue is null,
			PropertyType.String => StringValue is null,
			PropertyType.DateTime => DateTimeValue is null,
			PropertyType.Guid => GuidValue is null,
			PropertyType.Json => string.IsNullOrWhiteSpace(JsonValue) || JsonValue == "null",
			_ => throw new PropertyWithUnknownTypeException(Definition.Name, Definition.Type.ToString())
		};

		/// <summary>
		/// Provides access to the typed value of the property.
		/// Get access for a primitively typed property returns the value from the ...<c>Value</c> property associated with the type indicated by <see cref="PropertyDefinitionBase.Type"/> of <see cref="Definition"/>.
		/// Get access for a JSON-typed property deserializes the JSON string representation (stored in <see cref="JsonValue"/>) and returns the deserialized object.
		/// Set access for a primitively typed property sets the value of the ...<c>Value</c> property associated with the type indicated by <see cref="PropertyDefinitionBase.Type"/> of <see cref="Definition"/>.
		/// Set access for a JSON-typed property serializes the value into a JSON string representation and stores it in <see cref="JsonValue"/>.
		/// JSON (de-)serialization is done using <see cref="ObjectDictionaryValueJsonConverter"/>.
		/// </summary>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyTypeDoesntMatchDefinitionException">The type of the value given to a set operation doesn't match the data type specified in the property definition.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		[NotMapped]
		public object? Value {
			get => Definition.Type switch {
				PropertyType.Integer => IntegerValue,
				PropertyType.FloatingPoint => FloatingPointValue,
				PropertyType.String => StringValue,
				PropertyType.DateTime => DateTimeValue,
				PropertyType.Guid => GuidValue,
				PropertyType.Json => JsonSerializer.Deserialize<object?>(JsonValue ?? "null", jsonOptions),
				_ => throw new PropertyWithUnknownTypeException(Definition.Name, Definition.Type.ToString())
			} ?? (Definition.Required ? throw new RequiredPropertyNullException(Definition.Name) : null);
			set {
				switch (value) {
					case null when Definition.Required:
						throw new RequiredPropertyNullException(Definition.Name);
					case null:
						IntegerValue = null;
						FloatingPointValue = null;
						StringValue = null;
						DateTimeValue = null;
						GuidValue = null;
						JsonValue = null;
						break;
					case int intVal when Definition.Type == PropertyType.Integer:
						IntegerValue = intVal;
						break;
					case double fpVal when Definition.Type == PropertyType.FloatingPoint:
						FloatingPointValue = fpVal;
						break;
					case string strVal when Definition.Type == PropertyType.String:
						StringValue = strVal;
						break;
					case DateTime dtVal when Definition.Type == PropertyType.DateTime:
						DateTimeValue = dtVal;
						break;
					case Guid guidVal when Definition.Type == PropertyType.Guid:
						GuidValue = guidVal;
						break;
					case object when Definition.Type == PropertyType.Json:
						JsonValue = JsonSerializer.Serialize<object?>(value, jsonOptions);
						break;
					default:
						throw new PropertyTypeDoesntMatchDefinitionException(Definition.Name, value.GetType(), Definition.Type);
				}
			}
		}

	}
}
