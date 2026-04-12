using Engine;
using Engine.Graphics.Shaders;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics.Backend.OpenGL;

internal static class GLGC {
	private const int ForcedDrainTokenThreshold = 2048;
	private const long ForcedDrainBytesThreshold = 256L * 1024L * 1024L;

	private static readonly object GlobalGate = new();
	private static readonly Dictionary<int, Bucket> Buckets = new();
	private static int _nextBucketId;

	public static int RegisterBucket() {
		lock (GlobalGate) {
			int bucketId = ++_nextBucketId;
			Buckets[bucketId] = new Bucket(Environment.CurrentManagedThreadId);
			return bucketId;
		}
	}

	public static Result<GraphicsError> SetAllowDisposal(int bucketId, bool allow) {
		Bucket? bucket = GetBucket(bucketId);
		if (bucket is null) {
			return GraphicsError.InvalidContext($"OpenGL disposal bucket '{bucketId}' does not exist.");
		}

		lock (bucket.Gate) {
			bucket.AllowDisposal = allow;
		}

		return Unit.Value;
	}

	public static void Enqueue(int bucketId, DeletionKind kind, int handle, int estimatedBytes = 0) {
		if (handle == 0) {
			return;
		}

		Bucket? bucket = GetBucket(bucketId);
		if (bucket is null) {
			return;
		}

		int clampedBytes = Math.Max(0, estimatedBytes);
		lock (bucket.Gate) {
			bucket.Queue.Enqueue(new DeletionToken(kind, handle, clampedBytes));
			bucket.QueuedCount++;
			bucket.QueuedBytes += clampedBytes;
		}
	}

	public static void Drain(int bucketId, bool force = false) {
		Bucket? bucket = GetBucket(bucketId);
		if (bucket is null) {
			return;
		}

		var tokens = new List<DeletionToken>();
		bool warnForcedDrain = false;
		lock (bucket.Gate) {
			if (bucket.QueuedCount == 0) {
				return;
			}

			bool thresholdExceeded = bucket.QueuedCount >= ForcedDrainTokenThreshold
				|| bucket.QueuedBytes >= ForcedDrainBytesThreshold;
			bool shouldDrain = force || bucket.AllowDisposal || thresholdExceeded;
			if (!shouldDrain) {
				return;
			}

			warnForcedDrain = !force && !bucket.AllowDisposal && thresholdExceeded;
			while (bucket.Queue.Count > 0) {
				tokens.Add(bucket.Queue.Dequeue());
			}

			bucket.QueuedCount = 0;
			bucket.QueuedBytes = 0;
		}

		if (warnForcedDrain) {
			Console.Error.WriteLine(
				$"[glgc] Forced disposal drain for bucket {bucketId} on thread "
				+ $"{Environment.CurrentManagedThreadId} (owner thread {bucket.OwnerThreadId})."
			);
		}

		foreach (DeletionToken token in tokens) {
			TryDelete(token);
		}
	}

	private static Bucket? GetBucket(int bucketId) {
		lock (GlobalGate) {
			Buckets.TryGetValue(bucketId, out Bucket? bucket);
			return bucket;
		}
	}

	private static void TryDelete(DeletionToken token) {
		try {
			switch (token.Kind) {
				case DeletionKind.Buffer:
					if (GL.IsBuffer(token.Handle)) {
						GL.DeleteBuffer(token.Handle);
					}
					break;
				case DeletionKind.Texture:
					if (GL.IsTexture(token.Handle)) {
						GL.DeleteTexture(token.Handle);
					}
					break;
				case DeletionKind.Program:
					if (GL.IsProgram(token.Handle)) {
						GL.DeleteProgram(token.Handle);
					}
					break;
				case DeletionKind.VertexArray:
					if (GL.IsVertexArray(token.Handle)) {
						GL.DeleteVertexArray(token.Handle);
					}
					break;
				case DeletionKind.Framebuffer:
					if (GL.IsFramebuffer(token.Handle)) {
						GL.DeleteFramebuffer(token.Handle);
					}
					break;
				case DeletionKind.Renderbuffer:
					if (GL.IsRenderbuffer(token.Handle)) {
						GL.DeleteRenderbuffer(token.Handle);
					}
					break;
			}
		} catch (Exception exception) {
			Console.Error.WriteLine(
				$"[glgc] Failed to delete OpenGL {token.Kind} handle {token.Handle}: {exception.Message}"
			);
		}
	}

	internal enum DeletionKind {
		Buffer,
		Texture,
		Program,
		VertexArray,
		Framebuffer,
		Renderbuffer
	}

	private readonly record struct DeletionToken(DeletionKind Kind, int Handle, int EstimatedBytes);

	private sealed class Bucket {
		public Bucket(int ownerThreadId) {
			OwnerThreadId = ownerThreadId;
		}

		public object Gate { get; } = new();
		public Queue<DeletionToken> Queue { get; } = new();
		public bool AllowDisposal { get; set; } = true;
		public int QueuedCount { get; set; }
		public long QueuedBytes { get; set; }
		public int OwnerThreadId { get; }
	}
}
