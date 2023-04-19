using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public interface ISerializationContext {
	internal bool DeserializeDirtiesProperty { get; }
	internal int Time { get; }

}
