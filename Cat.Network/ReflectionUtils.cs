using Cat.Network.Entities;
using Cat.Network.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Cat.Network
{

    internal static class ReflectionUtils {

		private static HashAlgorithm HashAlgorithm { get; } = MD5.Create();

		public class CompoundPropertyReflectionNode {
			public string Path { get; set; }
			public CompoundPropertyReflectionNode Parent { get; set; }
			public PropertyInfo PropertyInfo { get; set; }
			public Type PropertyType => PropertyInfo.PropertyType.GetGenericArguments()[0];
			public List<LeafPropertyReflectionNode> Leaves { get; set; }
			public List<CompoundPropertyReflectionNode> Branches { get; set; }

		}

		public class LeafPropertyReflectionNode {
			public string Path { get; set; }
			public PropertyInfo PropertyInfo { get; set; }

		}


		public static IEnumerable<PropertyInfo> GetInstanceNetworkPropertiesOfType<T>(Type type, bool includeNonPublic) {
			Type currentType = type;
			IEnumerable<PropertyInfo> currentList = Enumerable.Empty<PropertyInfo>();
			while (currentType != null) {
				// get NetworkProperties
				var declaredSimpleProperties = GetDeclaredInstanceNetworkPropertiesOfType<T>(currentType, includeNonPublic);
				currentList = currentList.Concat(declaredSimpleProperties);
				currentType = currentType.BaseType;
			}

			List<PropertyInfo> result = currentList.ToList();

			if (result.Any(Property => Property.SetMethod != null || Property.CanWrite)) {
				string message = $"NetworkProperties must be get-only!\n" +
					string.Join("\n", result.Where(property => property.SetMethod != null || property.CanWrite).Select(property => $"{type.FullName} -> {property.DeclaringType.FullName}.{property.Name}"));
				throw new AccessViolationException(message);
			}

			return currentList;
		}

		public static IEnumerable<PropertyInfo> GetDeclaredInstanceNetworkPropertiesOfType<T>(Type type, bool includeNonPublic) {

			BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			if (includeNonPublic) {
				flags |= BindingFlags.NonPublic;
			}

			return type
				.GetProperties(flags)
				.Where(x => typeof(T).IsAssignableFrom(x.PropertyType));
		}
		public static IEnumerable<FieldInfo> GetInstanceFieldsOfType<T>(Type type, bool includeNonPublic) {
			Type currentType = type;
			IEnumerable<FieldInfo> currentList = Enumerable.Empty<FieldInfo>();
			while (currentType != null) {
				// get NetworkProperties
				var declaredFields = GetDeclaredInstanceFieldsOfType<T>(currentType, includeNonPublic);
				currentList = currentList.Concat(declaredFields);
				currentType = currentType.BaseType;
			}

			return currentList;
		}

		public static IEnumerable<FieldInfo> GetDeclaredInstanceFieldsOfType<T>(Type type, bool includeNonPublic) {

			BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			if (includeNonPublic) {
				flags |= BindingFlags.NonPublic;
			}

			return type
				.GetFields(flags)
				.Where(x => typeof(T).IsAssignableFrom(x.FieldType));
		}

		private static ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> NetworkPropertiesCache { get; } = new ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>>();
		private static ConcurrentDictionary<Type, IReadOnlyList<CompoundPropertyReflectionNode>> CompoundNetworkPropertiesCache { get; } =
			new ConcurrentDictionary<Type, IReadOnlyList<CompoundPropertyReflectionNode>>();

		public static IReadOnlyDictionary<string, NetworkProperty> GetNetworkProperties(NetworkEntity entity) {

			IReadOnlyList<PropertyInfo> simpleNetworkProperties = NetworkPropertiesCache.GetOrAdd(entity.GetType(), SimpleNetworkPropertiesValueFactory);
			IReadOnlyList<CompoundPropertyReflectionNode> compoundNetworkProperties = CompoundNetworkPropertiesCache.GetOrAdd(entity.GetType(), CompoundNetworkPropertiesValueFactory);

			Dictionary<string, NetworkProperty> properties = simpleNetworkProperties.ToDictionary(
				property => $"{property.DeclaringType.Name}.{property.Name}",
				property => (NetworkProperty)property.GetValue(entity));


			foreach (var node in compoundNetworkProperties) {
				InitializeCompoundPropertiesRecursive(node, entity);
			}

			void InitializeCompoundPropertiesRecursive(CompoundPropertyReflectionNode node, object instance) {
				CompoundNetworkProperty propertyInstance = (CompoundNetworkProperty)node.PropertyInfo.GetValue(instance);
				object compoundPropertyValue = propertyInstance.InternalValue;

				foreach (var leaf in node.Leaves) {
					properties.Add(leaf.Path, (NetworkProperty)leaf.PropertyInfo.GetValue(compoundPropertyValue));
				}

				foreach (var branch in node.Branches) {
					InitializeCompoundPropertiesRecursive(branch, compoundPropertyValue);
				}
			}

			return properties;


			void AssertNoFieldNetworkProperties(Type type) {
#if DEBUG // Why is there no way to avoid getting backing fields?
				var fields = GetInstanceFieldsOfType<NetworkProperty>(type, true).Concat(GetInstanceFieldsOfType<CompoundNetworkProperty>(type, true)).ToList();

				if (fields.Any(field => !field.Name.Contains("k__BackingField"))) {
					throw new Exception($"Type {type.Name} contains NetworkProperty instance fields: \n\t{string.Join("\n\t", fields.Where(field => !field.Name.Contains("k__BackingField")).Select(field => $"{field.DeclaringType.Name}.{field.Name}"))}");
				}
#endif
			}

			IReadOnlyList<PropertyInfo> SimpleNetworkPropertiesValueFactory(Type key) {

				AssertNoFieldNetworkProperties(key);

				List<PropertyInfo> simpleProperties =
					GetInstanceNetworkPropertiesOfType<NetworkProperty>(key, true)
					.OrderBy(X => X.Name)
					.ToList();

				return simpleProperties;
			}

			IReadOnlyList<CompoundPropertyReflectionNode> CompoundNetworkPropertiesValueFactory(Type key) {

				List<CompoundPropertyReflectionNode> compoundProperties =
					GetInstanceNetworkPropertiesOfType<CompoundNetworkProperty>(key, true)
					.Select(propertyInfo => new CompoundPropertyReflectionNode { PropertyInfo = propertyInfo, Path = $"{propertyInfo.DeclaringType.Name}.{propertyInfo.Name}" })
					.ToList();

				foreach (var node in compoundProperties) {
					Recurse(node);
				}

				void Recurse(CompoundPropertyReflectionNode node) {

					var parentNode = node.Parent;
					while (parentNode != null) {

						if (parentNode.PropertyType == node.PropertyType) {
							throw new Exception($"Recursive CompoundNetworkProperty detected: {node.PropertyType.Name}");
						}

						parentNode = parentNode.Parent;
					}

					AssertNoFieldNetworkProperties(node.PropertyType);

					node.Branches = GetInstanceNetworkPropertiesOfType<CompoundNetworkProperty>(node.PropertyType, false)
						.Select(propertyInfo => new CompoundPropertyReflectionNode { Parent = node, PropertyInfo = propertyInfo, Path = $"{node.Path}.{propertyInfo.Name}" })
						.ToList();

					node.Leaves = GetInstanceNetworkPropertiesOfType<NetworkProperty>(node.PropertyType, false)
						.Select(propertyInfo => new LeafPropertyReflectionNode { PropertyInfo = propertyInfo, Path = $"{node.Path}.{propertyInfo.Name}" })
						.ToList();

					foreach (var child in node.Branches) {
						Recurse(child);
					}
				}

				return compoundProperties;
			}

		}




	}
}
