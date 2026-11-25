using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(nameof(RelayClient.EntityObserved))]
	[TestCase(nameof(RelayClient.EntityUnobserved))]
	[TestCase(nameof(RelayClient.EntityOwnershipGained))]
	[TestCase(nameof(RelayClient.EntityOwnershipLost))]
	public void RelayClientEntityEvents_SubscriptionMutation_UsesCurrentNotificationSnapshot(string eventName) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		client.Connect(new RecordingRelayTransport());
		int entityValue = 0;
		int ownershipGainedCount = 0;
		client.EntityOwnershipGained += _ => ownershipGainedCount++;

		(Action<Action<NetworkEntity>> Subscribe, Action<Action<NetworkEntity>> Unsubscribe) subscription = eventName switch {
			nameof(RelayClient.EntityObserved) => (handler => client.EntityObserved += handler, handler => client.EntityObserved -= handler),
			nameof(RelayClient.EntityUnobserved) => (handler => client.EntityUnobserved += handler, handler => client.EntityUnobserved -= handler),
			nameof(RelayClient.EntityOwnershipGained) => (handler => client.EntityOwnershipGained += handler, handler => client.EntityOwnershipGained -= handler),
			nameof(RelayClient.EntityOwnershipLost) => (handler => client.EntityOwnershipLost += handler, handler => client.EntityOwnershipLost -= handler),
			_ => throw new ArgumentOutOfRangeException(nameof(eventName))
		};

		void Notify() {
			RelayValueState entity = new() { Value = ++entityValue };
			client.Spawn(entity);
			if (eventName == nameof(RelayClient.EntityUnobserved)) {
				client.Delete(entity);
			} else if (eventName == nameof(RelayClient.EntityOwnershipLost)) {
				client.Tick();
				client.AssignOwner(entity, Guid.NewGuid());
			}
			client.Tick();

			Assert.Multiple(() => {
				Assert.That(client.TryGetEntity(entity.Id, out _), Is.EqualTo(eventName != nameof(RelayClient.EntityUnobserved)));
				Assert.That(entity.IsSpawned, Is.EqualTo(eventName != nameof(RelayClient.EntityUnobserved)));
				Assert.That(entity.IsOwner, Is.EqualTo(eventName is nameof(RelayClient.EntityObserved) or nameof(RelayClient.EntityOwnershipGained)));
			});
		}

		AssertLifecycleSubscriptionSnapshot(client, subscription.Subscribe, subscription.Unsubscribe, Notify);
		Assert.That(ownershipGainedCount, Is.EqualTo(2));
	}

	[TestCase(nameof(RelayClient.ProfileJoined))]
	[TestCase(nameof(RelayClient.ProfileLeft))]
	public void RelayClientProfileEvents_SubscriptionMutation_UsesCurrentNotificationSnapshot(string eventName) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		MemoryRelayTransport transport = new();

		(Action<Action<NetworkProfile>> Subscribe, Action<Action<NetworkProfile>> Unsubscribe) subscription = eventName switch {
			nameof(RelayClient.ProfileJoined) => (handler => client.ProfileJoined += handler, handler => client.ProfileJoined -= handler),
			nameof(RelayClient.ProfileLeft) => (handler => client.ProfileLeft += handler, handler => client.ProfileLeft -= handler),
			_ => throw new ArgumentOutOfRangeException(nameof(eventName))
		};

		void Notify() {
			Guid profileId = Guid.NewGuid();
			client.Receive(transport, BuildProfileMessage(catalogue, profileId, new RelayProfileState()));
			if (eventName == nameof(RelayClient.ProfileLeft)) {
				client.Receive(transport, BuildAssignProfileMessage(profileId));
				client.Receive(transport, BuildDeleteProfileMessage(profileId));
			}

			Assert.Multiple(() => {
				Assert.That(client.TryGetProfile(profileId, out _), Is.EqualTo(eventName == nameof(RelayClient.ProfileJoined)));
				Assert.That(client.Profile, Is.Null);
			});
		}

		AssertLifecycleSubscriptionSnapshot(client, subscription.Subscribe, subscription.Unsubscribe, Notify);
	}

	[TestCase(nameof(RelayClient.EntityObserved))]
	[TestCase(nameof(RelayClient.ProfileJoined))]
	public void RelayClientEventHandlerException_SubscriptionMutation_PreservesErrorAndLifecycleCallbacks(string sourceEventName) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState), typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		List<string> calls = [];
		List<Exception> reportedExceptions = [];
		List<Exception> addedHandlerExceptions = [];
		InvalidOperationException firstException = new("First lifecycle handler failed.");
		InvalidOperationException secondException = new("Second lifecycle handler failed.");

		void OnAdded(Exception exception) {
			calls.Add("added-error");
			addedHandlerExceptions.Add(exception);
		}

		void OnRemoved(Exception _) {
			calls.Add("removed-error");
		}

		void OnMutating(Exception _) {
			calls.Add("mutating-error");
			client.EventHandlerException -= OnMutating;
			client.EventHandlerException -= OnRemoved;
			client.EventHandlerException += OnAdded;
			throw new InvalidOperationException("Exception handler failed after changing subscriptions.");
		}

		client.EventHandlerException += OnMutating;
		client.EventHandlerException += OnRemoved;
		client.EventHandlerException += exception => {
			calls.Add("remaining-error");
			reportedExceptions.Add(exception);
		};

		Action<Action> subscribe = sourceEventName switch {
			nameof(RelayClient.EntityObserved) => handler => client.EntityObserved += _ => handler(),
			nameof(RelayClient.ProfileJoined) => handler => client.ProfileJoined += _ => handler(),
			_ => throw new ArgumentOutOfRangeException(nameof(sourceEventName))
		};
		subscribe(() => {
			calls.Add("first-lifecycle");
			throw firstException;
		});
		subscribe(() => calls.Add("between-lifecycle"));
		subscribe(() => {
			calls.Add("second-lifecycle");
			throw secondException;
		});
		subscribe(() => calls.Add("last-lifecycle"));

		if (sourceEventName == nameof(RelayClient.EntityObserved)) {
			RelayValueState entity = new();
			client.EntityOwnershipGained += _ => calls.Add("ownership-gained");
			Assert.That(() => client.Spawn(entity), Throws.Nothing);
			Assert.That(entity.IsOwner, Is.True);
		} else {
			Guid profileId = Guid.NewGuid();
			Assert.That(() => client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, profileId, new RelayProfileState())), Throws.Nothing);
			Assert.That(client.TryGetProfile(profileId, out _), Is.True);
		}

		List<string> expectedCalls = [
			"first-lifecycle", "mutating-error", "removed-error", "remaining-error", "between-lifecycle",
			"second-lifecycle", "remaining-error", "added-error", "last-lifecycle"
		];
		if (sourceEventName == nameof(RelayClient.EntityObserved)) {
			expectedCalls.Add("ownership-gained");
		}

		Assert.Multiple(() => {
			Assert.That(calls, Is.EqualTo(expectedCalls));
			Assert.That(reportedExceptions, Is.EqualTo(new[] { firstException, secondException }));
			Assert.That(addedHandlerExceptions, Is.EqualTo(new[] { secondException }));
		});
	}

	private static void AssertLifecycleSubscriptionSnapshot<T>(
		RelayClient client,
		Action<Action<T>> subscribe,
		Action<Action<T>> unsubscribe,
		Action notify) {
		List<string> calls = [];
		List<Exception> reportedExceptions = [];
		InvalidOperationException handlerException = new("Lifecycle handler failed.");

		void OnAdded(T _) {
			calls.Add("added");
		}

		void OnRemoved(T _) {
			calls.Add("removed");
		}

		void OnMutating(T _) {
			calls.Add("mutating");
			unsubscribe(OnMutating);
			unsubscribe(OnRemoved);
			subscribe(OnAdded);
		}

		client.EventHandlerException += exception => {
			calls.Add("exception");
			reportedExceptions.Add(exception);
		};
		subscribe(OnMutating);
		subscribe(OnRemoved);
		subscribe(_ => {
			calls.Add("throwing");
			throw handlerException;
		});
		subscribe(_ => calls.Add("remaining"));

		Assert.That(() => notify(), Throws.Nothing);
		Assert.That(calls, Is.EqualTo(new[] { "mutating", "removed", "throwing", "exception", "remaining" }));
		calls.Clear();

		Assert.That(() => notify(), Throws.Nothing);
		Assert.Multiple(() => {
			Assert.That(calls, Is.EqualTo(new[] { "throwing", "exception", "remaining", "added" }));
			Assert.That(reportedExceptions, Is.EqualTo(new[] { handlerException, handlerException }));
		});
	}
}
