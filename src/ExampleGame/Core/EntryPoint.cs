using Engine;
using Engine.Graphics.Contexts;
using Engine.Graphics.Shaders;

namespace ExampleGame.Core;

public static class EntryPoint {
	public static void Main(string[] args) {
		var example = ExampleCatalog.Create(args, out string? selectionMessage);
		if (!string.IsNullOrWhiteSpace(selectionMessage)) {
			Console.WriteLine(selectionMessage);
		}

		var contextResult = GraphicsContextFactory.CreateWindow(
			new WindowRenderContextOptions(
				title: $"ExampleGame - {example.DisplayName}",
				width: 1280,
				height: 720
			)
		);

		if (contextResult.IsErr) {
			var error = contextResult.Error;
			Console.Error.WriteLine($"[context] {error.Code}: {error.Message}");
			return;
		}

		var context = contextResult.Value;
		using (context) {
			var callbacks = new WindowRenderCallbacks {
				OnLoad = example.OnLoad,
				OnUpdate = example.OnUpdate,
				OnRender = example.OnRender,
				OnResize = example.OnResize,
				OnUnload = example.OnUnload
			};

			var runResult = context.Run(callbacks);
			if (runResult.TryErr() is { Error: var runError }) {
				Console.Error.WriteLine($"[run] {runError.Code}: {runError.Message}");
			}
		}
	}
}