using System;

namespace Engine.ECS;

public interface ISystem<T> where T : ISystem<T> {
	static abstract void Update(ref World world);
}