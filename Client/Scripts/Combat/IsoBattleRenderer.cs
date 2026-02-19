using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

/// <summary>
/// Renders a BattleGrid as textured isometric 3D tiles with pixel art materials.
/// Uses real 3D geometry + nearest-filter textures for a crisp HD-2D look.
/// Directional light provides real-time shadows across height differences.
/// </summary>
public partial class IsoBattleRenderer : Node3D
{
	[Signal] public delegate void TileClickedEventHandler(int x, int y);
	[Signal] public delegate void TileHoveredEventHandler(int x, int y);
	[Signal] public delegate void TileRightClickedEventHandler(int x, int y);

	// ─── CONFIG ──────────────────────────────────────────
	private const float TileSize = 1.0f;
	private const float HeightStep = 0.35f;
	private const float TileTopThick = 0.08f;
	private const float TileGap = 0.03f;
	private const float WallThick = 0.02f;

	// Side face darkening (applied via material tint on textured sides)
	private const float SideDarken = 0.30f;
	private const float SideFrontDarken = 0.15f;

	// Grid line overlay
	private static readonly Color ColGridLine = new("ffffff12");

	// Highlights
	private static readonly Color ColHover       = new("aaccff50");
	private static readonly Color ColMoveRange   = new("4499ff80");
	private static readonly Color ColAttackRange = new("ff4455bb");
	private static readonly Color ColSelected    = new("ffcc4490");
	private static readonly Color ColMovePreview = new("aa55ff90"); // Purple — pending move confirm

	// Units
	private static readonly Color ColTeamA = new("5599ee");
	private static readonly Color ColTeamB = new("ee5555");
	private static readonly Color ColTeamADark = new("2255aa");
	private static readonly Color ColTeamBDark = new("aa2222");

	// ─── STATE ───────────────────────────────────────────
	private BattleGrid _grid;
	private readonly Dictionary<Vector2I, Node3D> _tileNodes = new();
	private readonly Dictionary<Vector2I, MeshInstance3D> _highlightMeshes = new();
	private readonly Dictionary<string, Node3D> _unitNodes = new();

	private Vector2I _hoveredTile = new(-1, -1);
	private Camera3D _camera;

	private readonly HashSet<int> _heightLevels = new();
	private readonly HashSet<Vector2I> _moveHighlights = new();
	private readonly HashSet<Vector2I> _attackHighlights = new();
	private Vector2I _selectedTile = new(-1, -1);

	// ═══════════════════════════════════════════════════════
	//  SETUP
	// ═══════════════════════════════════════════════════════

	public void Initialize(BattleGrid grid, Camera3D camera)
	{
		_grid = grid;
		_camera = camera;

		// Seed texture generator for deterministic map looks
		TileTextureGenerator.SetSeed((ulong)(grid.Width * 1000 + grid.Height));

		AddLighting();
		BuildGrid();
	}

	/// <summary>
	/// Adds a directional light for real shadows across elevated terrain,
	/// plus a soft ambient fill so shadowed areas aren't pitch black.
	/// </summary>
	private void AddLighting()
	{
		// Main directional light — angled like late afternoon sun
		var sun = new DirectionalLight3D();
		sun.Name = "BattleSun";
		sun.RotationDegrees = new Vector3(-45, -30, 0);
		sun.LightColor = new Color("fff8e8");       // warm white
		sun.LightEnergy = 0.9f;
		sun.ShadowEnabled = true;
		sun.ShadowBias = 0.03f;
		sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
		AddChild(sun);

		// Fill light from opposite side (no shadows) to soften contrast
		var fill = new DirectionalLight3D();
		fill.Name = "BattleFill";
		fill.RotationDegrees = new Vector3(-30, 150, 0);
		fill.LightColor = new Color("b0c0d8");      // cool blue fill
		fill.LightEnergy = 0.25f;
		fill.ShadowEnabled = false;
		AddChild(fill);
	}

