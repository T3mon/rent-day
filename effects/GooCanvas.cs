using System.Collections.Generic;
using Godot;

namespace RentDay;

// All the goo on a level's floor, painted into one canvas that never clears: a splat is
// drawn into it once and forgotten, so a thousand splats cost the same as one. The canvas
// shows on the goo layer (under bodies, walls and furniture); every splat gets its own fixed colour mix.
//   - Splats are clipped to the floor around where they land: they flow through doorways,
//     never through walls.
//   - Drops fly out of a hit, slow down, stop at walls and land as small splats.
//   - Pools spread under a body for a few seconds.
//   - Whoever walks through goo leaves footprints for a few steps.
// Main builds one per level; everything else uses the static calls, like ShakeCamera.Shake.
public partial class GooCanvas : Node2D
{
	private const int Cell = 8;               // clip grid cell: the size of a wall tile
	private const float DropDamping = 380f;   // how quickly a flying drop slows (pixels per second, per second)
	private const float PoolTime = 2.5f;      // seconds a pool keeps spreading
	private const uint WallStops = Layers.World | Layers.Doors; // what drops splash against

	private static readonly Texture2D Mask = GD.Load<Texture2D>("res://art/goo_mask.png");
	private static readonly Shader StampShader = GD.Load<Shader>("res://art/goo_stamp.gdshader");
	private static readonly Texture2D Dot = MakeDot(); // the splat texture shrunk this small breaks up, so drops and prints use a plain blob
	private static GooCanvas _active;

	private class Drop { public Vector2 Pos, Vel; public float Seed; }
	private class Pool { public Vector2 At; public float Size, Angle, Seed, Age, NextStamp; public ShaderMaterial Clip; }
	private record SplatOrder(Vector2 From, Vector2 Offset, float Size, float Angle, float Stretch, float Strength);

	private Rect2 _area;                       // the floor, in world pixels
	private SubViewport _canvas;
	private ShaderMaterial _noClip;
	private bool[,] _wall;                     // which grid cells are wall
	private float[,] _wet;                     // how much goo lies in each cell, for footprints
	private readonly List<Sprite2D> _painted = new();
	private readonly List<Drop> _drops = new();
	private readonly List<Pool> _pools = new();
	private readonly List<SplatOrder> _orders = new(); // placed on the next physics tick, when wall checks are allowed

	// floorArea is in world pixels (the level's root sits at (0, 0)).
	public static void Build(Node2D level, Rect2 floorArea)
	{
		level.AddChild(new GooCanvas { Name = "Goo", _area = floorArea, ZIndex = 6 }); // flying drops draw over walls and everyone (bullets and slow motion's look sit higher)
	}

	// A splat `size` pixels across, `offset` away from `from` (where the goo comes from), but
	// never past a wall in between. stretch > 1 smears it along `angle`.
	public static void Splat(Vector2 from, Vector2 offset, float size, float angle, float stretch = 1f, float strength = 1f)
	{
		_active?._orders.Add(new SplatOrder(from, offset, size, angle, stretch, strength));
	}

	// `count` drops flying out along `direction`, fanning out by up to `spread` radians.
	public static void Spray(Vector2 at, Vector2 direction, int count, float spread = 0.6f, float speedMin = 70f, float speedMax = 350f)
	{
		for (int i = 0; i < count && _active != null; i++)
		{
			Vector2 dir = direction.Rotated((float)GD.RandRange(-spread, spread));
			_active._drops.Add(new Drop { Pos = at, Vel = dir * (float)GD.RandRange(speedMin, speedMax), Seed = GD.Randf() });
		}
	}

	// A pool that spreads under a body for a few seconds.
	public static void PoolUnder(Vector2 at, float size)
	{
		if (_active is { } goo)
			goo._pools.Add(new Pool { At = at, Size = size, Angle = GD.Randf() * Mathf.Tau, Seed = GD.Randf(), Clip = goo.ClipFor(at, size / 2f) });
	}

