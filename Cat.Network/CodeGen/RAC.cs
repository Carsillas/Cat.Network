
using System;
using System.IO;
using System.Text;

namespace Cat.Network {


	public class RAC : RPC  {
		
		public event Action OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke()
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			
			OnInvoke?.Invoke();
        }
	}




	public class RAC<T1> : RPC where T1 : IEquatable<T1> {
		
		public event Action<T1> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			
			OnInvoke?.Invoke(_1);
        }
	}




	public class RAC<T1, T2> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> {
		
		public event Action<T1, T2> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2);
        }
	}




	public class RAC<T1, T2, T3> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> {
		
		public event Action<T1, T2, T3> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2, T3 _3)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2, _3);
        }
	}




	public class RAC<T1, T2, T3, T4> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> {
		
		public event Action<T1, T2, T3, T4> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2, T3 _3, T4 _4)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2, _3, _4);
        }
	}




	public class RAC<T1, T2, T3, T4, T5> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> {
		
		public event Action<T1, T2, T3, T4, T5> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2, T3 _3, T4 _4, T5 _5)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2, _3, _4, _5);
        }
	}




	public class RAC<T1, T2, T3, T4, T5, T6> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> {
		
		public event Action<T1, T2, T3, T4, T5, T6> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T6>()(writer, _6);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2, _3, _4, _5, _6);
        }
	}




	public class RAC<T1, T2, T3, T4, T5, T6, T7> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> where T7 : IEquatable<T7> {
		
		public event Action<T1, T2, T3, T4, T5, T6, T7> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T6>()(writer, _6);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T7>()(writer, _7);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			T7 _7 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T7>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2, _3, _4, _5, _6, _7);
        }
	}




	public class RAC<T1, T2, T3, T4, T5, T6, T7, T8> : RPC where T1 : IEquatable<T1> where T2 : IEquatable<T2> where T3 : IEquatable<T3> where T4 : IEquatable<T4> where T5 : IEquatable<T5> where T6 : IEquatable<T6> where T7 : IEquatable<T7> where T8 : IEquatable<T8> {
		
		public event Action<T1, T2, T3, T4, T5, T6, T7, T8> OnInvoke;

		public RAC(RPCInvokeSite invokeSite) : base(invokeSite) {
		
		}
		public void Invoke(T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6, T7 _7, T8 _8)
        {
		
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
					writer.Write((byte)RequestType.RPC);
					writer.Write(Entity.NetworkID.ToByteArray());
					writer.Write(ID);
					
					Entity.Serializer.SerializationContext.GetSerializationFunction<T1>()(writer, _1);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T2>()(writer, _2);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T3>()(writer, _3);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T4>()(writer, _4);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T5>()(writer, _5);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T6>()(writer, _6);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T7>()(writer, _7);
					Entity.Serializer.SerializationContext.GetSerializationFunction<T8>()(writer, _8);

					Entity.Serializer.WriteOutgoingRPCInvocation(stream.ToArray());
				}
			}

        }
        internal override void HandleIncomingInvocation(BinaryReader reader)
        {
			
			T1 _1 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T1>()(reader, null);
			T2 _2 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T2>()(reader, null);
			T3 _3 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T3>()(reader, null);
			T4 _4 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T4>()(reader, null);
			T5 _5 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T5>()(reader, null);
			T6 _6 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T6>()(reader, null);
			T7 _7 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T7>()(reader, null);
			T8 _8 = Entity.Serializer.SerializationContext.GetDeserializationFunction<T8>()(reader, null);
			
			OnInvoke?.Invoke(_1, _2, _3, _4, _5, _6, _7, _8);
        }
	}




}


