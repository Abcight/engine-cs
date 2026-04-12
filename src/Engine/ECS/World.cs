namespace Engine.ECS;

public sealed class World {
	private delegate void SystemDelegate(ref World world);
	public delegate void RefAction1<TA>(Entity entity, ref TA a);
	public delegate void RefAction2<TA, TB>(Entity entity, ref TA a, ref TB b);
	public delegate void RefAction3<TA, TB, TC>(Entity entity, ref TA a, ref TB b, ref TC c);
	public delegate void RefAction4<TA, TB, TC, TD>(Entity entity, ref TA a, ref TB b, ref TC c, ref TD d);
	private List<SystemDelegate> _systems = new();
	private Stack<Entity> _freeEntities = new();
	private Dictionary<Type, ComponentStore> stores = new();
	private int _highestIndex = 0;

	public void AddSystem<T>() where T : ISystem<T> {
		_systems.Add(T.Update);
	}

	public void RemoveSystem<T>() where T : ISystem<T> {
		_systems.Remove(T.Update);
	}

	public Entity CreateEntity() {
		if (_freeEntities.TryPop(out Entity result)) {
			result.generation += 1;
			return result;
		}
		Entity ret = new Entity(_highestIndex, 0);
		_highestIndex += 1;
		return ret;
	}

	public void DestroyEntity(Entity entity) {
		_freeEntities.Push(entity);
		foreach (var store in stores.Values) {
			store.Unset(entity);
		}
	}

	public void AddComponent<T>(Entity entity, T component) where T : unmanaged {
		var key = typeof(T);

		if (!stores.TryGetValue(key, out var store)) {
			store = ComponentStore.For<T>();
			stores.Add(key, store);
		}

		store.Set(entity, component);
	}

	public void RemoveComponent<T>(Entity entity) where T : unmanaged {
		if (stores.TryGetValue(typeof(T), out var store)) {
			store.Unset(entity);
		}
	}

	public ComponentStore GetStore<T>() where T : unmanaged {
		var key = typeof(T);
		if (!stores.TryGetValue(key, out var store)) {
			store = ComponentStore.For<T>();
			stores.Add(key, store);
		}
		return store;
	}

	public void ForEach<TA>(RefAction1<TA> action)
		where TA : unmanaged {
		var aStore = GetStore<TA>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			ref var a = ref aStore.Get<TA>(entity);

			action(entity, ref a);
		}
	}

	public void ForEach<TA, TB>(RefAction2<TA, TB> action)
	where TA : unmanaged
	where TB : unmanaged {
		var aStore = GetStore<TA>();
		var bStore = GetStore<TB>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			if (!bStore.Has(entity))
				continue;

			ref var a = ref aStore.Get<TA>(entity);
			ref var b = ref bStore.Get<TB>(entity);

			action(entity, ref a, ref b);
		}
	}

	public void ForEach<TA, TB, TC>(RefAction3<TA, TB, TC> action)
	where TA : unmanaged
	where TB : unmanaged
	where TC : unmanaged {
		var aStore = GetStore<TA>();
		var bStore = GetStore<TB>();
		var cStore = GetStore<TC>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			if (!bStore.Has(entity)) continue;
			if (!cStore.Has(entity)) continue;

			ref var a = ref aStore.Get<TA>(entity);
			ref var b = ref bStore.Get<TB>(entity);
			ref var c = ref cStore.Get<TC>(entity);

			action(entity, ref a, ref b, ref c);
		}
	}

	public void ForEach<TA, TB, TC, TD>(RefAction4<TA, TB, TC, TD> action)
	where TA : unmanaged
	where TB : unmanaged
	where TC : unmanaged
	where TD : unmanaged {
		var aStore = GetStore<TA>();
		var bStore = GetStore<TB>();
		var cStore = GetStore<TC>();
		var dStore = GetStore<TD>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			if (!bStore.Has(entity)) continue;
			if (!cStore.Has(entity)) continue;
			if (!dStore.Has(entity)) continue;

			ref var a = ref aStore.Get<TA>(entity);
			ref var b = ref bStore.Get<TB>(entity);
			ref var c = ref cStore.Get<TC>(entity);
			ref var d = ref dStore.Get<TD>(entity);

			action(entity, ref a, ref b, ref c, ref d);
		}
	}
}