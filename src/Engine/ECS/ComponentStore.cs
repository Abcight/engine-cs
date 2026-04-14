using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine.ECS;

public sealed class ComponentStore {
	private const int DEFAULT_CAPACITY = 128;
	private const int SPARSE_NULL = int.MaxValue;

	private readonly DenseStore dense;
	private int[] sparseToDense = new int[DEFAULT_CAPACITY];
	private int[] denseToSparse = new int[DEFAULT_CAPACITY];
	private int[] sparseGenerations = new int[DEFAULT_CAPACITY];

	private ComponentStore(DenseStore dense) {
		this.dense = dense;
		Array.Fill(sparseToDense, SPARSE_NULL);
		Array.Fill(sparseGenerations, SPARSE_NULL);
		Array.Fill(denseToSparse, SPARSE_NULL);
	}

	public static ComponentStore For<T>() where T : unmanaged {
		int stride = Unsafe.SizeOf<T>();
		var dense = new DenseStore(stride);
		return new ComponentStore(dense);
	}

	public int Length => dense.Length;

	public void Set<T>(Entity entity, T value) where T : unmanaged {
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

			dense.Set(denseIndex, value);
			return;
		}

		denseIndex = dense.Length;
		ensureDenseCapacity(denseIndex + 1);
		sparseToDense[index] = denseIndex;
		denseToSparse[denseIndex] = index;
		sparseGenerations[index] = entity.generation;
		dense.Set(denseIndex, value);
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

	public ref T GetUnchecked<T>(Entity entity) where T : unmanaged {
		return ref dense.Get<T>(sparseToDense[entity.index]);
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

	private sealed class DenseStore {
		private byte[] buffer = [];
		private int count;
		private readonly int stride;

		public int Length => count;

		public DenseStore(int stride) {
			this.stride = stride;
		}

		public void Set<T>(int index, T value) where T : unmanaged {
			var needed = (index + 1) * stride;
			if (needed > buffer.Length) {
				var grown = buffer.Length == 0 ? stride : buffer.Length * 2;
				Array.Resize(ref buffer, Math.Max(grown, needed));
			}

			count = Math.Max(index + 1, count);
			var span = buffer.AsSpan(index * stride, stride);
			MemoryMarshal.Write(span, in value);
		}

		public ref T Get<T>(int index) where T : unmanaged {
			if (index < 0 || index >= count) {
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			var span = buffer.AsSpan(index * stride, stride);
			return ref MemoryMarshal.AsRef<T>(span);
		}

		public void Swap(int a, int b) {
			if (a == b) {
				return;
			}

			var spanA = buffer.AsSpan(a * stride, stride);
			var spanB = buffer.AsSpan(b * stride, stride);

			for (int i = 0; i < stride; i++) {
				byte tmp = spanA[i];
				spanA[i] = spanB[i];
				spanB[i] = tmp;
			}
		}

		public void RemoveLast() {
			if (count > 0) {
				count--;
			}
		}
	}
}
