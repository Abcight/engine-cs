namespace Engine.ECS;

public struct Entity {
	public int index;
	public int generation;
	public Entity(int index, int generation) {
		this.index = index;
		this.generation = generation;
	}
}
