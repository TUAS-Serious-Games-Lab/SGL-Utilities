using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public static class ModelBuilderExtensions {
		public static ModelBuilder KeyValuePropertiesBetween<TDefinitionOwner, TInstanceOwner, TDefinition, TInstance>(this ModelBuilder modelBuilder,
				EntityTypeBuilder<TDefinitionOwner> definitionOwner, EntityTypeBuilder<TInstanceOwner> instanceOwner,
				Expression<Func<TDefinitionOwner, IEnumerable<TDefinition>>> definitionExpression, Expression<Func<TInstanceOwner, IEnumerable<TInstance>>> instanceExpression, int nameLength = 128)
				where TDefinitionOwner : class where TInstanceOwner : class where TDefinition : PropertyDefinitionBase<TDefinitionOwner> where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> {
			modelBuilder.Ignore<PropertyDefinitionBase>();
			modelBuilder.Ignore<PropertyDefinitionBase<TDefinitionOwner>>();
			modelBuilder.Ignore<PropertyInstanceBase<TInstanceOwner, TDefinition>>();
			definitionOwner.OwnsMany(definitionExpression, def => {
				var ownership = def.WithOwner(d => d.Owner);
				var uniqueOwnerAndNameIndexPropNames = ownership.Metadata.Properties.Select(p => p.Name).Append(nameof(PropertyDefinitionBase.Name)).ToArray();
				def.HasIndex(uniqueOwnerAndNameIndexPropNames).IsUnique();
				def.Property(d => d.Name).HasMaxLength(nameLength);
			});
			instanceOwner.OwnsMany(instanceExpression, inst => {
				var ownership = inst.WithOwner(pi => pi.Owner);
				var defFK = inst.HasOne(pi => pi.Definition).WithMany();
				var uniqueDefAndOwnerIndexPropNames = defFK.Metadata.Properties.Concat(ownership.Metadata.Properties).Select(p => p.Name).ToArray();
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
