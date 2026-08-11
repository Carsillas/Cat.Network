namespace Cat.Network;

public partial class RelayClient {
	private List<Action<NetworkEntity>> EntityObservedHandlers { get; } = [];
	private List<Action<NetworkEntity>> EntityUnobservedHandlers { get; } = [];
	private List<Action<NetworkEntity>> EntityOwnershipGainedHandlers { get; } = [];
	private List<Action<NetworkEntity>> EntityOwnershipLostHandlers { get; } = [];
	private List<Action<Exception>> EventHandlerExceptionHandlers { get; } = [];

	public event Action<NetworkEntity>? EntityObserved {
		add {
			if (value is not null) {
				EntityObservedHandlers.Add(value);
			}
		}
		remove {
			if (value is not null) {
				EntityObservedHandlers.Remove(value);
			}
		}
	}

	public event Action<NetworkEntity>? EntityUnobserved {
		add {
			if (value is not null) {
				EntityUnobservedHandlers.Add(value);
			}
		}
		remove {
			if (value is not null) {
				EntityUnobservedHandlers.Remove(value);
			}
		}
	}

	public event Action<NetworkEntity>? EntityOwnershipGained {
		add {
			if (value is not null) {
				EntityOwnershipGainedHandlers.Add(value);
			}
		}
		remove {
			if (value is not null) {
				EntityOwnershipGainedHandlers.Remove(value);
			}
		}
	}

	public event Action<NetworkEntity>? EntityOwnershipLost {
		add {
			if (value is not null) {
				EntityOwnershipLostHandlers.Add(value);
			}
		}
		remove {
			if (value is not null) {
				EntityOwnershipLostHandlers.Remove(value);
			}
		}
	}

	public event Action<Exception>? EventHandlerException {
		add {
			if (value is not null) {
				EventHandlerExceptionHandlers.Add(value);
			}
		}
		remove {
			if (value is not null) {
				EventHandlerExceptionHandlers.Remove(value);
			}
		}
	}

	private void RaiseEntityEvent(List<Action<NetworkEntity>> handlers, NetworkEntity entity) {
		foreach (Action<NetworkEntity> handler in handlers) {
			try {
				handler(entity);
			} catch (Exception exception) {
				RaiseEventHandlerException(exception);
			}
		}
	}

	private void RaiseEventHandlerException(Exception exception) {
		foreach (Action<Exception> handler in EventHandlerExceptionHandlers) {
			try {
				handler(exception);
			} catch (Exception) {
				// Consumer exception handlers should not interrupt relay state changes.
			}
		}
	}
}
