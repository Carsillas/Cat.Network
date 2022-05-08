using Cat.Network;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ProxyManager : IProxyManager {

	public Client Client { get; set; }

	private Dictionary<Guid, GameObject> Proxies = new Dictionary<Guid, GameObject>();

	public ProxyManager() {
		Debug.Log("???");
	}

	public void OnEntityCreated(NetworkEntity entity) {
		Debug.Log("Created " + entity.NetworkID);
		Proxies.Add(entity.NetworkID, Create(entity));
	}

	public void OnEntityDeleted(NetworkEntity entity) {
		Debug.Log("Deleted " + entity.NetworkID);
		if (Proxies.TryGetValue(entity.NetworkID, out GameObject proxy)) {
			UnityEngine.Object.Destroy(proxy);
		}
	}
	public void OnGainedOwnership(NetworkEntity entity) {
		Debug.Log("Gained ownership of " + entity.NetworkID);
		if (Proxies.TryGetValue(entity.NetworkID, out GameObject proxy)) {
			IOwnerEntityBehavior[] Behaviors = proxy.GetComponentsInChildren<IOwnerEntityBehavior>();
			foreach (MonoBehaviour behavior in Behaviors) {
				behavior.enabled = true;
			}
		}
	}

	private Dictionary<Type, GameObject> Prefabs { get; } = new Dictionary<Type, GameObject>();
	private Dictionary<Type, Func<NetworkEntity, GameObject>> GenericCreationMethods { get; } = new Dictionary<Type, Func<NetworkEntity, GameObject>>();

	public GameObject Create(NetworkEntity entity) {

		if (!GenericCreationMethods.TryGetValue(entity.GetType(), out Func<NetworkEntity, GameObject> CreationMethod)) {
			MethodInfo OpenGeneric = ((Func<Func<NetworkEntity, GameObject>>)GetCreateDelegate<NetworkEntity>).Method.GetGenericMethodDefinition();
			MethodInfo ClosedGeneric = OpenGeneric.MakeGenericMethod(entity.GetType());

			CreationMethod = (Func<NetworkEntity, GameObject>)ClosedGeneric.Invoke(this, new object[] { });

			GenericCreationMethods.Add(entity.GetType(), CreationMethod);
		}

		return CreationMethod(entity);
	}

	private Func<NetworkEntity, GameObject> GetCreateDelegate<T>() where T : NetworkEntity {
		Func<T, GameObject> CreateMethod = CreateInternal;
		GameObject Wrapped(NetworkEntity entity) {
			return CreateMethod((T)entity);
		}

		return Wrapped;
	}

	private GameObject CreateInternal<T>(T entity) where T : NetworkEntity {
		if (!Prefabs.TryGetValue(typeof(T), out GameObject prefab)) {
			prefab = (GameObject)Resources.Load($"Proxies/{typeof(T).Name}");
			Prefabs.Add(typeof(T), prefab);
		}

		GameObject gameObject = UnityEngine.Object.Instantiate(prefab);
		EntityBehavior<T>[] Behaviors = gameObject.GetComponentsInChildren<EntityBehavior<T>>();
		foreach (EntityBehavior<T> behavior in Behaviors) {
			behavior.Entity = entity;
			behavior.Client = Client;

			if (behavior is IOwnerEntityBehavior) {
				behavior.enabled = false;
			}

		}
		return gameObject;
	}
}
