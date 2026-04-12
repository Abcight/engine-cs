using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine.ECS;

public class ComponentStore {
	private const int DEFAULT_CAPACITY = 128;
	private const int SPARSE_NULL = int.MaxValue;

	private DenseStore dense;
	private int[] sparse_to_dense = new int[DEFAULT_CAPACITY];
	private int[] dense_to_sparse = new int[DEFAULT_CAPACITY];
	private int[] sparse_generations = new int[DEFAULT_CAPACITY];

	private ComponentStore(DenseStore dense) {
		this.dense = dense;
		Array.Fill(sparse_to_dense, SPARSE_NULL);
		Array.Fill(sparse_generations, SPARSE_NULL);
		Array.Fill(dense_to_sparse, SPARSE_NULL);
	}

	public static ComponentStore For<T>() where T : unmanaged {
		int stride = Unsafe.SizeOf<T>();
		var dense = new DenseStore(stride);
		return new ComponentStore(dense);
	}

	public int Length => dense.Length;

	public void Set<T>(Entity entity, T value) where T : unmanaged {
		var index = entity.index;
		ensureSparseCapacity(index + 1);
		sparse_generations[index] = entity.generation;

		var dense_index = sparse_to_dense[index];
		if (dense_index == SPARSE_NULL) {
			dense_index = dense.Length;
			sparse_to_dense[index] = dense_index;
		}
		dense_to_sparse[dense_index] = index;
		dense.Set<T>(dense_index, value);
	}

	public void Unset(Entity entity) {
		var index = entity.index;

		if (!Has(entity))
			return;

		int denseIndex = sparse_to_dense[index];
		int lastIndex = dense.Length - 1;

		sparse_to_dense[index] = SPARSE_NULL;
		sparse_generations[index] = SPARSE_NULL;

		if (denseIndex == lastIndex) {
			dense.RemoveLast();
			dense_to_sparse[lastIndex] = SPARSE_NULL;
			return;
		}

		dense.Swap(denseIndex, lastIndex);

		int movedEntityIndex = dense_to_sparse[lastIndex];
		sparse_to_dense[movedEntityIndex] = denseIndex;
		dense_to_sparse[denseIndex] = movedEntityIndex;
		dense_to_sparse[lastIndex] = SPARSE_NULL;

		dense.RemoveLast();
	}

	public bool Has(Entity entity) {
		var index = entity.index;
		if (index >= sparse_to_dense.Length)
			return false;
		if (index >= sparse_generations.Length)
			return false;
		return sparse_to_dense[index] != SPARSE_NULL && sparse_generations[index] == entity.generation;
	}

	public ref T Get<T>(Entity entity) where T : unmanaged {
		if (!Has(entity))
			throw new InvalidOperationException();

		return ref dense.Get<T>(sparse_to_dense[entity.index]);
	}

	public Entity GetEntityFromDenseIdx(int denseIndex) {
		return new Entity(dense_to_sparse[denseIndex], sparse_generations[dense_to_sparse[denseIndex]]);
	}

	private void ensureSparseCapacity(int length) {
		if (length > sparse_to_dense.Length) {
			var old_start = sparse_to_dense.Length;
			Array.Resize(ref sparse_to_dense, length);
			for (int i = old_start; i < sparse_to_dense.Length; i++) {
				sparse_to_dense[i] = SPARSE_NULL;
			}
		}

		if (length > dense_to_sparse.Length) {
			var old_start = dense_to_sparse.Length;
			Array.Resize(ref dense_to_sparse, length);
			for (int i = old_start; i < dense_to_sparse.Length; i++) {
				dense_to_sparse[i] = SPARSE_NULL;
			}
		}

		if (length > sparse_generations.Length) {
			var old_start = sparse_generations.Length;
			Array.Resize(ref sparse_generations, length);
			for (int i = old_start; i < sparse_generations.Length; i++) {
				sparse_generations[i] = SPARSE_NULL;
			}
		}
	}

	private class DenseStore {
		private byte[] buffer = [];
		private int count = 0;
		private readonly int stride;

		public int Length => count;

		public DenseStore(int stride) {
			this.stride = stride;
		}

		public void Set<T>(int index, T value) where T : unmanaged {
			var needed = (index + 1) * stride;
			if (needed > buffer.Length) {
				Array.Resize(ref buffer, Math.Max(buffer.Length * 2, needed));
			}
			count = Math.Max(index + 1, count);
			var span = buffer.AsSpan(index * stride, stride);
			MemoryMarshal.Write(span, value);
		}

		public ref T Get<T>(int index) where T : unmanaged {
			var span = buffer.AsSpan(index * stride, stride);
			return ref MemoryMarshal.AsRef<T>(span);
		}

		public void Swap(int a, int b) {
			if (a == b) return;

			var spanA = buffer.AsSpan(a * stride, stride);
			var spanB = buffer.AsSpan(b * stride, stride);

			byte tmp;

			for (int i = 0; i < stride; i++) {
				tmp = spanA[i];
				spanA[i] = spanB[i];
				spanB[i] = tmp;
			}
		}

		public void RemoveLast() {
			count--;
		}
	}
}