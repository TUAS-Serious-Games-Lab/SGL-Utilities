using System;

namespace SGL.Utilities.Backend.Applications {
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
