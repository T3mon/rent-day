using Godot;

namespace RentDay;

// Builds the map goons find their way around with: the whole floor, minus walls and
// furniture. Doors don't count as obstacles, because goons walk through them (pushing
// them open). Godot works out the walkable area itself ("baking"), keeping AgentRadius
// pixels away from every obstacle so a goon's body fits.
public static class LevelNavigation
{
	private const string SourceGroup = "navigation_source";

	// floorArea is in the level's own coordinates.
	public static void Build(Node2D level, Rect2 floorArea)
	{
		// The baker looks for walls and furniture in this group (and everything under it).
		level.AddToGroup(SourceGroup);

		var map = new NavigationPolygon
		{
			AgentRadius = 9f,
			ParsedGeometryType = NavigationPolygon.ParsedGeometryTypeEnum.StaticColliders,
			ParsedCollisionMask = Layers.World | Layers.Furniture,
			SourceGeometryMode = NavigationPolygon.SourceGeometryModeEnum.GroupsWithChildren,
			SourceGeometryGroupName = SourceGroup,
		};
		map.AddOutline(new[]
		{
			floorArea.Position,
			new Vector2(floorArea.End.X, floorArea.Position.Y),
			floorArea.End,
			new Vector2(floorArea.Position.X, floorArea.End.Y),
		});

		var region = new NavigationRegion2D { Name = "Navigation", NavigationPolygon = map };
		level.AddChild(region);
		region.BakeNavigationPolygon(false);
	}
}
