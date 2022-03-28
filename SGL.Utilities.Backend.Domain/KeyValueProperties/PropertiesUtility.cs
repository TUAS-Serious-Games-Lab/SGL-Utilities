using System;
using System.Collections.Generic;
using System.Linq;

namespace SGL.Utilities.Backend.Domain.KeyValueProperties {
	/// <summary>
	/// Provides utility methods for working with key-value properties.
	/// </summary>
	public static class PropertiesUtility {
		/// <summary>
		/// Validates the key-value property instances for the instance-owning entity against the defined property definitions of the definition-owning entity with which the instance-owning entity is associated.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The entity, holding the property instances.</param>
		/// <param name="instancesGetter">The delegate to get the collection of property instances from the owning entity.</param>
		/// <param name="definitionsGetter">
		/// The delegate to get the collection of property definitions from the entity that owns the instances.
		/// This is usually implemented by navigating from the instance-owning entity to the definition-owning entity and then obtaining the collection of definitions from there.
		/// </param>
		/// <exception cref="RequiredPropertyMissingException">If no instance was present for a required property.</exception>
		/// <exception cref="RequiredPropertyNullException">If the instance for a required property contained an empty value.</exception>
		/// <exception cref="UndefinedPropertyException">If a property instance references a property instance that is not correctly associated with the definition-owning entity
		/// associated with the instance-owning entity.</exception>
		/// <exception cref="ConflictingPropertyInstanceException">If multiple instances for the same property are present.</exception>
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


