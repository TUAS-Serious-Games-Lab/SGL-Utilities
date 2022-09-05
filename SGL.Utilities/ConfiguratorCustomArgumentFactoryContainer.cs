using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities {
	/// <summary>
	/// Implements a container for factories (mapped by target type) for custom arguments of factories in a configurator for a client facade.
	/// </summary>
	/// <typeparam name="TArgs">A type for encapsulating factory arguments.</typeparam>
	public class ConfiguratorCustomArgumentFactoryContainer<TArgs> : IDisposable {
		private Dictionary<Type, (Func<TArgs, object> Factory, bool CacheResult)> argFactories =
			new Dictionary<Type, (Func<TArgs, object> Factory, bool CacheResult)>();
		private Dictionary<Type, object> cache = new Dictionary<Type, object>();

		/// <inheritdoc/>
		public void Dispose() {
			foreach (var disposable in cache.Values.OfType<IDisposable>()) {
				disposable.Dispose();
			}
		}

		/// <summary>
		/// Obtains a stored custom argument of type <typeparamref name="T"/> by either using a cached value from a previous call or by invoking the stored factory function.
		/// </summary>
		/// <typeparam name="T">The type of object to obtain.</typeparam>
		/// <param name="argsForArgFactory">The arguments to pass to the factory function for <typeparamref name="T"/> if it needs to be invoked.</param>
		/// <returns>An object of type <typeparamref name="T"/> or null if no factory function for it is registered.</returns>
		public T? GetCustomArgument<T>(TArgs argsForArgFactory) where T : class {
			if (cache.TryGetValue(typeof(T), out var cachedValue)) {
				return (T)cachedValue;
			}
			else if (argFactories.TryGetValue(typeof(T), out var factoryEntry)) {
				var obj = (T)factoryEntry.Factory(argsForArgFactory);
				if (factoryEntry.CacheResult) {
					cache[typeof(T)] = obj;
				}
				return obj;
			}
			else {
				return null;
			}
		}
		/// <summary>
		/// Sets the factory function for type <typeparamref name="T"/> to <paramref name="factory"/> and specifies whether its results shall be cached.
		/// </summary>
		/// <typeparam name="T">The type for which to set the factory function.</typeparam>
		/// <param name="factory">The factory function to use for <typeparamref name="T"/>.</param>
		/// <param name="cacheResult">Indicates whether the results of <paramref name="factory"/> shall be cached.</param>
		public void SetCustomArgumentFactory<T>(Func<TArgs, T> factory, bool cacheResult = false) where T : class {
			cache.Remove(typeof(T));
			argFactories[typeof(T)] = (factory, cacheResult);
		}
	}

	/// <summary>
	/// Implements a container for factories (mapped by target type) for custom arguments of factories in a configurator for a client facade.
	/// </summary>
	/// <typeparam name="TBaseArgs">A type for encapsulating basic factory arguments.</typeparam>
	/// <typeparam name="TAuthenticatedArgs">A type for encapsulating arguments for factories that require an authenticated user.</typeparam>
	public class ConfiguratorCustomArgumentFactoryContainer<TBaseArgs, TAuthenticatedArgs> : ConfiguratorCustomArgumentFactoryContainer<TBaseArgs> where TAuthenticatedArgs : class, TBaseArgs {
		/// <summary>
		/// Sets the factory function for type <typeparamref name="T"/> to <paramref name="factory"/>, which requires an authenticated user session and thus needs <typeparamref name="TAuthenticatedArgs"/> arguments, and specifies whether its results shall be cached.
		/// </summary>
		/// <typeparam name="T">The type for which to set the factory function.</typeparam>
		/// <param name="factory">The factory function to use for <typeparamref name="T"/>.</param>
		/// <param name="cacheResult">Indicates whether the results of <paramref name="factory"/> shall be cached.</param>
		public void SetCustomArgumentFactory<T>(Func<TAuthenticatedArgs, T> factory, bool cacheResult = false) where T : class {
			base.SetCustomArgumentFactory(args => factory(args as TAuthenticatedArgs ??
				throw new InvalidOperationException("Attempt to call an authenticated custom argument factory from an unauthenticated context.")), cacheResult);
		}
	}
}
