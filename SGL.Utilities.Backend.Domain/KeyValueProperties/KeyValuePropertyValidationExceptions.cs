using System;

namespace SGL.Utilities.Backend.Domain.KeyValueProperties {
	/// <summary>
	/// The base class for exceptions thrown when an invalid property is encountered with key-value properties
	/// represented by <see cref="PropertyDefinitionBase{TDefinitionOwner}"/> and <see cref="PropertyInstanceBase{TInstanceOwner, TDefinition}"/>.
	/// </summary>
	public abstract class KeyValuePropertyValidationException : Exception {
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		protected KeyValuePropertyValidationException(string? message, Exception? innerException) : base(message, innerException) { }

		/// <summary>
		/// The name of the property that violates a validation condition.
		/// </summary>
		public abstract string InvalidPropertyName { get; }
	}

	/// <summary>
	/// The base class for exceptions thrown when property access or validation fails because a property definition or instance collection is missing.
	/// </summary>
	public abstract class PropertyCollectionMissingException : Exception {
		/// <summary>
		/// Creates a new exception object with the given mesage and inner exception.
		/// </summary>
		protected PropertyCollectionMissingException(string? message, Exception? innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// The exception type thrown when property access or validation fails because a property definition collection is missing.
	/// </summary>
	public class PropertyDefinitionsCollectionMissing : PropertyCollectionMissingException {
		/// <summary>
		/// Creates a new exception object with the given inner exception.
		/// </summary>
		public PropertyDefinitionsCollectionMissing(Exception? innerException = null) :
			base("Could not validate / set properties because the definitions collection is missing.", innerException) { }
	}
	/// <summary>
	/// The exception type thrown when property access or validation fails because a property instance collection is missing.
	/// </summary>
	public class PropertyInstancesCollectionMissing : PropertyCollectionMissingException {
		/// <summary>
		/// Creates a new exception object with the given inner exception.
		/// </summary>
		public PropertyInstancesCollectionMissing(Exception? innerException = null) :
			base("Could not validate / set / get properties because the instances collection is missing.", innerException) { }
	}

	/// <summary>
	/// The exception type thrown when an empty value is given for a required property.
	/// </summary>
	public class RequiredPropertyNullException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the required property that has an empty value.
		/// </summary>
		public string NullPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="NullPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => NullPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public RequiredPropertyNullException(string nullPropertyName, Exception? innerException = null) :
			base($"The property {nullPropertyName} is required but has a null value.", innerException) {
			NullPropertyName = nullPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when there is no property instance on instance-owning entity for a required property definition of the definition-owning entity.
	/// </summary>
	public class RequiredPropertyMissingException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the propery definition for which the instance is missing.
		/// </summary>
		public string MissingPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="MissingPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => MissingPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public RequiredPropertyMissingException(string missingPropertyName, Exception? innerException = null) :
			base($"The required property {missingPropertyName} is not present.", innerException) {
			MissingPropertyName = missingPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to create a property instance for a property name where no corresponding property definition is present.
	/// </summary>
	public class UndefinedPropertyException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the undefined property.
		/// </summary>
		public string UndefinedPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="UndefinedPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => UndefinedPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public UndefinedPropertyException(string undefinedPropertyName, Exception? innerException = null) :
			base($"The property {undefinedPropertyName} is present but not defined.", innerException) {
			UndefinedPropertyName = undefinedPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when a value of a type is given for a property that doesn't match the type specified in the property definition.
	/// </summary>
	public class PropertyTypeDoesntMatchDefinitionException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the property with for which a non-matching value was given.
		/// </summary>
		public override string InvalidPropertyName { get; }
		/// <summary>
		/// The type of the supplied value.
		/// </summary>
		public Type ValueType { get; }
		/// <summary>
		/// The type specified in the property definition.
		/// </summary>
		public PropertyType DefinitionType { get; }

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public PropertyTypeDoesntMatchDefinitionException(string invalidPropertyName, Type valueType, PropertyType definitionType, Exception? innerException = null) :
			base($"The value of type {valueType.Name} that was given for the property {invalidPropertyName} is not compatible with the type {definitionType.ToString()} as which the property is defined.", innerException) {
			InvalidPropertyName = invalidPropertyName;
			ValueType = valueType;
			DefinitionType = definitionType;
		}
	}

	/// <summary>
	/// The exception type thrown when a property definition with an unknown type is encountered in the data layer.
	/// </summary>
	public class PropertyWithUnknownTypeException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the property definition with the unknown type.
		/// </summary>
		public override string InvalidPropertyName { get; }
		/// <summary>
		/// The unknown type specification.
		/// </summary>
		public string UnknownType { get; }
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public PropertyWithUnknownTypeException(string invalidPropertyName, string unknownType, Exception? innerException = null) :
			base($"The property {invalidPropertyName} the unknown property type {unknownType}.", innerException) {
			InvalidPropertyName = invalidPropertyName;
			UnknownType = unknownType;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to add a property definition with a name that is already in use by another property definition on the owning entity.
	/// </summary>
	public class ConflictingPropertyNameException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name that conflicts between two definitions.
		/// </summary>
		public string ConflictingPropertyName { get; }

		/// <summary>
		/// Returns <see cref="ConflictingPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => ConflictingPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ConflictingPropertyNameException(string conflictingPropertyName, Exception? innerException = null) :
			base($"The property name {conflictingPropertyName} is already in use by another property on the same entity.", innerException) {
			ConflictingPropertyName = conflictingPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to lookup a property instance (by the name of its definition) that doesn't exist for the owning entities.
	/// </summary>
	public class PropertyNotFoundException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the property that wasn't found.
		/// </summary>
		public string MissingPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="MissingPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => MissingPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public PropertyNotFoundException(string missingPropertyName, Exception? innerException = null) :
			base($"The requested property {missingPropertyName} is not present.", innerException) {
			MissingPropertyName = missingPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to create multiple instances of the same property definition for the same entity.
	/// </summary>
	public class ConflictingPropertyInstanceException : KeyValuePropertyValidationException {
		/// <summary>
		/// The name of the duplicate property.
		/// </summary>
		public string PropertyName { get; }

		/// <summary>
		/// Returns <see cref="PropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => PropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ConflictingPropertyInstanceException(string propertyName, Exception? innerException = null) :
			base($"There is more than one instance of the property {propertyName} present for the same user registration.", innerException) {
			PropertyName = propertyName;
		}
	}

}
