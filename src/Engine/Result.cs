namespace Engine;

public abstract record Result<T, E>
	where T : notnull
	where E : notnull {

	public sealed record Ok(T Value) : Result<T, E>;
	public sealed record Err(E Error) : Result<T, E>;

	public bool IsOk => this is Ok;
	public bool IsErr => this is Err;

	public TResult Match<TResult>(Func<T, TResult> ok, Func<E, TResult> err) {
		ArgumentNullException.ThrowIfNull(ok);
		ArgumentNullException.ThrowIfNull(err);
		return this switch {
			Ok(var value) => ok(value),
			Err(var error) => err(error),
			_ => throw new InvalidOperationException("Invalid result state.")
		};
	}

	public void Switch(Action<T> ok, Action<E> err) {
		ArgumentNullException.ThrowIfNull(ok);
		ArgumentNullException.ThrowIfNull(err);
		switch (this) {
			case Ok(var value):
				ok(value);
				return;
			case Err(var error):
				err(error);
				return;
			default:
				throw new InvalidOperationException("Invalid result state.");
		}
	}

	public bool TryOk(out T value) {
		if (this is Ok(var okValue)) {
			value = okValue;
			return true;
		}

		value = default!;
		return false;
	}

	public bool TryErr(out E error) {
		if (this is Err(var errValue)) {
			error = errValue;
			return true;
		}

		error = default!;
		return false;
	}

	public static implicit operator Result<T, E>(T value) => new Ok(value);
	public static implicit operator Result<T, E>(E error) => new Err(error);
}
