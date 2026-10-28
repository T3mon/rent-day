using System.Collections.Generic;
using Godot;

namespace RentDay;

// The map enemies find their way around with: graph paper laid over the floor, 8 px squares,
// each open or closed. A square is closed if an enemy standing in its middle would come closer
// than Room pixels to a wall, furniture or the edge of the floor. Doors don't count: enemies
// walk through them, pushing them open.
// A route runs square to square (Godot's AStarGrid2D), then gets pulled straight wherever an
// enemy fits all the way, so he cuts across a room instead of zig-zagging.
// Main builds it once per level; enemies use the static calls.
public static class LevelGrid
{
	private const int Cell = 8;
	private const float Room = 10f; // an enemy's body (8 px) plus a little to spare, so he never scrapes a corner
	private const float Body = 7.5f; // an enemy's body, a hair under, so touching a wall isn't being in it

	private static Node2D _level;
	private static Rect2 _floor;
	private static ulong _readyFrame;
	private static AStarGrid2D _grid;
	private static string _gridFor; // the floor scene _grid was made for
	private static PhysicsShapeQueryParameters2D _query;
	private static readonly PhysicsShapeQueryParameters2D _bodyQuery = new() { Shape = new CircleShape2D { Radius = Body } };

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

	// Is there room for an enemy here? Before the map is ready (the first frames), everywhere is open.
	public static bool IsOpen(Vector2 at) => !Ready() || IsOpen(CellAt(at));

	// The corners to walk through from `from` to `to`, ending at `to` (or as near as an enemy fits).
	// Empty if there's no way there; null if the map isn't ready yet.
	// Call it from _PhysicsProcess: it asks the physics engine what's in the way.
	public static List<Vector2> Path(Vector2 from, Vector2 to)
	{
		if (!Ready())
			return null;
		var route = new List<Vector2>();
		// Too close to a wall to stand on the map: start from an open square he can walk straight onto.
		if (NearestReachable(from, Layers.World | Layers.Furniture) is not Vector2 first
			|| NearestOpen(CellAt(to)) is not Vector2I end)
			return route;
		Vector2I start = CellAt(first);
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

	// Is an enemy's body here pressed into a wall or furniture (`mask`), rather than just close to one?
	public static bool Wedged(Vector2 at, uint mask)
	{
		if (!Ready())
			return false;
		_bodyQuery.Transform = new Transform2D(0f, at);
		_bodyQuery.CollisionMask = mask;
		return _level.GetWorld2D().DirectSpaceState.IntersectShape(_bodyQuery, 1).Count > 0;
	}

	// Where an enemy wedged in something steps out to: the middle of the nearest open square he can walk
	// straight onto without going through a wall (furniture doesn't count: he may be sitting in it).
	public static Vector2? StepOut(Vector2 from) => NearestReachable(from, Layers.World);

	// The nearest open square, up to 8 squares away, whose middle a straight line from `from` reaches
	// without crossing `blockers`; null if none.
	private static Vector2? NearestReachable(Vector2 from, uint blockers)
	{
		if (!Ready())
			return null;
		Vector2I cell = CellAt(from);
		for (int ring = 0; ring <= 8; ring++)
		{
			Vector2? best = null;
			for (int x = -ring; x <= ring; x++)
			{
				for (int y = -ring; y <= ring; y++)
				{
					Vector2I near = cell + new Vector2I(x, y);
					if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != ring || !IsOpen(near))
						continue;
					Vector2 middle = Middle(near);
					if ((best == null || from.DistanceSquaredTo(middle) < from.DistanceSquaredTo(best.Value)) && !Between(from, middle, blockers))
						best = middle;
				}
			}
			if (best != null)
				return best;
		}
		return null;
	}

	// How far an enemy walks from `from` to `to`, square by square: straight-line distance before the
	// map is ready, infinity if there's no way.
	public static float WalkLength(Vector2 from, Vector2 to)
	{
		if (!Ready())
			return from.DistanceTo(to);
		if (NearestOpen(CellAt(from)) is not Vector2I start || NearestOpen(CellAt(to)) is not Vector2I end)
			return float.PositiveInfinity;
		Godot.Collections.Array<Vector2I> cells = _grid.GetIdPath(start, end);
		if (cells.Count == 0)
			return float.PositiveInfinity;
		float length = 0f;
		for (int i = 1; i < cells.Count; i++)
			length += ((Vector2)(cells[i] - cells[i - 1])).Length() * Cell;
		return length;
	}

	// Is there a wall (not furniture, not a door) on the straight line from a to b?
	public static bool WallBetween(Vector2 a, Vector2 b) => Between(a, b, Layers.World);

	private static bool Between(Vector2 a, Vector2 b, uint mask) =>
		_level != null && _level.GetWorld2D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters2D.Create(a, b, mask)).Count > 0;

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

	// Would an enemy (plus Room) fit all the way from a to b, touching no wall or furniture?
	private static bool Fits(Vector2 a, Vector2 b)
	{
		_query.Transform = new Transform2D(0f, a);
		_query.Motion = b - a;
		return _level.GetWorld2D().DirectSpaceState.IntersectShape(_query, 1).Count == 0;
	}

	private static Vector2I CellAt(Vector2 at) => (Vector2I)(at / Cell).Floor();

	private static Vector2 Middle(Vector2I cell) => cell * Cell + new Vector2(Cell / 2f, Cell / 2f);
}