	private void BuildGrid()
	{
		_heightLevels.Clear();
		for (int x = 0; x < _grid.Width; x++)
		for (int y = 0; y < _grid.Height; y++)
		{
			var tile = _grid.At(x, y);
			_heightLevels.Add(tile.Height);
			var tileNode = BuildTileVisual(tile);
			AddChild(tileNode);
			_tileNodes[new Vector2I(x, y)] = tileNode;
		}

		// Dark ground plane beneath the map
		var ground = new MeshInstance3D();
		var groundMesh = new PlaneMesh();
		float mapSpan = Mathf.Max(_grid.Width, _grid.Height) * TileSize;
		groundMesh.Size = new Vector2(mapSpan * 2, mapSpan * 2);
		ground.Mesh = groundMesh;
		var groundMat = new StandardMaterial3D();
		groundMat.AlbedoColor = new Color("12141a");
		groundMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		ground.MaterialOverride = groundMat;
		ground.Position = new Vector3(mapSpan * 0.5f, -0.1f, mapSpan * 0.5f);
		AddChild(ground);
	}

	// ═══════════════════════════════════════════════════════
	//  TILE CONSTRUCTION — textured top + textured sides
	// ═══════════════════════════════════════════════════════

	private Node3D BuildTileVisual(GridTile tile)
	{
		var node = new Node3D();
		node.Name = $"Tile_{tile.X}_{tile.Y}";

		float halfTile = (TileSize - TileGap) * 0.5f;

		// ─── TOP FACE (pixel art texture) ────────────────
		var top = new MeshInstance3D();
		var topMesh = new BoxMesh();
		topMesh.Size = new Vector3(halfTile * 2, TileTopThick, halfTile * 2);
		top.Mesh = topMesh;
		var topTex = TileTextureGenerator.GetTopTexture(tile.Terrain);
		top.MaterialOverride = TileTextureGenerator.MakeTextureMat(topTex);
		top.Position = GridToWorld(tile.X, tile.Y, tile.Height);
		top.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		node.AddChild(top);

		// ─── SIDE WALLS (textured cliff/stone for elevated tiles) ─
		if (tile.Height > 0)
		{
			float wallH = tile.Height * HeightStep;
			var sideTex = TileTextureGenerator.GetSideTexture(tile.Terrain);
			var basePos = GridToWorld(tile.X, tile.Y, 0);

			// Side materials: darker tint for sides, lighter for front
			var sideMat = TileTextureGenerator.MakeTextureMat(sideTex, SideDarken);
			var frontMat = TileTextureGenerator.MakeTextureMat(sideTex, SideFrontDarken);

			// Left wall (X-)
			AddWall(node, basePos, wallH, halfTile, new Vector3(-halfTile, 0, 0), sideMat, true);
			// Right wall (X+)
			AddWall(node, basePos, wallH, halfTile, new Vector3(halfTile, 0, 0), sideMat, true);
			// Front wall (Z+) — faces camera in default iso view
			AddWall(node, basePos, wallH, halfTile, new Vector3(0, 0, halfTile), frontMat, false);
			// Back wall (Z-)
			AddWall(node, basePos, wallH, halfTile, new Vector3(0, 0, -halfTile), sideMat, false);
		}

		// ─── GRID BORDER (thin line on top) ──────────────
		var border = new MeshInstance3D();
		var borderMesh = new BoxMesh();
		borderMesh.Size = new Vector3(halfTile * 2 + 0.01f, 0.005f, halfTile * 2 + 0.01f);
		border.Mesh = borderMesh;
		var borderMat = new StandardMaterial3D();
		borderMat.AlbedoColor = ColGridLine;
		borderMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		borderMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		border.MaterialOverride = borderMat;
		border.Position = GridToWorld(tile.X, tile.Y, tile.Height) + new Vector3(0, TileTopThick * 0.5f + 0.003f, 0);
		node.AddChild(border);

		// ─── TERRAIN DECORATIONS ─────────────────────────
		if (tile.Terrain == TerrainType.Forest)
			AddTreeDecor(node, tile);
		else if (tile.Terrain == TerrainType.Water)
			AddWaterDecor(node, tile);
		else if (tile.Terrain == TerrainType.Rock)
			AddRockDecor(node, tile);

		return node;
	}

