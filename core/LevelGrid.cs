using System.Collections.Generic;
using Godot;

namespace RentDay;

// The map goons find their way around with: graph paper laid over the floor, 8 px squares,
// each open or closed. A square is closed if a goon standing in its middle would come closer
// than Room pixels to a wall, furniture or the edge of the floor. Doors don't count: goons
// walk through them, pushing them open.
// A route runs square to square (Godot's AStarGrid2D), then gets pulled straight wherever a
// goon fits all the way, so he cuts across a room instead of zig-zagging.
// Main builds it once per level; goons use the static calls.
public static class LevelGrid
{
	private const int Cell = 8;
	private const float Room = 10f; // a goon's body (8 px) plus a little to spare, so he never scrapes a corner

	private static Node2D _level;
	private static Rect2 _floor;
	private static ulong _readyFrame;
	private static AStarGrid2D _grid;
	private static string _gridFor; // the floor scene _grid was made for
	private static PhysicsShapeQueryParameters2D _query;

	// floorArea is in world pixels (the level's root sits at (0, 0)).
	public static void Build(Node2D level, Rect2 floorArea)
	{
		_level = level;
		if (_grid != null && level.SceneFilePath == _gridFor)
			return; // the same floor again (a restart): walls and furniture haven't moved, so the squares still hold
		_floor = floorArea;
		_grid = null;
		_readyFrame = Engine.GetPhysicsFrames() + 2; // walls are only solid to queries after a physics step, so the squares are checked on first use
		_query = new PhysicsShapeQueryParameters2D
		{
			Shape = new CircleShape2D { Radius = Room },
			CollisionMask = Layers.World | Layers.Furniture,
		};
	}

	// Is there room for a goon here? Before the map is ready (the first frames), everywhere is open.
	public static bool IsOpen(Vector2 at) => !Ready() || IsOpen(CellAt(at));

	// The corners to walk through from `from` to `to`, ending at `to` (or as near as a goon fits).
	// Empty if there's no way there; null if the map isn't ready yet.
	// Call it from _PhysicsProcess: it asks the physics engine what's in the way.
	public static List<Vector2> Path(Vector2 from, Vector2 to)
	{
		if (!Ready())
			return null;
		var route = new List<Vector2>();
		if (NearestOpen(CellAt(from)) is not Vector2I start || NearestOpen(CellAt(to)) is not Vector2I end)
			return route;
		Godot.Collections.Array<Vector2I> cells = _grid.GetIdPath(start, end);
		if (cells.Count == 0)
			return route;

		// Pull it straight: a square that can't be walked to in a straight line from the last
		// corner makes the square before it the next corner.
		var points = new List<Vector2>();
		foreach (Vector2I cell in cells)
			points.Add(Middle(cell));
		if (Fits(to, to))
			points[^1] = to;
		Vector2 corner = from;
		for (int i = 1; i < points.Count; i++)
		{
			if (!Fits(corner, points[i]))
			{
				corner = points[i - 1];
				route.Add(corner);
			}
		}
		route.Add(points[^1]);
		return route;
	}

	private static bool Ready()
	{
		if (_grid != null)
			return true;
		if (_level == null || Engine.GetPhysicsFrames() < _readyFrame)
			return false;

		_grid = new AStarGrid2D
		{
			Region = new Rect2I((Vector2I)(_floor.Position / Cell), (Vector2I)(_floor.Size / Cell)),
			DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, // never diagonally past a corner
		};
		_grid.Update();
		Rect2 inside = _floor.Grow(-Room);
		for (int x = _grid.Region.Position.X; x < _grid.Region.End.X; x++)
		{
			for (int y = _grid.Region.Position.Y; y < _grid.Region.End.Y; y++)
			{
				var cell = new Vector2I(x, y);
				Vector2 middle = Middle(cell);
				if (!inside.HasPoint(middle) || !Fits(middle, middle))
					_grid.SetPointSolid(cell);
			}
		}
		_gridFor = _level.SceneFilePath;
		return true;
	}

	private static bool IsOpen(Vector2I cell) => _grid.IsInBoundsv(cell) && !_grid.IsPointSolid(cell);

	// The open square nearest to `cell` (itself, if it's open), up to 4 squares away.
	private static Vector2I? NearestOpen(Vector2I cell)
	{
		for (int ring = 0; ring <= 4; ring++)
		{
			for (int x = -ring; x <= ring; x++)
			{
				for (int y = -ring; y <= ring; y++)
				{
					Vector2I near = cell + new Vector2I(x, y);
					if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == ring && IsOpen(near))
						return near;
				}
			}
		}
		return null;
	}

	// Would a goon (plus Room) fit all the way from a to b, touching no wall or furniture?
	private static bool Fits(Vector2 a, Vector2 b)
	{
		_query.Transform = new Transform2D(0f, a);
		_query.Motion = b - a;
		return _level.GetWorld2D().DirectSpaceState.IntersectShape(_query, 1).Count == 0;
	}

	private static Vector2I CellAt(Vector2 at) => (Vector2I)(at / Cell).Floor();

	private static Vector2 Middle(Vector2I cell) => cell * Cell + new Vector2(Cell / 2f, Cell / 2f);
}
