
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Cat.Network {
	public partial class NetworkEntity {


		public void InvokeRPC(Action RPC ) 
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1>(Action<T1> RPC, T1 _1) where T1 : IEquatable<T1>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2>(Action<T1, T2> RPC, T1 _1, T2 _2) where T1 : IEquatable<T1> where T2 : IEquatable<T2>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2, T3>(Action<T1, T2, T3> RPC, T1 _1, T2 _2, T3 _3) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2, T3, T4>(Action<T1, T2, T3, T4> RPC, T1 _1, T2 _2, T3 _3, T4 _4) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> RPC, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> RPC, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);
					Serializer.SerializationContext.GetSerializationFunction<T6>()(writer, _6);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> RPC, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> where T7 : IEquatable<T7>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);
					Serializer.SerializationContext.GetSerializationFunction<T6>()(writer, _6);
					Serializer.SerializationContext.GetSerializationFunction<T7>()(writer, _7);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }


		public void InvokeRPC<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> RPC, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> where T7 : IEquatable<T7> where T8 : IEquatable<T8>
        {

		    if(RPC.Target != RPCTarget)
            {
                throw new Exception("RPC Invoked on incorrect object!");
            }

            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(NetworkID.ToByteArray());
					Serializer.WriteRPCID(writer, RPC.Method);
					
					Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);
					Serializer.SerializationContext.GetSerializationFunction<T6>()(writer, _6);
					Serializer.SerializationContext.GetSerializationFunction<T7>()(writer, _7);
					Serializer.SerializationContext.GetSerializationFunction<T8>()(writer, _8);

					Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}
        }



		internal void DeserializeInvokeAction0(BinaryReader reader, MethodInfo methodInfo)  { 
			
			
			methodInfo.Invoke(RPCTarget, new object[] {  });
		}



		internal void DeserializeInvokeAction1<T1>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1 });
		}



		internal void DeserializeInvokeAction2<T1, T2>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2 });
		}



		internal void DeserializeInvokeAction3<T1, T2, T3>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2, _3 });
		}



		internal void DeserializeInvokeAction4<T1, T2, T3, T4>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2, _3, _4 });
		}



		internal void DeserializeInvokeAction5<T1, T2, T3, T4, T5>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2, _3, _4, _5 });
		}



		internal void DeserializeInvokeAction6<T1, T2, T3, T4, T5, T6>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2, _3, _4, _5, _6 });
		}



		internal void DeserializeInvokeAction7<T1, T2, T3, T4, T5, T6, T7>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> where T7 : IEquatable<T7> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			T7 _7 = Serializer.SerializationContext.GetDeserializationFunction<T7>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2, _3, _4, _5, _6, _7 });
		}



		internal void DeserializeInvokeAction8<T1, T2, T3, T4, T5, T6, T7, T8>(BinaryReader reader, MethodInfo methodInfo) where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> where T7 : IEquatable<T7> where T8 : IEquatable<T8> { 
			
			T1 _1 = Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			T7 _7 = Serializer.SerializationContext.GetDeserializationFunction<T7>()(reader, null);
			T8 _8 = Serializer.SerializationContext.GetDeserializationFunction<T8>()(reader, null);
			
			methodInfo.Invoke(RPCTarget, new object[] { _1, _2, _3, _4, _5, _6, _7, _8 });
		}



	}
}


