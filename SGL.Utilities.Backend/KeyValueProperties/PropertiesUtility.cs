using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public static class PropertiesUtility {
		public static void ValidateProperties<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, Func<TInstanceOwner, IEnumerable<TInstance>> instancesGetter,
				Func<TInstanceOwner, IEnumerable<TDefinition>> definitionsGetter)
				where TInstanceOwner : class where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TDefinition : PropertyDefinitionBase {
			var definitions = definitionsGetter(instanceOwner).ToList();
			var requiredDefinitions = definitions.Where(p => p.Required).ToList();
			var instances = instancesGetter(instanceOwner);
			// TODO: If this method becomes a bottleneck, maybe use temporary Dictionaries / Sets to avoid O(n^2) runtime.
			// However, the involved 'n's should be quite low and this happens in-memory, just before we access the database, which should dwarf this overhead.
			foreach (var propInst in instances) {
				if (!definitions.Any(pd => pd.Id == propInst.DefinitionId)) {
					throw new UndefinedPropertyException(propInst.Definition.Name);
				}
				if (instances.Count(p => p.Definition.Name == propInst.Definition.Name) > 1) {
					throw new ConflictingPropertyInstanceException(propInst.Definition.Name);
				}
			}
			foreach (var propDef in requiredDefinitions) {
				var propInst = instances.SingleOrDefault(pi => pi.DefinitionId == propDef.Id);
				if (propInst == null) {
					throw new RequiredPropertyMissingException(propDef.Name);
				}
				else if (propInst.IsNull()) {
					throw new RequiredPropertyNullException(propDef.Name);
				}
			}
		}
		/// <summary>
		/// Returns the value of the application-specific property with the given name for the owning entity.
		/// </summary>
		/// <param name="instanceOwner">The owning entity, holding the property instance.</param>
		/// <param name="name">The name of the property for which to get the value.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <returns>The value of the property.</returns>
		/// <exception cref="PropertyNotFoundException">No property definition with the given name was found for the owning entity.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		public static object? GetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, string name, Func<TInstanceOwner, ICollection<TInstance>> getPropInsts)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			var propInst = getPropInsts(instanceOwner).SingleOrDefault(p => p.Definition.Name == name);
			if (propInst is null) {
				throw new PropertyNotFoundException(name);
			}
			return propInst.Value;
		}

		/// <summary>
		/// Returns the value of the application-specific property represented by the given definition for the owning entity.
		/// Compared to <see cref="GetKeyValueProperty{TInstanceOwner, TInstance, TDefinition}(TInstanceOwner, string, Func{TInstanceOwner, ICollection{TInstance}})"/>,
		/// this overload can avoid the cost of looking up the property definition.
		/// </summary>
		/// <param name="instanceOwner">The owning entity, holding the property instance.</param>
		/// <param name="propDef">The definition of the property to get.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <returns>The value of the property for the owning entity.</returns>
		/// <exception cref="PropertyNotFoundException">No property definition with the given name was found for the owning entity.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		public static object? GetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, TDefinition propDef, Func<TInstanceOwner, ICollection<TInstance>> getPropInsts)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			return GetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(instanceOwner, propDef.Name, getPropInsts);
		}

	}
}
