using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.AspNetCore {
	/// <summary>
	/// Provides the <see cref="ForController{TController}(ControllerActionEndpointConventionBuilder)"/> extension method.
	/// </summary>
	public static class EndpointConventionBuilderExtensions {
		/// <summary>
		/// Obtains an <see cref="IEndpointConventionBuilder"/> that applies the conventions that are added to it only to endpoints from <paramref name="builder"/>
		/// that belong to the controller class <typeparamref name="TController"/>.
		/// This allows configuring conventions for a specific controller.
		/// </summary>
		/// <typeparam name="TController">The controller class to filter for.</typeparam>
		/// <param name="builder">The builder to attach to.</param>
		/// <returns>The constrained convention builder that only applies to <typeparamref name="TController"/>.</returns>
		public static EndpointConventionBuilderForController<TController> ForController<TController>(this ControllerActionEndpointConventionBuilder builder) where TController : ControllerBase {
			return new EndpointConventionBuilderForController<TController>(builder);
		}
	}

	/// <summary>
	/// Collects endpoint conventions (from <see cref="Add(Action{EndpointBuilder})"/>) and only applies them to endpoints of controller <typeparamref name="TController"/>.
	/// It attaches to an underlying <see cref="IEndpointConventionBuilder"/> using <see cref="EndpointConventionBuilderExtensions.ForController{TController}(ControllerActionEndpointConventionBuilder)"/>.
	/// </summary>
	/// <typeparam name="TController">The controller class to filter for.</typeparam>
	public class EndpointConventionBuilderForController<TController> : IEndpointConventionBuilder where TController : ControllerBase {
		private List<Action<EndpointBuilder>> builders = new List<Action<EndpointBuilder>>();

		internal EndpointConventionBuilderForController(ControllerActionEndpointConventionBuilder builder) {
			builder.Add(apply);
		}

		private void apply(EndpointBuilder b) {
			var descriptor = b.Metadata.OfType<ControllerActionDescriptor>().FirstOrDefault();
			if (descriptor == null) return;
			if (descriptor.ControllerTypeInfo != typeof(TController)) return;
			foreach (var bld in builders) {
				bld(b);
			}
		}

		/// <inheritdoc/>
		public void Add(Action<EndpointBuilder> convention) {
			builders.Add(convention);
		}
	}
}
