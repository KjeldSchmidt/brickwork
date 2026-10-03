namespace Brickwork.Core.Models;

public static class MapLayerEditing
{
    public const string DefaultLayerId = "layer-default";
    public const string DefaultLayerName = "Default";

    public static MapLayer EnsureDefaultLayer(MapDocument map)
    {
        var existing = map.Layers.FirstOrDefault(layer =>
            string.Equals(layer.Id, DefaultLayerId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.Name))
            {
                existing.Name = DefaultLayerName;
            }

            return existing;
        }

        var layer = new MapLayer
        {
            Id = DefaultLayerId,
            Name = DefaultLayerName,
            IsVisible = true,
            Order = map.Layers.Count == 0 ? 0 : map.Layers.Max(candidate => candidate.Order) + 1,
        };
        map.Layers.Add(layer);
        return layer;
    }
}
