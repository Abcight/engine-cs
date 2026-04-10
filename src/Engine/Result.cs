using System.Diagnostics.CodeAnalysis;

namespace Engine;

public abstract record Result<T, E>
	where T : class
	where E : class {

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
	[MemberNotNullWhen(true, nameof(Value))]
	[MemberNotNullWhen(false, nameof(ErrVariant))]
	[MemberNotNullWhen(false, nameof(Error))]
	public bool IsOk => this is Ok;

	[MemberNotNullWhen(true, nameof(ErrVariant))]
	[MemberNotNullWhen(true, nameof(Error))]
	[MemberNotNullWhen(false, nameof(OkVariant))]
	[MemberNotNullWhen(false, nameof(Value))]
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

	public static implicit operator Result<T, E>(T value) => new Ok(value);
	public static implicit operator Result<T, E>(E error) => new Err(error);
}