	private void AddWall(Node3D parent, Vector3 basePos, float wallH,
		float halfTile, Vector3 offset, StandardMaterial3D mat, bool alongX)
	{
		var wall = new MeshInstance3D();
		var wallMesh = new BoxMesh();

		if (alongX)
			wallMesh.Size = new Vector3(WallThick, wallH, halfTile * 2);
		else
			wallMesh.Size = new Vector3(halfTile * 2, wallH, WallThick);

		wall.Mesh = wallMesh;
		wall.MaterialOverride = mat;
		wall.Position = basePos + offset + new Vector3(0, wallH * 0.5f, 0);
		wall.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		parent.AddChild(wall);
	}

	// ─── TERRAIN DECORATIONS ─────────────────────────────

	private void AddTreeDecor(Node3D parent, GridTile tile)
	{
		var tree = new MeshInstance3D();
		var cone = new CylinderMesh();
		cone.TopRadius = 0f;
		cone.BottomRadius = 0.12f;
		cone.Height = 0.35f;
		tree.Mesh = cone;
		tree.MaterialOverride = MakeMat(new Color("1a3d1a"));
		tree.Position = GridToWorld(tile.X, tile.Y, tile.Height) + new Vector3(0.1f, 0.22f, -0.05f);
		tree.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		parent.AddChild(tree);

		var trunk = new MeshInstance3D();
		var trunkMesh = new CylinderMesh();
		trunkMesh.TopRadius = 0.02f;
		trunkMesh.BottomRadius = 0.03f;
		trunkMesh.Height = 0.12f;
		trunk.Mesh = trunkMesh;
		trunk.MaterialOverride = MakeMat(new Color("4a3520"));
		trunk.Position = GridToWorld(tile.X, tile.Y, tile.Height) + new Vector3(0.1f, 0.1f, -0.05f);
		parent.AddChild(trunk);
	}

	private void AddWaterDecor(Node3D parent, GridTile tile)
	{
		var water = new MeshInstance3D();
		var quad = new BoxMesh();
		quad.Size = new Vector3(TileSize * 0.44f, 0.02f, TileSize * 0.44f);
		water.Mesh = quad;
		var waterMat = new StandardMaterial3D();
		waterMat.AlbedoColor = new Color("3366aa70");
		waterMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		waterMat.Metallic = 0.3f;
		waterMat.Roughness = 0.1f;
		water.MaterialOverride = waterMat;
		water.Position = GridToWorld(tile.X, tile.Y, tile.Height) + new Vector3(0, TileTopThick * 0.5f + 0.01f, 0);
		parent.AddChild(water);
	}

	private void AddRockDecor(Node3D parent, GridTile tile)
	{
		var rock = new MeshInstance3D();
		var sphere = new SphereMesh();
		sphere.Radius = 0.08f;
		sphere.Height = 0.12f;
		rock.Mesh = sphere;
		rock.MaterialOverride = MakeMat(new Color("6a6a70"));
		rock.Position = GridToWorld(tile.X, tile.Y, tile.Height) + new Vector3(-0.1f, 0.1f, 0.08f);
		rock.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		parent.AddChild(rock);
	}

	// ═══════════════════════════════════════════════════════
	//  COORDINATE CONVERSION
	//  Tiles are placed on a regular XZ grid. The camera's
	//  isometric angle creates the diamond appearance, just
	//  like Tactics Ogre Reborn.
	// ═══════════════════════════════════════════════════════

	public static Vector3 GridToWorld(int gx, int gy, int height)
	{
		float wx = gx * TileSize;
		float wz = gy * TileSize;
		float wy = height * HeightStep;
		return new Vector3(wx, wy, wz);
	}

	public Vector2I WorldToGrid(Vector3 worldPos)
	{
		int gx = Mathf.RoundToInt(worldPos.X / TileSize);
		int gy = Mathf.RoundToInt(worldPos.Z / TileSize);
		return new Vector2I(gx, gy);
	}

