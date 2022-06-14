using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGL.Utilities.Backend.Domain.KeyValueProperties;
using SGL.Utilities.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SGL.Utilities.Backend.KeyValueProperties {
	/// <summary>
	/// Provides the extension method
	/// <see cref="KeyValuePropertiesBetween{TDefinitionOwner, TInstanceOwner, TDefinition, TInstance}(ModelBuilder, EntityTypeBuilder{TDefinitionOwner}, EntityTypeBuilder{TInstanceOwner},
	/// Expression{Func{TDefinitionOwner, IEnumerable{TDefinition}}}, Expression{Func{TInstanceOwner, IEnumerable{TInstance}}}, int)"/>.
	/// </summary>
	public static class ModelBuilderExtensions {
		/// <summary>
		/// Sets up key-value properties between the related entity types <typeparamref name="TDefinitionOwner"/> and <typeparamref name="TInstanceOwner"/> with
		/// <typeparamref name="TDefinitionOwner"/> holding property definitions of type <typeparamref name="TDefinition"/> and
		/// <typeparamref name="TInstanceOwner"/> holding property instances of type <typeparamref name="TInstanceOwner"/>.
		/// </summary>
		/// <typeparam name="TDefinitionOwner">The entity type holding the property definitions.</typeparam>
		/// <typeparam name="TInstanceOwner">The entity type holding the property instances.</typeparam>
		/// <typeparam name="TDefinition">The weak entity type representing a property definition, derived from <see cref="PropertyDefinitionBase{TDefinitionOwner}"/>.</typeparam>
		/// <typeparam name="TInstance">The wead entity type representing a property instance, derived from <see cref="PropertyInstanceBase{TInstanceOwner, TDefinition}"/>.</typeparam>
		/// <param name="modelBuilder">The <see cref="ModelBuilder"/> with which to set up the model for the properties.</param>
		/// <param name="definitionOwner">The entity type builder for the entity holding the property definitions.</param>
		/// <param name="instanceOwner">The entity type builder for the entity holding the property instances.</param>
		/// <param name="definitionExpression">The expression identifying the collection of property definition within <typeparamref name="TDefinitionOwner"/>.</param>
		/// <param name="instanceExpression">The expression identifying the collection of property instances within <typeparamref name="TInstanceOwner"/>.</param>
		/// <param name="nameLength">The maximum length of the property names to set up.</param>
		/// <returns>A reference to <paramref name="modelBuilder"/> for chaining.</returns>
		public static ModelBuilder KeyValuePropertiesBetween<TDefinitionOwner, TInstanceOwner, TDefinition, TInstance>(this ModelBuilder modelBuilder,
				EntityTypeBuilder<TDefinitionOwner> definitionOwner, EntityTypeBuilder<TInstanceOwner> instanceOwner,
				Expression<Func<TDefinitionOwner, IEnumerable<TDefinition>?>> definitionExpression, Expression<Func<TInstanceOwner, IEnumerable<TInstance>?>> instanceExpression, int nameLength = 128)
				where TDefinitionOwner : class where TInstanceOwner : class where TDefinition : PropertyDefinitionBase<TDefinitionOwner> where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> {
			modelBuilder.Ignore<PropertyDefinitionBase>();
			modelBuilder.Ignore<PropertyDefinitionBase<TDefinitionOwner>>();
			modelBuilder.Ignore<PropertyInstanceBase<TInstanceOwner, TDefinition>>();
			var definitionsRel = definitionOwner.HasMany(definitionExpression).WithOne(pd => pd.Owner);
			definitionOwner.Navigation(definitionExpression).AutoInclude(true);
			modelBuilder.Entity<TDefinition>(def => {
				var uniqueOwnerAndNameIndexPropNames = definitionsRel.Metadata.Properties.Select(p => p.Name).Append(nameof(PropertyDefinitionBase.Name)).ToArray();
				def.HasIndex(uniqueOwnerAndNameIndexPropNames).IsUnique();
				def.Property(d => d.Name).HasMaxLength(nameLength);
			});

			var instancesRel = instanceOwner.HasMany(instanceExpression).WithOne(pi => pi.Owner);
			modelBuilder.Entity<TInstance>(inst => {
				var defFK = inst.HasOne(pi => pi.Definition).WithMany();
				var uniqueDefAndOwnerIndexPropNames = defFK.Metadata.Properties.Concat(instancesRel.Metadata.Properties).Select(p => p.Name).ToArray();
				inst.HasIndex(uniqueDefAndOwnerIndexPropNames).IsUnique();
				inst.Property(pi => pi.IntegerValue);
				inst.Property(pi => pi.FloatingPointValue);
				inst.Property(pi => pi.StringValue);
				inst.Property(pi => pi.DateTimeValue).IsStoredInUtc();
				inst.Property(pi => pi.GuidValue);
				inst.Property(pi => pi.JsonValue);
				inst.Ignore(pi => pi.Value);
				// Loading the instance without the definition is not very useful. Thus, enable auto-loading for that reference:
				inst.Navigation(pi => pi.Definition).AutoInclude(true);
			});
			// Don't automatically load key-value-properties to avoid imposing the cost if the properties are not needed:
			definitionOwner.Navigation(definitionExpression).AutoInclude(false);
			instanceOwner.Navigation(instanceExpression).AutoInclude(false);
			return modelBuilder;
		}
	}
}
