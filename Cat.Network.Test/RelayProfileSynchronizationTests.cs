namespace Cat.Network.Test;

public sealed class RelayProfileSynchronizationTests {
	[Test]
	public void OwnerAdd_IsAppliedOnceOnEveryPeerWithoutRepeatingOwnerEvents() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		int added = 0;
		owner.Values.ItemAdded += (_, _) => added++;

		owner.Values.Add(5);
		owner.Labels.Add(5, 50);
		session.Pump();

		session.AssertCopies(owner.Id, 0, 0, [5], new Dictionary<int, int> { [5] = 50 });
		Assert.That(added, Is.EqualTo(1));
		Assert.That(session.Clients[0].Profile, Is.SameAs(owner));
	}

	[Test]
	public void IdenticalAcknowledgement_PreservesChildObjectsAndCollectionEntries() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		ProfileSynchronizationChild child = new() { Value = 5 };
		ProfileSynchronizationChild entry = new() { Value = 6 };
		owner.Child = child;
		owner.Children.Add(entry);
		session.Pump();

		Assert.Multiple(() => {
			Assert.That(owner.Child, Is.SameAs(child));
			Assert.That(owner.Children, Has.Count.EqualTo(1));
			Assert.That(owner.Children[0], Is.SameAs(entry));
		});
	}

	[Test]
	public void ServerCorrection_PreservesRootAndCollectionContainerButReplacesChildren() {
		Session session = new(profile => {
			profile.Child = new ProfileSynchronizationChild { Value = 5 };
			profile.Children.Add(new ProfileSynchronizationChild { Value = 6 });
		});
		ProfileSynchronizationState owner = session.Profile(0);
		ProfileSynchronizationChild oldChild = owner.Child!;
		NetworkList<ProfileSynchronizationChild> collection = owner.Children;
		ProfileSynchronizationChild oldEntry = collection[0];

		session.ServerProfile(owner.Id).Status = 1;
		session.Pump();

		Assert.Multiple(() => {
			Assert.That(session.Clients[0].Profile, Is.SameAs(owner));
			Assert.That(owner.Children, Is.SameAs(collection));
			Assert.That(owner.Child, Is.Not.SameAs(oldChild));
			Assert.That(owner.Children[0], Is.Not.SameAs(oldEntry));
			Assert.That(owner.Child!.Value, Is.EqualTo(5));
			Assert.That(owner.Children[0].Value, Is.EqualTo(6));
			Assert.That(owner.Status, Is.EqualTo(1));
		});
	}

	[Test]
	public void OwnerRemove_IsAppliedOnceOnEveryPeer() {
		Session session = new(profile => {
			profile.Values.Add(10);
			profile.Values.Add(20);
			profile.Labels.Add(10, 100);
			profile.Labels.Add(20, 200);
		});
		ProfileSynchronizationState owner = session.Profile(0);

		owner.Values.RemoveAt(0);
		owner.Labels.Remove(10);
		session.Pump();

		session.AssertCopies(owner.Id, 0, 0, [20], new Dictionary<int, int> { [20] = 200 });
	}

	[Test]
	public void PendingResponse_DoesNotEraseNewerUnsentScalarEdit() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		owner.Value = 1;
		session.Clients[0].Tick();
		session.Server.Tick();

		owner.Value = 2;
		session.Clients[0].Tick();

		Assert.That(owner.Value, Is.EqualTo(2));
		session.Pump();
		session.AssertCopies(owner.Id, 2, 0, [], []);
	}

	[Test]
	public void PendingResponse_DoesNotOverwriteANewerRequestAlreadySent() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		owner.Value = 1;
		session.Clients[0].Tick();
		session.Server.Tick();

		session.Transports[0].HoldIncoming = true;
		owner.Value = 2;
		session.Clients[0].Tick();
		session.Transports[0].HoldIncoming = false;
		session.Clients[0].Tick();

		Assert.That(owner.Value, Is.EqualTo(2));
		session.Pump();
		session.AssertCopies(owner.Id, 2, 0, [], []);
	}

	[Test]
	public void SeveralRequestsBeforeServerTick_AreAcknowledgedTogether() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		for (int i = 1; i <= 3; i++) {
			owner.Value = i;
			owner.Values.Add(i);
			session.Clients[0].Tick();
		}

		session.Pump();

		session.AssertCopies(owner.Id, 3, 0, [1, 2, 3], []);
	}

	[Test]
	public void RepeatedRequest_DoesNotApplyCollectionOperationsAgain() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		owner.Values.Add(5);
		session.Clients[0].Tick();
		byte[] request = session.Transports[0].SentMessages.Last();
		session.Transports[0].Send(request);
		session.Pump();

		session.AssertCopies(owner.Id, 0, 0, [5], []);
	}

	[Test]
	public void MultipleOwners_CanUpdateTheirProfilesInTheSameServerTick() {
		Session session = new();
		foreach (RelayClient client in session.Clients) {
			ProfileSynchronizationState profile = (ProfileSynchronizationState)client.Profile!;
			profile.Value = profile.Identity;
			profile.Values.Add(profile.Identity);
			client.Tick();
		}

		session.Pump();

		foreach (ProfileSynchronizationState profile in session.ServerProfiles) {
			session.AssertCopies(profile.Id, profile.Identity, 0, [profile.Identity], []);
		}
	}

	[Test]
	public void ServerChanges_ReachAssignedOwnerAndObservers() {
		Session session = new(profile => profile.Values.Add(10));
		ProfileSynchronizationState owner = session.Profile(0);
		ProfileSynchronizationState server = session.ServerProfile(owner.Id);
		server.Value = 9;
		server.Status = 90;
		server.Values.RemoveAt(0);
		server.Values.Add(20);
		server.Labels.Add(20, 200);

		session.Pump();

		session.AssertCopies(owner.Id, 9, 90, [20], new Dictionary<int, int> { [20] = 200 });
		Assert.That(session.Clients[0].Profile, Is.SameAs(owner));
	}

	[Test]
	public void ServerCorrectionDuringRequest_ReachesOwnerAndObservers() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		ProfileSynchronizationState server = session.ServerProfile(owner.Id);
		server.PropertyChanged += (_, _) => {
			if (server.Value > 10) {
				server.Value = 10;
				server.Status = 1;
			}
		};

		owner.Value = 99;
		owner.Values.Add(5);
		session.Pump();

		session.AssertCopies(owner.Id, 10, 1, [5], []);
	}

	[Test]
	public void DeferredServerCorrection_SurvivesAlongsideNewerLocalEdit() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		owner.Value = 1;
		session.Clients[0].Tick();
		session.ServerProfile(owner.Id).Status = 42;
		session.Server.Tick();

		owner.Value = 2;
		session.Clients[0].Tick();
		Assert.That(owner.Value, Is.EqualTo(2));
		session.Pump();

		session.AssertCopies(owner.Id, 2, 42, [], []);
	}

	[Test]
	public void AcceptedRequestWithoutValueChange_StillSynchronizesDeferredServerState() {
		Session session = new();
		ProfileSynchronizationState owner = session.Profile(0);
		ProfileSynchronizationState server = session.ServerProfile(owner.Id);
		server.Value = 7;
		server.Status = 42;
		session.Server.Tick();

		owner.Value = 7;
		session.Clients[0].Tick();
		session.Pump();

		session.AssertCopies(owner.Id, 7, 42, [], []);
	}

	[Test]
	public void PendingResponse_DoesNotReplayOrEraseNewerCollectionOperations() {
		Session session = new(profile => profile.Values.Add(10));
		ProfileSynchronizationState owner = session.Profile(0);
		owner.Values.Add(20);
		session.Clients[0].Tick();
		session.Server.Tick();

		owner.Values.RemoveAt(0);
		owner.Values.Add(30);
		session.Clients[0].Tick();
		Assert.That(owner.Values, Is.EqualTo(new[] { 20, 30 }));
		session.Pump();

		session.AssertCopies(owner.Id, 0, 0, [20, 30], []);
	}

	private sealed class Session {
		public RelayServer Server { get; }
		public RelayClient[] Clients { get; }
		public DelayedTransport[] Transports { get; }
		public List<ProfileSynchronizationState> ServerProfiles { get; } = [];

		public Session(Action<ProfileSynchronizationState>? initialize = null) {
			TypeCatalogue catalogue = new();
			catalogue.Register(typeof(ProfileSynchronizationState));
			catalogue.Register(typeof(ProfileSynchronizationChild));
			MemoryRelayDaemon daemon = new(() => {
				// Distinct values keep this fixture independent of the identity-set finding R2.
				ProfileSynchronizationState profile = new() { Identity = ServerProfiles.Count + 1 };
				initialize?.Invoke(profile);
				ServerProfiles.Add(profile);
				return profile;
			});
			Server = new RelayServer(daemon, catalogue, new TestEntityStorage());
			Clients = [new(catalogue), new(catalogue), new(catalogue)];
			Transports = Clients.Select(_ => new DelayedTransport(daemon.Connect())).ToArray();
			for (int i = 0; i < Clients.Length; i++) {
				Clients[i].Connect(Transports[i]);
			}
			Pump();
		}

		public ProfileSynchronizationState Profile(int client) => (ProfileSynchronizationState)Clients[client].Profile!;

		public ProfileSynchronizationState ServerProfile(Guid id) => ServerProfiles.Single(profile => profile.Id == id);

		public void Pump() {
			for (int i = 0; i < 4; i++) {
				Server.Tick();
				foreach (RelayClient client in Clients) {
					client.Tick();
				}
			}
		}

		public void AssertCopies(Guid id, int value, int status, int[] values, Dictionary<int, int> labels) {
			List<ProfileSynchronizationState> copies = [ServerProfile(id)];
			foreach (RelayClient client in Clients) {
				Assert.That(client.TryGetProfile(id, out NetworkProfile? profile), Is.True);
				copies.Add((ProfileSynchronizationState)profile!);
			}
			Assert.Multiple(() => {
				foreach (ProfileSynchronizationState profile in copies) {
					Assert.That(profile.Value, Is.EqualTo(value));
					Assert.That(profile.Status, Is.EqualTo(status));
					Assert.That(profile.Values, Is.EqualTo(values));
					Assert.That(profile.Labels, Is.EquivalentTo(labels));
					Assert.That(((INetworkObject)profile).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
				}
			});
		}
	}

	private sealed class DelayedTransport : IRelayTransport {
		private MemoryRelayTransport Inner { get; }
		public bool HoldIncoming { get; set; }
		public List<byte[]> SentMessages { get; } = [];
		public event MessageHandler? MessageReceived;
		public event Action<IRelayTransport>? Disconnected;

		public DelayedTransport(MemoryRelayTransport inner) {
			Inner = inner;
			inner.MessageReceived += (_, message) => MessageReceived?.Invoke(this, message);
			inner.Disconnected += _ => Disconnected?.Invoke(this);
		}

		public void Send(ReadOnlySpan<byte> message) {
			SentMessages.Add(message.ToArray());
			Inner.Send(message);
		}

		public void PumpMessages() {
			if (!HoldIncoming) {
				Inner.PumpMessages();
			}
		}
	}
}

[NetworkObject]
public partial class ProfileSynchronizationState : NetworkProfile {
	[NetworkProperty]
	public partial int Identity { get; set; }

	[NetworkProperty]
	public partial int Value { get; set; }

	[NetworkProperty]
	public partial int Status { get; set; }

	[NetworkCollection]
	public partial NetworkList<int> Values { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, int> Labels { get; }

	[NetworkProperty]
	public partial ProfileSynchronizationChild? Child { get; set; }

	[NetworkCollection]
	public partial NetworkList<ProfileSynchronizationChild> Children { get; }
}

[NetworkObject]
public partial class ProfileSynchronizationChild : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; set; }
}