	// ═══════════════════════════════════════════════════════
	//  HIGHLIGHTING
	// ═══════════════════════════════════════════════════════

	public void ShowMoveRange(List<GridTile> tiles)
	{
		ClearMoveHighlights();
		foreach (var tile in tiles)
		{
			_moveHighlights.Add(tile.GridPos);
			SetTileOverlay(tile.GridPos, ColMoveRange);
		}
	}

	public void ShowAttackRange(List<GridTile> tiles)
	{
		ClearAttackHighlights();
		foreach (var tile in tiles)
		{
			_attackHighlights.Add(tile.GridPos);
			SetTileOverlay(tile.GridPos, ColAttackRange);
		}
	}

	public void ClearMoveHighlights()
	{
		foreach (var pos in _moveHighlights) RemoveTileOverlay(pos);
		_moveHighlights.Clear();
	}

	public void ClearAttackHighlights()
	{
		foreach (var pos in _attackHighlights) RemoveTileOverlay(pos);
		_attackHighlights.Clear();
	}

	public void ClearAllHighlights()
	{
		ClearMoveHighlights();
		ClearAttackHighlights();
		ClearMovePreview();
		_selectedTile = new(-1, -1);
	}

	// ─── MOVE PREVIEW (purple confirm target + path) ─────
	private readonly HashSet<Vector2I> _previewHighlights = new();

	public void ShowMovePreview(Vector2I target, List<Vector2I> path)
	{
		ClearMovePreview();
		// Highlight the path tiles in a dimmer purple
		if (path != null)
		{
			foreach (var p in path)
			{
				if (p == target) continue;
				_previewHighlights.Add(p);
				SetTileOverlay(p, new Color("8844cc60"));
			}
		}
		// Highlight the destination in bright purple
		_previewHighlights.Add(target);
		SetTileOverlay(target, ColMovePreview);
	}

	public void ClearMovePreview()
	{
		foreach (var pos in _previewHighlights) RemoveTileOverlay(pos);
		_previewHighlights.Clear();
	}

	private void SetTileOverlay(Vector2I pos, Color color)
	{
		if (_highlightMeshes.ContainsKey(pos))
		{
			if (_highlightMeshes[pos].MaterialOverride is StandardMaterial3D existing)
				existing.AlbedoColor = color;
			return;
		}

		var tile = _grid.At(pos);
		if (tile == null) return;

		var mesh = new MeshInstance3D();
		var quad = new BoxMesh();
		float s = (TileSize - TileGap) * 0.98f;
		quad.Size = new Vector3(s, 0.01f, s);
		mesh.Mesh = quad;

		var mat = new StandardMaterial3D();
		mat.AlbedoColor = color;
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		mat.EmissionEnabled = true;
		mat.Emission = new Color(color.R, color.G, color.B, 1f);
		mat.EmissionEnergyMultiplier = 0.5f;
		mat.RenderPriority = 1; // render on top of tile textures
		mesh.MaterialOverride = mat;

		mesh.Position = GridToWorld(pos.X, pos.Y, tile.Height)
			+ new Vector3(0, TileTopThick * 0.5f + 0.02f, 0);
		AddChild(mesh);
		_highlightMeshes[pos] = mesh;
	}

	private void RemoveTileOverlay(Vector2I pos)
	{
		if (_highlightMeshes.TryGetValue(pos, out var mesh))
		{
			mesh.QueueFree();
			_highlightMeshes.Remove(pos);
		}
	}

	// ═══════════════════════════════════════════════════════
	//  UNIT VISUALS
	// ═══════════════════════════════════════════════════════