		private static TInstance setPropertyImpl<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, string name, object? value,
				Func<TInstanceOwner, ICollection<TInstance>> getPropInsts, Func<string, TDefinition> getPropDef, Func<TDefinition, TInstanceOwner, TInstance> createInst)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			ICollection<TInstance> instances = getPropInsts(instanceOwner);
			var propInst = instances.SingleOrDefault(p => p.Definition.Name == name);
			if (propInst is null) {
				propInst = createInst(getPropDef(name), instanceOwner);
				instances.Add(propInst);
			}
			propInst.Value = value;
			return propInst;
		}

		/// <summary>
		/// Sets the property with the given name for this owner entity to the given value.
		/// This either updates the value of an existing instance object, or, if no instance exists for this property for the entity, creates such an instance with the given value.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The owning entity for which to set the property.</param>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">The value to which the property shall be set.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <param name="getPropDefs">
		/// The delegate to get the collection of property definitions from the entity that owns the instances.
		/// This is usually implemented by navigating from the instance-owning entity to the definition-owning entity and then obtaining the collection of definitions from there.
		/// </param>
		/// <param name="createInst">
		/// The delegate to create a new property instance for a given property definition and owning entity.
		/// It is used when the property doesn't exist yet for the owning entity.
		/// </param>
		/// <returns>The property instance object with the given name for the entity.</returns>
		/// <exception cref="UndefinedPropertyException">A property with the given name is not defined for the definition-owning entity with which the instance-owning entity is associated.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was given for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyTypeDoesntMatchDefinitionException">The type of the value given doesn't match the data type specified in the property definition.</exception>
		public static TInstance SetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, string name, object? value,
				Func<TInstanceOwner, ICollection<TInstance>> getPropInsts, Func<TInstanceOwner, IEnumerable<TDefinition>> getPropDefs, Func<TDefinition, TInstanceOwner, TInstance> createInst)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			return setPropertyImpl(instanceOwner, name, value,
				getPropInsts,
				name => {
					var propDef = getPropDefs(instanceOwner).SingleOrDefault(p => p.Name == name);
					if (propDef is null) {
						throw new UndefinedPropertyException(name);
					}
					return propDef;
				}, createInst);
		}

		/// <summary>
		/// Returns the value of the application-specific property with the given name for the owning entity.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The owning entity, holding the property instance.</param>
		/// <param name="name">The name of the property for which to get the value.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <param name="definitionHint">
		/// Needs to a lambda referencing <see cref="PropertyInstanceBase{TInstanceOwner, TDefinition}"/> within <typeparamref name="TInstanceOwner"/>.
		/// This is just needed as a workaround to make type parameter deduction work.
		/// </param>
		/// <returns>The value of the property.</returns>
		/// <exception cref="PropertyNotFoundException">No property definition with the given name was found for the owning entity.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		public static object? GetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, string name,
			Func<TInstanceOwner, ICollection<TInstance>> getPropInsts, Func<TInstance, TDefinition> definitionHint)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			var propInst = getPropInsts(instanceOwner).SingleOrDefault(p => p.Definition.Name == name);
			if (propInst is null) {
				throw new PropertyNotFoundException(name);
			}
			return propInst.Value;
		}

		/// <summary>
		/// Sets the property represented by the given property definition for this owning entity to the given value.
		/// This either updates the value of an existing instance object, or, if no instance exists for this property for the owning entity, creates such an instance with the given value.
		/// Compared to <see cref="SetKeyValueProperty{TInstanceOwner, TInstance, TDefinition}(TInstanceOwner, string, object?, Func{TInstanceOwner, ICollection{TInstance}},
		/// Func{TInstanceOwner, IEnumerable{TDefinition}}, Func{TDefinition, TInstanceOwner, TInstance})"/>,
		/// this overload can avoid the cost of looking up the property definition and avoids <see cref="UndefinedPropertyException"/> because the property definition is already given.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The owning entity for which to set the property.</param>
		/// <param name="propDef">The definition of the property to set.</param>
		/// <param name="value">The value to which the property shall be set.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <param name="createInst">
		/// The delegate to create a new property instance for a given property definition and owning entity.
		/// It is used when the property doesn't exist yet for the owning entity.
		/// </param>
		/// <returns>The relevant property instance for the user.</returns>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was given for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyTypeDoesntMatchDefinitionException">The type of the value given doesn't match the data type specified in the property definition.</exception>
		public static TInstance SetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, TDefinition propDef, object? value,
				Func<TInstanceOwner, ICollection<TInstance>> getPropInsts, Func<TDefinition, TInstanceOwner, TInstance> createInst)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			return setPropertyImpl(instanceOwner, propDef.Name, value, getPropInsts, name => propDef, createInst);
		}

		/// <summary>
		/// Returns the value of the application-specific property represented by the given definition for the owning entity.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The owning entity, holding the property instance.</param>
		/// <param name="propDef">The definition of the property to get.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <returns>The value of the property for the owning entity.</returns>
		/// <exception cref="PropertyNotFoundException">No property definition with the given name was found for the owning entity.</exception>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="PropertyDefinitionBase.Required"/>.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		public static object? GetKeyValueProperty<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, TDefinition propDef, Func<TInstanceOwner, ICollection<TInstance>> getPropInsts)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			return GetKeyValueProperty(instanceOwner, propDef.Name, getPropInsts, p => p.Definition);
		}

		/// <summary>
		/// Converts a collection of key-value property instances to a dictionary.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The owning entity, holding property instances.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <param name="definitionHint">
		/// Needs to a lambda referencing <see cref="PropertyInstanceBase{TInstanceOwner, TDefinition}"/> within <typeparamref name="TInstanceOwner"/>.
		/// This is just needed as a workaround to make type parameter deduction work.
		/// </param>
		/// <returns>A dictionary containing the names of the provided properties associated with their corresponding values.</returns>
		public static Dictionary<string, object?> ConvertKeyValuePropertiesToDictionary<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner,
			Func<TInstanceOwner, ICollection<TInstance>> getPropInsts, Func<TInstance, TDefinition> definitionHint)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			return getPropInsts(instanceOwner).ToDictionary(p => p.Definition.Name, p => p.Value);
		}

		/// <summary>
		/// Sets the key-value pairs from the given dictionary-like enumerable in the key-value properties of the given property-owning entity.
		/// </summary>
		/// <typeparam name="TInstanceOwner">The instance-owning entity type.</typeparam>
		/// <typeparam name="TInstance">The concrete type of the property instances.</typeparam>
		/// <typeparam name="TDefinition">The concrete type of the property definitions.</typeparam>
		/// <param name="instanceOwner">The owning entity, holding property instances.</param>
		/// <param name="dictionary">The source data to convert.</param>
		/// <param name="getPropInsts">The delegate to get the collection of property instances from the owning entity.</param>
		/// <param name="getPropDefs">
		/// The delegate to get the collection of property definitions from the entity that owns the instances.
		/// This is usually implemented by navigating from the instance-owning entity to the definition-owning entity and then obtaining the collection of definitions from there.
		/// </param>
		/// <param name="createInst">
		/// The delegate to create a new property instance for a given property definition and owning entity.
		/// It is used when the property doesn't exist yet for the owning entity.
		/// </param>
		public static void SetKeyValuePropertiesFromDictionary<TInstanceOwner, TInstance, TDefinition>(TInstanceOwner instanceOwner, IEnumerable<KeyValuePair<string, object?>> dictionary,
				Func<TInstanceOwner, ICollection<TInstance>> getPropInsts, Func<TInstanceOwner, IEnumerable<TDefinition>> getPropDefs, Func<TDefinition, TInstanceOwner, TInstance> createInst)
				where TInstance : PropertyInstanceBase<TInstanceOwner, TDefinition> where TInstanceOwner : class where TDefinition : PropertyDefinitionBase {
			foreach (var dictProp in dictionary) {
				SetKeyValueProperty(instanceOwner, dictProp.Key, dictProp.Value, getPropInsts, getPropDefs, createInst);
			}
		}
	}
}
