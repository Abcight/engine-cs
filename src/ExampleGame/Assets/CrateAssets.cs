using Engine.Graphics.Assets;
using Engine.Graphics.Resources;

namespace ExampleGame.Assets;

[AssetGroup]
public partial class CrateAssets {
	[Asset(
		"res/textures/crate_pbr/Stylized_Crate_002_basecolor.jpg",
		GenerateMipmaps = true,
		FlipVertically = true,
		Label = "Crate.BaseColor"
	)]
	public AssetHandle<Texture2D> BaseColor;

	[Asset(
		"res/textures/crate_pbr/Stylized_Crate_002_metallic.jpg",
		GenerateMipmaps = true,
		FlipVertically = true,
		Label = "Crate.Metallic"
	)]
	public AssetHandle<Texture2D> Metallic;

	[Asset(
		"res/textures/crate_pbr/Stylized_Crate_002_normal.jpg",
		GenerateMipmaps = true,
		FlipVertically = true,
		Label = "Crate.Normal"
	)]
	public AssetHandle<Texture2D> Normal;

	[Asset(
		"res/textures/crate_pbr/Stylized_Crate_002_roughness.jpg",
		GenerateMipmaps = true,
		FlipVertically = true,
		Label = "Crate.Roughness"
	)]
	public AssetHandle<Texture2D> Roughness;

	[Asset(
		"res/textures/crate_pbr/Stylized_Crate_002_ambientOcclusion.jpg",
		GenerateMipmaps = true,
		FlipVertically = true,
		Label = "Crate.AmbientOcclusion"
	)]
	public AssetHandle<Texture2D> Occlusion;
}