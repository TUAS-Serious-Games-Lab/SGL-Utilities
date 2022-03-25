using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	/// <summary>
	/// Provides the extension methods <see cref="LoadKeyValuePropertiesAsync{TDefinitionOwner, TDefinition}(DbContext, TDefinitionOwner, Expression{Func{TDefinitionOwner, IEnumerable{TDefinition}}})"/> and
	/// <see cref="LoadKeyValuePropertiesAsync{TInstanceOwner, TPropInstance, TDefinition}(DbContext, TInstanceOwner, Expression{Func{TInstanceOwner, IEnumerable{TPropInstance}}})"/>.
	/// </summary>
	public static class DbContextExtensions {
		/// <summary>
		/// Asynchronously explicitly loads the property instances of a property-owning entity and their associated definitions.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TPropInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="context">The <see cref="DbContext"/> through which to load the data.</param>
		/// <param name="owner">The property-owning entity for which to load the properties.</param>
		/// <param name="propertyInstancesExpression">The expression identifying the collection to load within the owning entity.</param>
		/// <returns>A Task object representing the asynchronous operation.</returns>
		public static Task LoadKeyValuePropertiesAsync<TInstanceOwner, TPropInstance, TDefinition>(this DbContext context, TInstanceOwner owner,
				Expression<Func<TInstanceOwner, IEnumerable<TPropInstance>>> propertyInstancesExpression)
				where TInstanceOwner : class
				where TDefinition : PropertyDefinitionBase
				where TPropInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> {
			return context.Entry(owner).Collection(propertyInstancesExpression).LoadAsync();
		}
		/// <summary>
		/// Asynchronously explicitly loads the property definitions of a property-owning entity.
		/// </summary>
		/// <typeparam name="TDefinitionOwner">The definition-owning entity type.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="context">The <see cref="DbContext"/> through which to load the data.</param>
		/// <param name="owner">The property-owning entity for which to load the properties.</param>
		/// <param name="propertyDefinitionsExpression">The expression identifying the collection to load within the owning entity.</param>
		/// <returns>A Task object representing the asynchronous operation.</returns>
		public static Task LoadKeyValuePropertiesAsync<TDefinitionOwner, TDefinition>(this DbContext context, TDefinitionOwner owner,
				Expression<Func<TDefinitionOwner, IEnumerable<TDefinition>>> propertyDefinitionsExpression)
				where TDefinitionOwner : class
				where TDefinition : PropertyDefinitionBase<TDefinitionOwner> {
			return context.Entry(owner).Collection(propertyDefinitionsExpression).LoadAsync();
		}
	}
}