	// Someone killed by a hit: drops burst out the way it went, a big splat, a smear where
	// the body slides, and a pool spreading under it.
	public static void Bleed(Vector2 at, Vector2 direction)
	{
		Splat(at, direction * 12f, (float)GD.RandRange(70.0, 100.0), direction.Angle() + (float)GD.RandRange(-0.4, 0.4));
		Splat(at, direction * 14f, 30f, direction.Angle(), stretch: 2.5f, strength: 0.8f);
		Spray(at, direction, 16);
		PoolUnder(at, 60f);
	}

	public static void Footprint(Vector2 at, float angle, float strength)
	{
		if (_active is { } goo)
			goo.Paint(at, 7f, angle, 1.4f, strength * 0.9f, GD.Randf(), null, wet: false); // prints don't refill your feet
	}

	public static bool IsWet(Vector2 at) => _active is { } goo && goo.CellAt(at) is Vector2I c && goo._wet[c.X, c.Y] > 0.3f;

	public override void _EnterTree() => _active = this;

	public override void _ExitTree()
	{
		RenderingServer.FramePostDraw -= ForgetPainted;
		if (_active == this)
			_active = null;
	}

	public override void _Ready()
	{
		_canvas = new SubViewport
		{
			Size = (Vector2I)_area.Size,
			TransparentBg = true,
			Disable3D = true,
			RenderTargetClearMode = SubViewport.ClearMode.Once, // clear when made, then never again
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once, // drawn again only when a splat lands (Paint)
		};
		AddChild(_canvas);
		_canvas.CanvasTransform = Transform2D.Identity.Translated(-_area.Position); // world pixels in, canvas pixels out (only once it's in the tree)
		var look = new ShaderMaterial { Shader = GD.Load<Shader>("res://art/goo_canvas.gdshader") };
		look.SetShaderParameter("all_red", DevSettings.Current.RedGoo);
		AddChild(new Sprite2D
		{
			Texture = _canvas.GetTexture(),
			Centered = false,
			Position = _area.Position,
			TextureFilter = TextureFilterEnum.Nearest,
			ZAsRelative = false,
			ZIndex = 1, // the goo layer
			Material = look,
		});
		_noClip = new ShaderMaterial { Shader = StampShader };
		FindWalls();
		RenderingServer.FramePostDraw += ForgetPainted;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		var space = GetWorld2D().DirectSpaceState;

		foreach (SplatOrder order in _orders)
		{
			var wall = space.IntersectRay(PhysicsRayQueryParameters2D.Create(order.From, order.From + order.Offset, Layers.World));
			Vector2 at = wall.Count > 0 ? (Vector2)wall["position"] - order.Offset.Normalized() * 2f : order.From + order.Offset;
			Paint(at, order.Size, order.Angle, order.Stretch, order.Strength, GD.Randf(), ClipFor(at, order.Size * Mathf.Max(order.Stretch, 1f) / 2f));
		}
		_orders.Clear();

		for (int i = _drops.Count - 1; i >= 0; i--)
		{
			Drop drop = _drops[i];
			Vector2 step = drop.Vel * dt;
			var hit = space.IntersectRay(PhysicsRayQueryParameters2D.Create(drop.Pos, drop.Pos + step, WallStops));
			if (hit.Count > 0)
			{
				// Splashes against the wall, smeared along it.
				Vector2 normal = (Vector2)hit["normal"];
				Paint((Vector2)hit["position"] + normal * 2f, (float)GD.RandRange(6.0, 12.0), normal.Angle() + Mathf.Pi / 2f, 1.8f, 1f, drop.Seed, ClipFor(drop.Pos, 8f));
				_drops.RemoveAt(i);
				continue;
			}
			drop.Pos += step;
			drop.Vel = drop.Vel.MoveToward(Vector2.Zero, DropDamping * dt);
			if (drop.Vel.LengthSquared() < 400f)
			{
				Paint(drop.Pos, (float)GD.RandRange(5.0, 12.0), GD.Randf() * Mathf.Tau, 1.3f, 1f, drop.Seed, ClipFor(drop.Pos, 7f));
				_drops.RemoveAt(i);
			}
		}

		for (int i = _pools.Count - 1; i >= 0; i--)
		{
			// A pool is painted over and over, a little bigger each time, so it creeps outward.
			Pool pool = _pools[i];
			pool.Age += dt;
			pool.NextStamp -= dt;
			if (pool.NextStamp <= 0f)
			{
				pool.NextStamp = 0.12f;
				float grown = Mathf.Lerp(0.35f, 1f, Mathf.Ease(Mathf.Min(pool.Age / PoolTime, 1f), 0.4f));
				Paint(pool.At, pool.Size * grown, pool.Angle, 1f, 0.3f, pool.Seed, pool.Clip);
			}
			if (pool.Age >= PoolTime)
				_pools.RemoveAt(i);
		}

		QueueRedraw();
	}

