namespace Engine.ECS;

public sealed class ComponentStore {
	private const int DEFAULT_CAPACITY = 128;
	private const int SPARSE_NULL = int.MaxValue;

	private readonly Type componentType;
	private readonly IDenseStore dense;
	private int[] sparseToDense = new int[DEFAULT_CAPACITY];
	private int[] denseToSparse = new int[DEFAULT_CAPACITY];
	private int[] sparseGenerations = new int[DEFAULT_CAPACITY];

	private ComponentStore(Type componentType, IDenseStore dense) {
		this.componentType = componentType;
		this.dense = dense;
		Array.Fill(sparseToDense, SPARSE_NULL);
		Array.Fill(sparseGenerations, SPARSE_NULL);
		Array.Fill(denseToSparse, SPARSE_NULL);
	}

	public static ComponentStore For<T>() where T : notnull {
		return new ComponentStore(typeof(T), new DenseStore<T>());
	}

	public int Length => dense.Length;

	public void Set<T>(Entity entity, T value) where T : notnull {
		DenseStore<T> typedDense = getDenseStore<T>();
		var index = entity.index;
		if (index < 0 || entity.generation < 0) {
			return;
		}

		ensureSparseCapacity(index + 1);

		var denseIndex = sparseToDense[index];
		if (denseIndex != SPARSE_NULL) {
			if (sparseGenerations[index] != entity.generation) {
				return;
			}

			typedDense.Set(denseIndex, value);
			return;
		}

		denseIndex = typedDense.Length;
		ensureDenseCapacity(denseIndex + 1);
		sparseToDense[index] = denseIndex;
		denseToSparse[denseIndex] = index;
		sparseGenerations[index] = entity.generation;
		typedDense.Set(denseIndex, value);
	}

	public void Unset(Entity entity) {
		var index = entity.index;
		if (!Has(entity)) {
			return;
		}

		var denseIndex = sparseToDense[index];
		var lastIndex = dense.Length - 1;

		sparseToDense[index] = SPARSE_NULL;

		if (denseIndex == lastIndex) {
			dense.RemoveLast();
			denseToSparse[lastIndex] = SPARSE_NULL;
			return;
		}

		dense.Swap(denseIndex, lastIndex);

		var movedEntityIndex = denseToSparse[lastIndex];
		sparseToDense[movedEntityIndex] = denseIndex;
		denseToSparse[denseIndex] = movedEntityIndex;
		denseToSparse[lastIndex] = SPARSE_NULL;

		dense.RemoveLast();
	}

	public bool Has(Entity entity) {
		var index = entity.index;

		if (index >= sparseToDense.Length || index >= sparseGenerations.Length) {
			return false;
		}

		return sparseToDense[index] != SPARSE_NULL && sparseGenerations[index] == entity.generation;
	}

	public ref T GetUnchecked<T>(Entity entity) where T : notnull {
		return ref getDenseStore<T>().Get(sparseToDense[entity.index]);
	}

	public Entity GetEntityFromDenseIdx(int denseIndex) {
		var sparseIndex = denseToSparse[denseIndex];
		return new Entity(sparseIndex, sparseGenerations[sparseIndex]);
	}

	private void ensureSparseCapacity(int length) {
		if (length <= sparseToDense.Length) {
			return;
		}

		var newLength = Math.Max(length, sparseToDense.Length * 2);

		var oldSparseLength = sparseToDense.Length;
		Array.Resize(ref sparseToDense, newLength);
		for (int i = oldSparseLength; i < sparseToDense.Length; i++) {
			sparseToDense[i] = SPARSE_NULL;
		}

		var oldGenerationLength = sparseGenerations.Length;
		Array.Resize(ref sparseGenerations, newLength);
		for (int i = oldGenerationLength; i < sparseGenerations.Length; i++) {
			sparseGenerations[i] = SPARSE_NULL;
		}
	}

	private void ensureDenseCapacity(int length) {
		if (length <= denseToSparse.Length) {
			return;
		}

		var newLength = Math.Max(length, denseToSparse.Length * 2);
		var oldDenseLength = denseToSparse.Length;
		Array.Resize(ref denseToSparse, newLength);
		for (int i = oldDenseLength; i < denseToSparse.Length; i++) {
			denseToSparse[i] = SPARSE_NULL;
		}
	}

	private DenseStore<T> getDenseStore<T>() where T : notnull {
		if (componentType != typeof(T)) {
			throw new InvalidOperationException(
				$"Component store mismatch. Store is '{componentType.Name}', requested '{typeof(T).Name}'."
			);
		}

		return (DenseStore<T>)dense;
	}

	private interface IDenseStore {
		int Length { get; }
		void Swap(int a, int b);
		void RemoveLast();
	}

	private sealed class DenseStore<T> : IDenseStore where T : notnull {
		private T[] values = [];
		private int count;

		public int Length => count;

		public void Set(int index, T value) {
			var needed = index + 1;
			if (needed > values.Length) {
				var grown = values.Length == 0 ? DEFAULT_CAPACITY : values.Length * 2;
				Array.Resize(ref values, Math.Max(grown, needed));
			}

			count = Math.Max(needed, count);
			values[index] = value;
		}

		public ref T Get(int index) {
			if (index < 0 || index >= count) {
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			return ref values[index];
		}

		public void Swap(int a, int b) {
			if (a == b) {
				return;
			}

			(values[a], values[b]) = (values[b], values[a]);
		}

		public void RemoveLast() {
			if (count > 0) {
				Array.Clear(values, count - 1, 1);
				count--;
			}
		}
	}
}
