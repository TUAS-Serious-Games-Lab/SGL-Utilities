using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Utilities.Backend.KeyValueProperties {
	public interface IPropertyOwner<TId> where TId : struct {
		TId Id { get; set; }
	}
}
