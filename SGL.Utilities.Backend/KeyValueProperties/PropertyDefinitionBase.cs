using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public class PropertyDefinitionBase {
		/// <summary>
		/// The unique database id of the property definition.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// The name of the property, must be unique within the owning entity.
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// The data type of the property.
		/// </summary>
		public PropertyType Type { get; set; }
		/// <summary>
		/// Whether the property is required, otherwise it is optional.
		/// </summary>
		public bool Required { get; set; }
	}

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
		public static PropertyDefinitionBase<TDefinitionOwner> Create(TDefinitionOwner owner, string name, PropertyType type, bool required) =>
			new PropertyDefinitionBase<TDefinitionOwner> {
				Id = Guid.NewGuid(),
				Owner = owner,
				Name = name,
				Type = type,
				Required = required
			};
	}
}
