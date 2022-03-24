using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public class PropertyDefinitionBase {
		/// <summary>
		/// The unique database id of the  property definition.
		/// </summary>
		public Guid Id { get; set; }

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

	public class PropertyDefinitionBase<TDefinitionOwner, TDefinitionOwnerId> : PropertyDefinitionBase where TDefinitionOwnerId : struct where TDefinitionOwner : class {
		/// <summary>
		/// The id of the owning entity for which this property is defined.
		/// </summary>
		public TDefinitionOwnerId OwnerId { get; set; }
		/// <summary>
		/// The owning entity for which this property is defined.
		/// </summary>
		public TDefinitionOwner Owner { get; set; } = null!;
	}
}
