using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public static class DbContextExtensions {
		public static Task LoadKeyValuePropertiesAsync<TInstanceOwner, TPropInstance, TDefinition>(this DbContext context, TInstanceOwner owner,
				Expression<Func<TInstanceOwner, IEnumerable<TPropInstance>>> propertyInstancesExpression)
				where TInstanceOwner : class
				where TDefinition : PropertyDefinitionBase
				where TPropInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> {
			return context.Entry(owner).Collection(propertyInstancesExpression).LoadAsync();
		}
		public static Task LoadKeyValuePropertiesAsync<TDefinitionOwner, TDefinition>(this DbContext context, TDefinitionOwner owner,
				Expression<Func<TDefinitionOwner, IEnumerable<TDefinition>>> propertyDefinitionsExpression)
				where TDefinitionOwner : class
				where TDefinition : PropertyDefinitionBase<TDefinitionOwner> {
			return context.Entry(owner).Collection(propertyDefinitionsExpression).LoadAsync();
		}
	}
}
