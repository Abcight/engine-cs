namespace ExampleGame;

internal static class ExampleCatalog {
	private static readonly ExampleDefinition[] Definitions = [
		new("triangle", static () => new RainbowTriangleExample()),
		new("cube", static () => new SpinningCubeExample()),
		new("rendering-pbr", static () => new RenderingPbrSceneExample()),
		new("rendering-custom", static () => new RenderingCustomMaterialExample())
	];

	public static IExample Create(string[] args, out string? selectionMessage) {
		string requestedId = args.Length > 0
			? args[0].Trim().ToLowerInvariant()
			: "triangle";

		foreach (ExampleDefinition definition in Definitions) {
			if (string.Equals(definition.Id, requestedId, StringComparison.OrdinalIgnoreCase)) {
				selectionMessage = args.Length == 0
					? $"Running default example '{definition.Id}'. Available examples: {DescribeAvailableExamples()}."
					: $"Running example '{definition.Id}'. Available examples: {DescribeAvailableExamples()}.";
				return definition.Factory();
			}
		}

		selectionMessage =
			$"Unknown example '{requestedId}'. Falling back to 'triangle'. Available examples: {DescribeAvailableExamples()}.";
		return new RainbowTriangleExample();
	}

	private static string DescribeAvailableExamples() {
		return string.Join(", ", Definitions.Select(static definition => definition.Id));
	}

	private sealed record ExampleDefinition(string Id, Func<IExample> Factory);
}
