using System.Diagnostics.CodeAnalysis;

namespace Engine;

public abstract record Result<T, E> {

	private Result() {
	}

	public sealed record Ok : Result<T, E> {
		public Ok(T value) {
			Value = value;
		}

		public new T Value { get; }

		public void Deconstruct(out T value) {
			value = Value;
		}
	}

	public sealed record Err : Result<T, E> {
		public Err(E error) {
			Error = error;
		}

		public new E Error { get; }

		public void Deconstruct(out E error) {
			error = Error;
		}
	}

	[MemberNotNullWhen(true, nameof(OkVariant))]
	[MemberNotNullWhen(false, nameof(ErrVariant))]
	public bool IsOk => this is Ok;

	[MemberNotNullWhen(true, nameof(ErrVariant))]
	[MemberNotNullWhen(false, nameof(OkVariant))]
	public bool IsErr => this is Err;

	public Ok? OkVariant => this as Ok;
	public Err? ErrVariant => this as Err;

	public T? Value => OkVariant is { } ok ? ok.Value : default;
	public E? Error => ErrVariant is { } err ? err.Error : default;

	public Ok? TryOk() => OkVariant;
	public Err? TryErr() => ErrVariant;

	public void Deconstruct(out bool isOk, out Ok? ok, out Err? err) {
		isOk = IsOk;
		ok = OkVariant;
		err = ErrVariant;
	}

	public T Expect(string message) =>
		this is Ok ok
			? ok.Value
			: throw new InvalidOperationException($"{message}: {((Err)this).Error}");

	public T ValueOr(T fallback) =>
		this is Ok ok ? ok.Value : fallback;

	public Result<U, E> AndThen<U>(Func<T, Result<U, E>> f) =>
		this is Ok ok ? f(ok.Value) : ((Err)this).Error;

	public Result<U, E> Map<U>(Func<T, U> f) =>
		this is Ok ok ? f(ok.Value) : ((Err)this).Error;

	public static implicit operator Result<T, E>(T value) => new Ok(value);
	public static implicit operator Result<T, E>(E error) => new Err(error);
}

public abstract record Result<E> {

	private Result() {
	}

	public sealed record Ok : Result<E> {
		public static Ok Instance { get; } = new();
	}

	public sealed record Err : Result<E> {
		public Err(E error) {
			Error = error;
		}

		public new E Error { get; }

		public void Deconstruct(out E error) {
			error = Error;
		}
	}

	[MemberNotNullWhen(true, nameof(ErrVariant))]
	public bool IsErr => this is Err;

	public bool IsOk => this is Ok;

	public Err? ErrVariant => this as Err;

	public E? Error => ErrVariant is { } err ? err.Error : default;

	public Err? TryErr() => ErrVariant;

	public void Expect(string message) {
		if (this is Err err) {
			throw new InvalidOperationException($"{message}: {err.Error}");
		}
	}

	public Result<E> AndThen(Func<Result<E>> f) =>
		this is Ok ? f() : this;

	public Result<T, E> AndThen<T>(Func<Result<T, E>> f) =>
		this is Ok ? f() : ((Err)this).Error;

	public static implicit operator Result<E>(E error) => new Err(error);
	public static implicit operator Result<E>(Unit _) => Ok.Instance;

	public static implicit operator Result<Unit, E>(Result<E> result) =>
		result is Ok ? (Result<Unit, E>)Unit.Value : ((Err)result).Error;

	public static implicit operator Result<E>(Result<Unit, E> result) =>
		result.IsOk ? Ok.Instance : (Result<E>)result.Error!;
}
