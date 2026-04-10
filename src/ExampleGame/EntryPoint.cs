using ExampleGame;

DemoShaderBinding binding = new();
binding.MyShaderProperty = 0.5f;

Console.WriteLine($"Generated shader binding test. MyShaderProperty={binding.MyShaderProperty}");