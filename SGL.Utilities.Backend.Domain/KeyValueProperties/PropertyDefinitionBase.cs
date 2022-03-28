using System;

namespace SGL.Utilities.Backend.Domain.KeyValueProperties {
	/// <summary>
	/// A base class for <see cref="PropertyDefinitionBase{TDefinitionOwner}"/> containing only the data that is neutral to the owning entity type.
	/// </summary>
	public class PropertyDefinitionBase {
		/// <summary>
		/// The unique database id of the property definition.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// The name of the property, must be unique within the owning entity.
		/// </summary>
		public string Name { get; set; } = "";
		/// <summary>
		/// The data type of the property.
		/// </summary>
		public PropertyType Type { get; set; }
		/// <summary>
		/// Whether the property is required, otherwise it is optional.
		/// </summary>
		public bool Required { get; set; }
	}

	/// <summary>
	/// A generic base class for weak entity types that represent key-value property definitions.
	/// The derived classes are usually only needed to make the weak entity types typesafe and give the weak entities a proper name for the data layer.
	/// Thus, the derived classes usually don't need their own functionality.
	/// </summary>
	/// <typeparam name="TDefinitionOwner">The entity type that holds these property definitions.</typeparam>
	public class PropertyDefinitionBase<TDefinitionOwner> : PropertyDefinitionBase where TDefinitionOwner : class {
		/// <summary>
		/// The owning entity for which this property is defined.
		/// </summary>
		/// <remarks>
		/// The id is stored as a shadow property.
		/// </remarks>
		public TDefinitionOwner Owner { get; set; } = null!;

		/// <summary>
		/// Creates a property definition for with the given name, type, and required flag for the given entity.
		/// </summary>
		/// <returns>The property definition object.</returns>
		public static TDefinition Create<TDefinition>(TDefinitionOwner owner, string name, PropertyType type, bool required)
				where TDefinition : PropertyDefinitionBase<TDefinitionOwner>, new() =>
			new TDefinition {
				Id = Guid.NewGuid(),
				Owner = owner,
				Name = name,
				Type = type,
				Required = required
			};
	}
}