	public void PlaceUnit(BattleUnit unit)
	{
		var node = new Node3D();
		node.Name = $"Unit_{unit.Name}";

		bool isA = unit.Team == UnitTeam.TeamA;
		Color mainCol = isA ? ColTeamA : ColTeamB;

		// Body
		var body = new MeshInstance3D();
		var bodyMesh = new CylinderMesh();
		bodyMesh.TopRadius = 0.1f;
		bodyMesh.BottomRadius = 0.13f;
		bodyMesh.Height = 0.35f;
		body.Mesh = bodyMesh;
		body.MaterialOverride = MakeMat(mainCol);
		body.Position = new Vector3(0, 0.22f, 0);
		body.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		node.AddChild(body);

		// Head
		var head = new MeshInstance3D();
		var headMesh = new SphereMesh();
		headMesh.Radius = 0.08f;
		headMesh.Height = 0.16f;
		head.Mesh = headMesh;
		head.MaterialOverride = MakeMat(mainCol.Lightened(0.15f));
		head.Position = new Vector3(0, 0.46f, 0);
		head.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		node.AddChild(head);

		// Shadow disc
		var shadow = new MeshInstance3D();
		var shadowMesh = new CylinderMesh();
		shadowMesh.TopRadius = 0.14f;
		shadowMesh.BottomRadius = 0.14f;
		shadowMesh.Height = 0.005f;
		shadow.Mesh = shadowMesh;
		var shadowMat = new StandardMaterial3D();
		shadowMat.AlbedoColor = new Color("00000040");
		shadowMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		shadowMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		shadow.MaterialOverride = shadowMat;
		shadow.Position = new Vector3(0, 0.045f, 0);
		node.AddChild(shadow);

		// Team ring
		var ring = new MeshInstance3D();
		var ringMesh = new TorusMesh();
		ringMesh.InnerRadius = 0.12f;
		ringMesh.OuterRadius = 0.17f;
		ring.Mesh = ringMesh;
		var ringMat = new StandardMaterial3D();
		ringMat.AlbedoColor = mainCol;
		ringMat.EmissionEnabled = true;
		ringMat.Emission = mainCol;
		ringMat.EmissionEnergyMultiplier = 0.5f;
		ring.MaterialOverride = ringMat;
		ring.Position = new Vector3(0, 0.05f, 0);
		node.AddChild(ring);

		// Name label
		var label = new Label3D();
		label.Text = unit.Name;
		label.FontSize = 28;
		label.PixelSize = 0.003f;
		label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		label.Position = new Vector3(0, 0.62f, 0);
		label.Modulate = Colors.White;
		label.OutlineSize = 6;
		label.OutlineModulate = new Color("000000cc");
		node.AddChild(label);

		// HP bar
		var hpBar = BuildHpBar(unit);
		hpBar.Position = new Vector3(0, 0.55f, 0);
		node.AddChild(hpBar);

		var tile = _grid.At(unit.GridPosition);
		node.Position = GridToWorld(unit.GridPosition.X, unit.GridPosition.Y, tile?.Height ?? 0);
		AddChild(node);
		_unitNodes[unit.CharacterId] = node;
	}

	private Node3D BuildHpBar(BattleUnit unit)
	{
		var container = new Node3D();

		var bg = new MeshInstance3D();
		var bgMesh = new BoxMesh();
		bgMesh.Size = new Vector3(0.3f, 0.025f, 0.01f);
		bg.Mesh = bgMesh;
		var bgMat = new StandardMaterial3D();
		bgMat.AlbedoColor = new Color("00000080");
		bgMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		bgMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		bgMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
		bg.MaterialOverride = bgMat;
		container.AddChild(bg);

		float fill = unit.HpPercent;
		var hp = new MeshInstance3D();
		var hpMesh = new BoxMesh();
		hpMesh.Size = new Vector3(0.28f * fill, 0.02f, 0.012f);
		hp.Mesh = hpMesh;
		var hpMat = new StandardMaterial3D();
		hpMat.AlbedoColor = fill > 0.5f ? new Color("44cc55") : fill > 0.25f ? new Color("ccaa33") : new Color("cc3333");
		hpMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		hpMat.EmissionEnabled = true;
		hpMat.Emission = hpMat.AlbedoColor;
		hpMat.EmissionEnergyMultiplier = 0.3f;
		hpMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
		hp.MaterialOverride = hpMat;
		hp.Position = new Vector3(-(0.28f - 0.28f * fill) * 0.5f, 0, 0.001f);
		container.AddChild(hp);

		return container;
	}