	// The drops still in the air.
	public override void _Draw()
	{
		foreach (Drop drop in _drops)
			DrawRect(new Rect2(drop.Pos - Vector2.One, Vector2.One * 2f), DevSettings.Current.RedGoo ? new Color(0.7f, 0.03f, 0.05f) : Color.FromHsv(drop.Seed, 0.85f, 0.95f));
	}

	private void Paint(Vector2 at, float size, float angle, float stretch, float strength, float seed, ShaderMaterial clip, bool wet = true)
	{
		Texture2D texture = size < 16f ? Dot : Mask;
		var stamp = new Sprite2D
		{
			Texture = texture,
			Position = at,
			Rotation = angle,
			Scale = new Vector2(size * stretch, size) / texture.GetWidth(),
			Modulate = new Color(1f, seed, Mathf.PosMod(seed * 7.31f + 0.37f, 1f), strength), // the splat's two colour seeds ride in green and blue
			Material = clip ?? _noClip,
		};
		_canvas.AddChild(stamp);
		_canvas.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		_painted.Add(stamp);
		if (wet)
			MarkWet(at, size * 0.35f, strength);
	}

	// A small soft round blob, bright in the middle.
	private static Texture2D MakeDot()
	{
		var dot = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
		for (int x = 0; x < 8; x++)
			for (int y = 0; y < 8; y++)
			{
				float edge = 1f - new Vector2(x - 3.5f, y - 3.5f).Length() / 4f;
				dot.SetPixel(x, y, new Color(0.5f + edge * 0.5f, 0f, 0f, Mathf.Clamp(edge * 3f, 0f, 1f)));
			}
		return ImageTexture.CreateFromImage(dot);
	}

	// Once a frame is drawn, its new splats are in the canvas for good: drop the stamps.
	private void ForgetPainted()
	{
		foreach (Sprite2D stamp in _painted)
		{
			stamp.Visible = false; // gone before the next frame draws, even if freeing waits
			stamp.QueueFree();
		}
		_painted.Clear();
	}

	// Which cells around `at` (within `radius`) the goo can reach without crossing a wall,
	// as a mask for the stamp shader. Null when no wall is near enough to matter.
	private ShaderMaterial ClipFor(Vector2 at, float radius)
	{
		if (CellAt(at) is not Vector2I start)
			return null;
		int r = Mathf.CeilToInt(radius / Cell) + 1, n = r * 2 + 1;
		Vector2I corner = start - new Vector2I(r, r);
		if (!AnyWall(corner, n))
			return null;
		if (_wall[start.X, start.Y] && FloorNextTo(start) is Vector2I floor)
			start = floor; // landed right in a wall's edge: flow from the floor beside it

		// Flood out from where it landed, never through a wall, and only by a nearly direct route:
		// it flows straight through a doorway, but never around a wall into the room behind it.
		var mask = Image.CreateEmpty(n, n, false, Image.Format.L8);
		var steps = new int[n, n];
		var todo = new Queue<Vector2I>();
		todo.Enqueue(start);
		mask.SetPixelv(start - corner, Colors.White);
		while (todo.Count > 0)
		{
			Vector2I cell = todo.Dequeue();
			int step = steps[cell.X - corner.X, cell.Y - corner.Y] + 1;
			foreach (Vector2I next in new[] { cell + Vector2I.Left, cell + Vector2I.Right, cell + Vector2I.Up, cell + Vector2I.Down })
			{
				Vector2I inMask = next - corner;
				if (inMask.X < 0 || inMask.Y < 0 || inMask.X >= n || inMask.Y >= n || IsWall(next) || mask.GetPixelv(inMask).R > 0f
					|| step > Mathf.Abs(next.X - start.X) + Mathf.Abs(next.Y - start.Y) + 2)
					continue;
				mask.SetPixelv(inMask, Colors.White);
				steps[inMask.X, inMask.Y] = step;
				todo.Enqueue(next);
			}
		}

		var material = new ShaderMaterial { Shader = StampShader };
		material.SetShaderParameter("clip", ImageTexture.CreateFromImage(mask));
		material.SetShaderParameter("clip_origin", _area.Position + (Vector2)corner * Cell);
		material.SetShaderParameter("clip_size", Vector2.One * n * Cell);
		return material;
	}

