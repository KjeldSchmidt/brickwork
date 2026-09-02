using InkarnateTools.Core.Models;
using SkiaSharp;

namespace InkarnateTools.App.Rendering;

public interface IMapSceneRenderer
{
    void Render(SKCanvas canvas, MapDocument map, SKRect destinationBounds, MapRenderHighlight? highlight = null);

    void ReleaseMap(MapDocument map);
}
