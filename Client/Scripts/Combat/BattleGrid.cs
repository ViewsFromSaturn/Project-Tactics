using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

// ═══════════════════════════════════════════════════════════
//  TERRAIN & TILE DATA
// ═══════════════════════════════════════════════════════════

public enum TerrainType
{
	Open,       // cost 1, no modifier
	Forest,     // cost 2, +15% evasion, blocks LoS
	Water,      // cost 2, water +25%, fire -25%
	Rock,       // cost 3 climb, +10% acc, +1 range
	Sand,       // cost 1.5, wind +25%, reduces move
	Building    // cost 1, blocks LoS, no AoE splash
}

public class GridTile
{
	public int X;
	public int Y;
	public int Height;          // 0-5, each unit = 1 tile height
	public TerrainType Terrain;
	public BattleUnit Occupant; // null if empty

	public float MoveCost => Terrain switch
	{
		TerrainType.Open     => 1f,
		TerrainType.Forest   => 2f,
		TerrainType.Water    => 2f,
		TerrainType.Rock     => 1f, // climbing cost handled separately
		TerrainType.Sand     => 1.5f,
		TerrainType.Building => 1f,
		_ => 1f
	};

	public bool BlocksLineOfSight => Terrain is TerrainType.Forest or TerrainType.Building;

	public bool IsPassable => Occupant == null;

	public Vector2I GridPos => new(X, Y);
}

// ═══════════════════════════════════════════════════════════
//  BATTLE GRID — logical grid with pathfinding
// ═══════════════════════════════════════════════════════════

public class BattleGrid
{
	public int Width { get; private set; }
	public int Height { get; private set; }
	public GridTile[,] Tiles { get; private set; }

	/// <summary>Grid sizes per format from design doc.</summary>
	public static Vector2I GridSizeForFormat(int playersPerSide) => playersPerSide switch
	{
		1 => new(8, 8),
		2 => new(10, 10),
		3 => new(12, 12),
		4 => new(16, 16),
		_ => new(8, 8)
	};

	public BattleGrid(int width, int height)
	{
		Width = width;
		Height = height;
		Tiles = new GridTile[width, height];

		for (int x = 0; x < width; x++)
		for (int y = 0; y < height; y++)
		{
			Tiles[x, y] = new GridTile
			{
				X = x, Y = y,
				Height = 0,
				Terrain = TerrainType.Open
			};
		}
	}

	public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
	public bool InBounds(Vector2I pos) => InBounds(pos.X, pos.Y);

	public GridTile At(int x, int y) => InBounds(x, y) ? Tiles[x, y] : null;
	public GridTile At(Vector2I pos) => At(pos.X, pos.Y);

	// ─── HEIGHT TOOLS ────────────────────────────────────
	public void SetHeight(int x, int y, int h)
	{
		if (InBounds(x, y)) Tiles[x, y].Height = Math.Clamp(h, 0, 10);
	}

	public void SetTerrain(int x, int y, TerrainType t)
	{
		if (InBounds(x, y)) Tiles[x, y].Terrain = t;
	}

	// ─── MOVEMENT RANGE (BFS with move cost + height/jump) ─
	/// <summary>
	/// Returns all tiles reachable within moveRange, respecting terrain cost,
	/// height differences, and jump stat.
	/// </summary>
	public List<GridTile> GetMovementRange(Vector2I origin, int moveRange, int jumpStat)
	{
		var result = new List<GridTile>();
		var costSoFar = new Dictionary<Vector2I, float>();
		var frontier = new PriorityQueue<Vector2I, float>();

		costSoFar[origin] = 0;
		frontier.Enqueue(origin, 0);

		Vector2I[] dirs = { new(1,0), new(-1,0), new(0,1), new(0,-1) };

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();
			var currentTile = At(current);
			if (currentTile == null) continue;

			foreach (var dir in dirs)
			{
				var next = current + dir;
				var nextTile = At(next);
				if (nextTile == null) continue;
				if (nextTile.Occupant != null && next != origin) continue; // blocked

				// Height check — can't climb more than jump stat allows
				int heightDiff = Math.Abs(nextTile.Height - currentTile.Height);
				if (heightDiff > jumpStat) continue;

				// Movement cost: base terrain + climbing penalty
				float cost = nextTile.MoveCost;
				if (heightDiff > 0) cost += heightDiff * 0.5f; // climbing penalty

				float newCost = costSoFar[current] + cost;
				if (newCost > moveRange) continue;

				if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
				{
					costSoFar[next] = newCost;
					frontier.Enqueue(next, newCost);
				}
			}
		}