	private bool AnyWall(Vector2I corner, int n)
	{
		for (int x = 0; x < n; x++)
			for (int y = 0; y < n; y++)
				if (IsWall(corner + new Vector2I(x, y)))
					return true;
		return false;
	}

	private Vector2I? FloorNextTo(Vector2I cell)
	{
		foreach (Vector2I next in new[] { cell + Vector2I.Left, cell + Vector2I.Right, cell + Vector2I.Up, cell + Vector2I.Down })
			if (!IsWall(next))
				return next;
		return null;
	}

	// Outside the floor counts as wall: goo never leaves the level.
	private bool IsWall(Vector2I c) => c.X < 0 || c.Y < 0 || c.X >= _wall.GetLength(0) || c.Y >= _wall.GetLength(1) || _wall[c.X, c.Y];

	private Vector2I? CellAt(Vector2 world)
	{
		Vector2 local = (world - _area.Position) / Cell;
		var c = new Vector2I(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
		return c.X >= 0 && c.Y >= 0 && c.X < _wall.GetLength(0) && c.Y < _wall.GetLength(1) ? c : null;
	}

	private void MarkWet(Vector2 at, float radius, float strength)
	{
		if (CellAt(at) is not Vector2I c)
			return;
		int r = Mathf.FloorToInt(radius / Cell);
		for (int x = c.X - r; x <= c.X + r; x++)
			for (int y = c.Y - r; y <= c.Y + r; y++)
				if (!IsWall(new Vector2I(x, y)) && new Vector2(x - c.X, y - c.Y).Length() <= r + 0.5f)
					_wet[x, y] = Mathf.Max(_wet[x, y], strength);
	}

	// Every wall tile in the level (any tile that has a collision shape) marks its grid cells.
	private void FindWalls()
	{
		_wall = new bool[Mathf.CeilToInt(_area.Size.X / Cell), Mathf.CeilToInt(_area.Size.Y / Cell)];
		_wet = new float[_wall.GetLength(0), _wall.GetLength(1)];
		foreach (Node node in GetParent().GetChildren())
		{
			if (node is not TileMapLayer layer || layer.TileSet == null || layer.TileSet.GetPhysicsLayersCount() == 0)
				continue;
			Vector2 half = (Vector2)layer.TileSet.TileSize / 2f;
			foreach (Vector2I tile in layer.GetUsedCells())
			{
				if (layer.GetCellTileData(tile).GetCollisionPolygonsCount(0) == 0)
					continue;
				Vector2 center = layer.ToGlobal(layer.MapToLocal(tile));
				for (float x = center.X - half.X + Cell / 2f; x < center.X + half.X; x += Cell)
					for (float y = center.Y - half.Y + Cell / 2f; y < center.Y + half.Y; y += Cell)
						if (CellAt(new Vector2(x, y)) is Vector2I c)
							_wall[c.X, c.Y] = true;
			}
		}
	}
}
