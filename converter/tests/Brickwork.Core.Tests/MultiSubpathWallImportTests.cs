using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;
using Xunit;

namespace Brickwork.Core.Tests;

public class MultiSubpathWallImportTests
{
    // Outer square 0..100 and inner square 40..60: a ring-shaped tunnel.
    private const string RingPath = "M0,0 L100,0 L100,100 L0,100 Z M40,40 L60,40 L60,60 L40,60 Z";

    [Fact]
    public void ParseToLocalSubpaths_SplitsOnMoveCommands()
    {
        var subpaths = InkSvgPathParser.ParseToLocalSubpaths(RingPath);

        Assert.Equal(2, subpaths.Count);
        Assert.Equal(4, subpaths[0].Count);
        Assert.Equal(4, subpaths[1].Count);
        Assert.Equal(new MapPoint(40, 40), subpaths[1][0]);
    }

    [Fact]
    public void Import_RingPath_CreatesSeparateOuterAndInnerWalls()
    {
        var map = Parse(RingPath, groupId: null);

        Assert.Equal(2, map.Walls.Count);
        var outer = Assert.Single(map.Walls, wall => wall.EntityId == 5);
        var inner = Assert.Single(map.Walls, wall => wall.EntityId != 5);

        Assert.Equal("Tunnel", outer.Name);
        Assert.Equal("Tunnel (inner)", inner.Name);
        Assert.True(outer.IsClosed);
        Assert.True(inner.IsClosed);
        Assert.All(outer.Points, point => Assert.True(point.X is 0 or 100 && point.Y is 0 or 100));
        Assert.All(inner.Points, point => Assert.True(point.X is 40 or 60 && point.Y is 40 or 60));
    }

    [Fact]
    public void Import_RingPath_LongestSubpathKeepsIdEvenWhenDrawnSecond()
    {
        var map = Parse("M40,40 L60,40 L60,60 L40,60 Z M0,0 L100,0 L100,100 L0,100 Z", groupId: null);

        var outer = Assert.Single(map.Walls, wall => wall.EntityId == 5);
        Assert.Contains(new MapPoint(100, 100), outer.Points);
    }

    [Fact]
    public void Import_RingPathInGroup_AddsSplitWallToGroup()
    {
        var map = Parse(RingPath, groupId: 9);

        var group = Assert.Single(map.Groups);
        var inner = Assert.Single(map.Walls, wall => wall.EntityId != 5);
        Assert.NotEqual(9, inner.EntityId);
        Assert.Equal(9, inner.GroupId);
        Assert.Contains(inner.EntityId, group.MemberIds);
    }

    [Fact]
    public void Import_SingleSubpath_IsUnchanged()
    {
        var map = Parse("M0,0 L100,0 L100,100 L0,100 Z", groupId: null);

        var wall = Assert.Single(map.Walls);
        Assert.Equal(4, wall.Points.Count);
    }

    private static MapDocument Parse(string paths, int? groupId)
    {
        var groupItem = groupId is int id
            ? $$"""
                {
                  "layerId": "layer-a",
                  "entity": { "entityType": "group", "entityId": {{id}} }
                },
                """
            : string.Empty;
        var groupProperty = groupId is int gid ? $", \"groupId\": {gid}" : string.Empty;
        var json = $$"""
            {
              "title": "ring",
              "version": 3,
              "history": [
                {
                  "transactionId": 1,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    {{groupItem}}
                    {
                      "layerId": "layer-a",
                      "entity": {
                        "entityType": "path-v2",
                        "entityId": 5,
                        "name": "Tunnel",
                        "wallEnabled": true,
                        "isClosedPath": true,
                        "x": 0,
                        "y": 0,
                        "scale": 1,
                        "paths": "{{paths}}"{{groupProperty}}
                      }
                    }
                  ]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        return InkarnateDocumentParser.Parse(document.RootElement);
    }
}