		foreach (var (pos, _) in costSoFar)
		{
			if (pos != origin)
				result.Add(At(pos));
		}

		return result;
	}

	// ─── A* PATHFINDING ──────────────────────────────────
	/// <summary>Find shortest path from start to goal, respecting terrain and height.</summary>
	public List<Vector2I> FindPath(Vector2I start, Vector2I goal, int jumpStat)
	{
		if (!InBounds(start) || !InBounds(goal)) return null;

		var cameFrom = new Dictionary<Vector2I, Vector2I>();
		var costSoFar = new Dictionary<Vector2I, float>();
		var frontier = new PriorityQueue<Vector2I, float>();

		cameFrom[start] = start;
		costSoFar[start] = 0;
		frontier.Enqueue(start, 0);

		Vector2I[] dirs = { new(1,0), new(-1,0), new(0,1), new(0,-1) };

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();
			if (current == goal) break;

			var currentTile = At(current);

			foreach (var dir in dirs)
			{
				var next = current + dir;
				var nextTile = At(next);
				if (nextTile == null) continue;
				if (nextTile.Occupant != null && next != goal) continue;

				int heightDiff = Math.Abs(nextTile.Height - currentTile.Height);
				if (heightDiff > jumpStat) continue;

				float cost = nextTile.MoveCost + (heightDiff > 0 ? heightDiff * 0.5f : 0);
				float newCost = costSoFar[current] + cost;

				if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
				{
					costSoFar[next] = newCost;
					float priority = newCost + Heuristic(next, goal);
					frontier.Enqueue(next, priority);
					cameFrom[next] = current;
				}
			}
		}

		if (!cameFrom.ContainsKey(goal)) return null;

		// Reconstruct path
		var path = new List<Vector2I>();
		var step = goal;
		while (step != start)
		{
			path.Add(step);
			step = cameFrom[step];
		}
		path.Reverse();
		return path;
	}

	private static float Heuristic(Vector2I a, Vector2I b)
		=> Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); // Manhattan

	// ─── ATTACK RANGE ────────────────────────────────────
	/// <summary>Get tiles within attack range (diamond shape, ignores terrain cost).</summary>
	public List<GridTile> GetAttackRange(Vector2I origin, int minRange, int maxRange)
	{
		var result = new List<GridTile>();
		for (int x = -maxRange; x <= maxRange; x++)
		for (int y = -maxRange; y <= maxRange; y++)
		{
			int dist = Math.Abs(x) + Math.Abs(y);
			if (dist < minRange || dist > maxRange) continue;
			var tile = At(origin.X + x, origin.Y + y);
			if (tile != null) result.Add(tile);
		}
		return result;
	}

	// ─── SAMPLE MAP GENERATION ───────────────────────────
	/// <summary>Generate a simple test map with some height variation and terrain.</summary>
	public static BattleGrid GenerateTestMap(int playersPerSide)
	{
		var size = GridSizeForFormat(playersPerSide);
		var grid = new BattleGrid(size.X, size.Y);
		var rng = new Random(42); // deterministic for testing

		for (int x = 0; x < size.X; x++)
		for (int y = 0; y < size.Y; y++)
		{
			// Center plateau
			int cx = size.X / 2, cy = size.Y / 2;
			int dist = Math.Abs(x - cx) + Math.Abs(y - cy);

			if (dist <= 2)
				grid.SetHeight(x, y, 2);
			else if (dist <= 4)
				grid.SetHeight(x, y, 1);

			// Scatter some terrain
			int roll = rng.Next(100);
			if (roll < 8 && dist > 3)
				grid.SetTerrain(x, y, TerrainType.Forest);
			else if (roll < 12 && dist > 2)
				grid.SetTerrain(x, y, TerrainType.Rock);
			else if (roll < 15)
				grid.SetTerrain(x, y, TerrainType.Water);
		}

		return grid;
	}
}