	public void MoveUnitVisual(BattleUnit unit)
	{
		if (_unitNodes.TryGetValue(unit.CharacterId, out var node))
		{
			var tile = _grid.At(unit.GridPosition);
			node.Position = GridToWorld(unit.GridPosition.X, unit.GridPosition.Y, tile?.Height ?? 0);
		}
	}

	// ═══════════════════════════════════════════════════════
	//  MOUSE INPUT
	// ═══════════════════════════════════════════════════════

	public override void _UnhandledInput(InputEvent ev)
	{
		if (_camera == null || _grid == null) return;

		if (ev is InputEventMouseMotion mm)
		{
			var gridPos = ScreenToGrid(mm.Position);
			if (gridPos != _hoveredTile)
			{
				// Restore previous hovered tile
				if (_grid.InBounds(_hoveredTile))
				{
					if (_moveHighlights.Contains(_hoveredTile))
						SetTileOverlay(_hoveredTile, ColMoveRange);  // restore blue
					else if (_attackHighlights.Contains(_hoveredTile))
						SetTileOverlay(_hoveredTile, ColAttackRange);
					else if (!_previewHighlights.Contains(_hoveredTile))
						RemoveTileOverlay(_hoveredTile);
				}

				_hoveredTile = gridPos;

				if (_grid.InBounds(gridPos))
				{
					// Purple hover for move-range tiles, red-ish for attack, default otherwise
					if (_moveHighlights.Contains(gridPos))
						SetTileOverlay(gridPos, ColMovePreview);
					else if (_attackHighlights.Contains(gridPos))
						SetTileOverlay(gridPos, new Color("ff6666bb"));
					else if (!_previewHighlights.Contains(gridPos))
						SetTileOverlay(gridPos, ColHover);

					EmitSignal(SignalName.TileHovered, gridPos.X, gridPos.Y);
				}
			}
		}
		else if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			var gridPos = ScreenToGrid(mb.Position);
			if (_grid.InBounds(gridPos))
			{
				_selectedTile = gridPos;
				EmitSignal(SignalName.TileClicked, gridPos.X, gridPos.Y);
			}
		}
		else if (ev is InputEventMouseButton rb && rb.Pressed && rb.ButtonIndex == MouseButton.Right)
		{
			var gridPos = ScreenToGrid(rb.Position);
			if (_grid.InBounds(gridPos))
				EmitSignal(SignalName.TileRightClicked, gridPos.X, gridPos.Y);
		}
	}

	private Vector2I ScreenToGrid(Vector2 screenPos)
	{
		if (_camera == null) return new(-1, -1);

		var from = _camera.ProjectRayOrigin(screenPos);
		var dir  = _camera.ProjectRayNormal(screenPos);

		if (Mathf.Abs(dir.Y) < 0.001f) return new(-1, -1);

		// Cast ray against each distinct height plane, use WorldToGrid
		// for proper isometric diamond-to-grid conversion
		Vector2I bestTile = new(-1, -1);
		float    bestT    = float.MaxValue;

		foreach (int h in _heightLevels)
		{
			float planeY = h * HeightStep + TileTopThick * 0.5f;
			float t = (planeY - from.Y) / dir.Y;
			if (t < 0 || t >= bestT) continue;

			var hitPoint = from + dir * t;
			var gridPos  = WorldToGrid(hitPoint);

			if (_grid.InBounds(gridPos))
			{
				var tile = _grid.At(gridPos);
				if (tile != null && tile.Height == h)
				{
					bestT    = t;
					bestTile = gridPos;
				}
			}
		}

		return bestTile;
	}

	// ═══════════════════════════════════════════════════════
	//  HELPERS
	// ═══════════════════════════════════════════════════════

	private static StandardMaterial3D MakeMat(Color color, bool unshaded = false)
	{
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = color;
		if (unshaded) mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		return mat;
	}
}
