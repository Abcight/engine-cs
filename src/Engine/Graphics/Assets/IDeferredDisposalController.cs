using Engine;
using Engine.Graphics.Shaders;

namespace Engine.Graphics.Assets;

internal interface IDeferredDisposalController {
	Result<GraphicsError> SetAllowDisposal(bool allow);
}