using Cat.Network.Generator;
using Cat.Network.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace Cat.Network.Entities
{

    [NetworkProperty(AccessModifier.Public, typeof(int), "X")]
	[NetworkProperty(AccessModifier.Public, typeof(int), "Y")]
	[NetworkProperty(AccessModifier.Public, typeof(int), "Z")]
	[NetworkProperty(AccessModifier.Public, typeof(bool), "DestroyWithOwner")]
	public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		public Guid NetworkID { get; internal set; }
		public bool IsOwner { get; internal set; }


		private NetworkProperty[] NetworkProperties;
		NetworkProperty[] INetworkEntityInitializer.NetworkProperties { get => NetworkProperties; set => NetworkProperties = value; }


		public NetworkEntity() {

			((INetworkEntityInitializer)this).Initialize();
		}

		public bool Equals(NetworkEntity other) {
			return NetworkID == other.NetworkID;
		}


		[RPC]
		void RPC.NetworkEntity.MyMethod(int a) {

		}

		[RPC]
		void RPC.NetworkEntity.MyMethod2() {

		}


	}


	[NetworkProperty(AccessModifier.Public, typeof(TestEntity<TestEntity<System.Boolean, System.Int32>, int>), "DestroyWithOwner")]
	public partial class TestEntity<T, U> : NetworkEntity {

	}


	[NetworkProperty(AccessModifier.Public, typeof(bool), "DestroyWithOwner")]
	public partial class TestEntity2 : TestEntity<int, int> {

	}





}
