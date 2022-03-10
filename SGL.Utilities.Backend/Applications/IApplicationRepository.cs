using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.Applications {

	/// <summary>
	/// Specifies the interface for a repository to store application objects that can be used by services to associate their data with
	/// one of multiple applications using them through a kind of multi-tenant functionality.
	/// </summary>
	public interface IApplicationRepository<TApp, TQueryOptions> where TApp : IApplication where TQueryOptions : class {
		/// <summary>
		/// Asynchronously obtains the application with the given name if it exists.
		/// </summary>
		/// <param name="appName">The unique technical name of the application.</param>
		/// <param name="queryOptions">Allows implementations to get passed options for a query, e.g. to specify which related objects should be loaded.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The application object if the application exists, or <see langword="null"/> otherwise.</returns>
		Task<TApp?> GetApplicationByNameAsync(string appName, TQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously creates the given application object in the repository.
		/// </summary>
		/// <param name="app">The application data for the application to create.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the created object as its result.</returns>
		Task<TApp> AddApplicationAsync(TApp app, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously updates the given application object in the repository.
		/// </summary>
		/// <param name="app">The updated application data.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated object as its result.</returns>
		Task<TApp> UpdateApplicationAsync(TApp app, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously obtains a list of all registered applications.
		/// </summary>
		/// <param name="queryOptions">Allows implementations to get passed options for a query, e.g. to specify which related objects should be loaded.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the list as its result.</returns>
		Task<IList<TApp>> ListApplicationsAsync(TQueryOptions? queryOptions = null, CancellationToken ct = default);
	}
}
