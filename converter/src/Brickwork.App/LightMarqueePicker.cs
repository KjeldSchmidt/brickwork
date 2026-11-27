using Brickwork.Core.Geometry;
using Brickwork.Core.Models;

namespace Brickwork.App;

public static class LightMarqueePicker
{
    public static IReadOnlyList<int> PickLightIds(
        MapDocument map,
        double left,
        double top,
        double right,
        double bottom)
    {
        var transform = SceneTransform.ForMapPreview(map);
        if (transform is null || right - left < 1e-6 || bottom - top < 1e-6)
        {
            return [];
        }

        var ids = new List<int>();
        foreach (var light in map.Lights)
        {
            var preview = transform.SceneToPreview(light.Position);
            if (preview.X >= left && preview.X <= right && preview.Y >= top && preview.Y <= bottom)
            {
                ids.Add(light.EntityId);
            }
        }

        return ids;
    }
}
