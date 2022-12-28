
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Cat.Network {
	public partial class NetworkEntity {


		internal void DeserializeInvokeAction0(BinaryReader reader, MethodInfo methodInfo) { 
			
			
			methodInfo.Invoke(this, new object[] {  });
		}



		internal void DeserializeInvokeAction1<T1>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1 });
		}



		internal void DeserializeInvokeAction2<T1, T2>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2 });
		}



		internal void DeserializeInvokeAction3<T1, T2, T3>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2, _3 });
		}



		internal void DeserializeInvokeAction4<T1, T2, T3, T4>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2, _3, _4 });
		}



		internal void DeserializeInvokeAction5<T1, T2, T3, T4, T5>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2, _3, _4, _5 });
		}



		internal void DeserializeInvokeAction6<T1, T2, T3, T4, T5, T6>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2, _3, _4, _5, _6 });
		}



		internal void DeserializeInvokeAction7<T1, T2, T3, T4, T5, T6, T7>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			T7 _7 = Serializer.SerializationContext.GetDeserializationFunction<T7>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2, _3, _4, _5, _6, _7 });
		}



		internal void DeserializeInvokeAction8<T1, T2, T3, T4, T5, T6, T7, T8>(BinaryReader reader, MethodInfo methodInfo) { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			T7 _7 = Serializer.SerializationContext.GetDeserializationFunction<T7>()(reader, null);
			T8 _8 = Serializer.SerializationContext.GetDeserializationFunction<T8>()(reader, null);
			
			methodInfo.Invoke(this, new object[] { _1, _2, _3, _4, _5, _6, _7, _8 });
		}



	}
}

