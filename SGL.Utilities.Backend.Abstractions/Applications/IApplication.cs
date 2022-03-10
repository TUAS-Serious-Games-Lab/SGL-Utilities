using System;

namespace SGL.Utilities.Backend.Applications {
	/// <summary>
	/// The interface that <see cref="IApplicationRepository{TApp, TQueryOptions}"/> and its implementations require application entity classes that are used for their <c>TApp</c> parameter to implement.
	/// </summary>
	public interface IApplication {
		/// <summary>
		/// A unique id of the application in the database.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// A unique technical name of the application that the client uses to identify the application when communicating with the backend services.
		/// </summary>
		public string Name { get; }
	}
}
