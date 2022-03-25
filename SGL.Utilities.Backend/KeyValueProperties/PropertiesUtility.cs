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
	}
}
