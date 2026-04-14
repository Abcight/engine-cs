namespace Engine.ECS;

public sealed class World {
	private delegate void SystemDelegate(ref World world);
	public delegate void RefAction1<TA>(Entity entity, ref TA a);
	public delegate void RefAction2<TA, TB>(Entity entity, ref TA a, ref TB b);
	public delegate void RefAction3<TA, TB, TC>(Entity entity, ref TA a, ref TB b, ref TC c);
	public delegate void RefAction4<TA, TB, TC, TD>(Entity entity, ref TA a, ref TB b, ref TC c, ref TD d);
	private List<SystemDelegate> systems = new();
	private Stack<Entity> freeEntities = new();
	private Dictionary<Type, ComponentStore> stores = new();
	private int highestIndex = 0;

	public void AddSystem<T>() where T : ISystem<T> {
		systems.Add(T.Update);
	}

	public void RemoveSystem<T>() where T : ISystem<T> {
		systems.Remove(T.Update);
	}

	public Entity CreateEntity() {
		if (freeEntities.TryPop(out Entity result)) {
			result.generation += 1;
			return result;
		}
		Entity ret = new Entity(highestIndex, 0);
		highestIndex += 1;
		return ret;
	}

	public void DestroyEntity(Entity entity) {
		freeEntities.Push(entity);
		foreach (var store in stores.Values) {
			store.Unset(entity);
		}
	}

	public void AddComponent<T>(Entity entity, T component) where T : notnull {
		var key = typeof(T);

		if (!stores.TryGetValue(key, out var store)) {
			store = ComponentStore.For<T>();
			stores.Add(key, store);
		}

		store.Set(entity, component);
	}

	public void RemoveComponent<T>(Entity entity) where T : notnull {
		if (stores.TryGetValue(typeof(T), out var store)) {
			store.Unset(entity);
		}
	}

	public ComponentStore GetStore<T>() where T : notnull {
		var key = typeof(T);
		if (!stores.TryGetValue(key, out var store)) {
			store = ComponentStore.For<T>();
			stores.Add(key, store);
		}
		return store;
	}

	public void ForEach<TA>(RefAction1<TA> action)
		where TA : notnull {
		var aStore = GetStore<TA>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			ref var a = ref aStore.GetUnchecked<TA>(entity);

			action(entity, ref a);
		}
	}

	public void ForEach<TA, TB>(RefAction2<TA, TB> action)
	where TA : notnull
	where TB : notnull {
		var aStore = GetStore<TA>();
		var bStore = GetStore<TB>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			if (!bStore.Has(entity))
				continue;

			ref var a = ref aStore.GetUnchecked<TA>(entity);
			ref var b = ref bStore.GetUnchecked<TB>(entity);

			action(entity, ref a, ref b);
		}
	}

	public void ForEach<TA, TB, TC>(RefAction3<TA, TB, TC> action)
	where TA : notnull
	where TB : notnull
	where TC : notnull {
		var aStore = GetStore<TA>();
		var bStore = GetStore<TB>();
		var cStore = GetStore<TC>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			if (!bStore.Has(entity)) continue;
			if (!cStore.Has(entity)) continue;

			ref var a = ref aStore.GetUnchecked<TA>(entity);
			ref var b = ref bStore.GetUnchecked<TB>(entity);
			ref var c = ref cStore.GetUnchecked<TC>(entity);

			action(entity, ref a, ref b, ref c);
		}
	}

	public void ForEach<TA, TB, TC, TD>(RefAction4<TA, TB, TC, TD> action)
	where TA : notnull
	where TB : notnull
	where TC : notnull
	where TD : notnull {
		var aStore = GetStore<TA>();
		var bStore = GetStore<TB>();
		var cStore = GetStore<TC>();
		var dStore = GetStore<TD>();

		for (int i = 0; i < aStore.Length; i++) {
			var entity = aStore.GetEntityFromDenseIdx(i);

			if (!bStore.Has(entity)) continue;
			if (!cStore.Has(entity)) continue;
			if (!dStore.Has(entity)) continue;

			ref var a = ref aStore.GetUnchecked<TA>(entity);
			ref var b = ref bStore.GetUnchecked<TB>(entity);
			ref var c = ref cStore.GetUnchecked<TC>(entity);
			ref var d = ref dStore.GetUnchecked<TD>(entity);

			action(entity, ref a, ref b, ref c, ref d);
		}
	}
}
