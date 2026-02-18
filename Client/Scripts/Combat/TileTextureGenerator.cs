using Godot;
using System.Collections.Generic;

namespace ProjectTactics.Combat;

/// <summary>
/// Generates pixel art tile textures procedurally.
/// Each texture is a small Image painted pixel-by-pixel, then converted to ImageTexture.
/// Set TextureFilter to Nearest for crisp pixel art look.
/// Replace these with real hand-painted or AI-generated textures later.
/// </summary>
public static class TileTextureGenerator
{
	private const int Size = 32; // 32x32 pixel textures
	private static readonly Dictionary<string, ImageTexture> _cache = new();
	private static RandomNumberGenerator _rng = new();

	public static void SetSeed(ulong seed) => _rng.Seed = seed;

	// ═══════════════════════════════════════════════════════
	//  TOP FACE TEXTURES
	// ═══════════════════════════════════════════════════════

	public static ImageTexture GrassTop()
	{
		if (_cache.TryGetValue("grass_top", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		// Base greens with variation
		Color[] greens = {
			new("3a5c3a"), new("3d6340"), new("345834"),
			new("426946"), new("2f5030"), new("4a7a4c")
		};

		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			var c = greens[_rng.RandiRange(0, greens.Length - 1)];
			// Occasional lighter grass blades
			if (_rng.Randf() < 0.08f) c = c.Lightened(0.15f);
			// Occasional dark spots (dirt peeking through)
			if (_rng.Randf() < 0.04f) c = new Color("3a3020");
			img.SetPixel(x, y, c);
		}

		// Small flower cluster (2-3 pixels)
		if (_rng.Randf() < 0.3f)
		{
			int fx = _rng.RandiRange(4, Size - 5);
			int fy = _rng.RandiRange(4, Size - 5);
			img.SetPixel(fx, fy, new Color("cc8844"));
			img.SetPixel(fx + 1, fy, new Color("ddaa55"));
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["grass_top"] = tex;
		return tex;
	}

	public static ImageTexture DirtTop()
	{
		if (_cache.TryGetValue("dirt_top", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		Color[] browns = {
			new("5a4830"), new("634f35"), new("4e3e28"),
			new("6b5a3f"), new("554530"), new("7a6848")
		};

		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			var c = browns[_rng.RandiRange(0, browns.Length - 1)];
			// Pebbles
			if (_rng.Randf() < 0.05f) c = new Color("8a8070");
			// Dark cracks
			if (_rng.Randf() < 0.03f) c = c.Darkened(0.25f);
			img.SetPixel(x, y, c);
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["dirt_top"] = tex;
		return tex;
	}

	public static ImageTexture StoneTop()
	{
		if (_cache.TryGetValue("stone_top", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		Color[] grays = {
			new("686868"), new("5e5e5e"), new("727272"),
			new("585858"), new("6e6e70"), new("7a7a7c")
		};

		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			var c = grays[_rng.RandiRange(0, grays.Length - 1)];
			img.SetPixel(x, y, c);
		}

		// Draw brick/stone lines
		for (int x = 0; x < Size; x++)
		{
			// Horizontal mortar lines
			for (int row = 0; row < Size; row += 8)
			{
				img.SetPixel(x, Mathf.Min(row, Size - 1), new Color("4a4a4a"));
			}
			// Vertical mortar (offset every other row)
			for (int row = 0; row < Size; row += 8)
			{
				int offset = (row / 8 % 2 == 0) ? 0 : 4;
				for (int col = offset; col < Size; col += 8)
				{
					img.SetPixel(Mathf.Min(col, Size - 1), Mathf.Min(row + 4, Size - 1), new Color("4a4a4a"));
				}
			}
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["stone_top"] = tex;
		return tex;
	}

	public static ImageTexture WaterTop()
	{
		if (_cache.TryGetValue("water_top", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		Color[] blues = {
			new("2a4a6a"), new("2e5070"), new("264565"),
			new("325878"), new("1e3e58"), new("3a6080")
		};

		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			var c = blues[_rng.RandiRange(0, blues.Length - 1)];
			// Wave highlights
			if (_rng.Randf() < 0.06f) c = c.Lightened(0.2f);
			img.SetPixel(x, y, c);
		}

		// Horizontal wave lines
		for (int y = 4; y < Size; y += 7)
		for (int x = 0; x < Size; x++)
		{
			if (_rng.Randf() < 0.6f)
				img.SetPixel(x, y, new Color("4a80aa"));
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["water_top"] = tex;
		return tex;
	}

	public static ImageTexture SandTop()
	{
		if (_cache.TryGetValue("sand_top", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		Color[] sands = {
			new("8a7a50"), new("927f55"), new("80724a"),
			new("9a8a5e"), new("786840"), new("a09060")
		};

		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			var c = sands[_rng.RandiRange(0, sands.Length - 1)];
			if (_rng.Randf() < 0.03f) c = c.Lightened(0.12f);
			img.SetPixel(x, y, c);
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["sand_top"] = tex;
		return tex;
	}

	// ═══════════════════════════════════════════════════════
	//  SIDE FACE TEXTURES (cliff / wall cross-sections)
	// ═══════════════════════════════════════════════════════

	public static ImageTexture CliffSide()
	{
		if (_cache.TryGetValue("cliff_side", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			// Gradient: lighter at top (grass edge), darker at bottom (earth)
			float t = (float)y / Size;
			Color baseCol;
			if (t < 0.15f)
				baseCol = new Color("3a5c3a").Lerp(new Color("5a4830"), t / 0.15f); // grass to dirt
			else
				baseCol = new Color("4a3828").Lerp(new Color("3a2a1a"), (t - 0.15f) / 0.85f); // dirt layers

			// Rock streaks
			if (_rng.Randf() < 0.06f) baseCol = new Color("5a5a58");
			// Horizontal layer lines
			if (y % 6 == 0) baseCol = baseCol.Darkened(0.12f);

			img.SetPixel(x, y, baseCol);
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["cliff_side"] = tex;
		return tex;
	}

	public static ImageTexture StoneWallSide()
	{
		if (_cache.TryGetValue("stonewall_side", out var cached)) return cached;
		var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

		// Stone brick pattern
		for (int x = 0; x < Size; x++)
		for (int y = 0; y < Size; y++)
		{
			Color c = new Color("555560");
			// Mortar lines
			bool hLine = (y % 8 == 0);
			int vOffset = (y / 8 % 2 == 0) ? 0 : 5;
			bool vLine = ((x + vOffset) % 10 == 0);

			if (hLine || vLine)
				c = new Color("3a3a3e");
			else
			{
				// Slight per-brick variation
				int brickId = (x / 10) + (y / 8) * 4;
				float variation = (brickId % 5) * 0.03f;
				c = c.Lightened(variation - 0.06f);
			}
			img.SetPixel(x, y, c);
		}

		var tex = ImageTexture.CreateFromImage(img);
		_cache["stonewall_side"] = tex;
		return tex;
	}

	// ═══════════════════════════════════════════════════════
	//  MATERIAL HELPERS
	// ═══════════════════════════════════════════════════════

	/// <summary>Create a material with pixel art texture. Nearest filter = no blur.</summary>
	public static StandardMaterial3D MakeTextureMat(ImageTexture tex, float darken = 0f)
	{
		var mat = new StandardMaterial3D();
		mat.AlbedoTexture = tex;
		mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;

		if (darken > 0f)
			mat.AlbedoColor = Colors.White.Darkened(darken);

		// Pixel art looks best without smoothing
		mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
		mat.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
		return mat;
	}

	/// <summary>Get the top-face texture for a terrain type.</summary>
	public static ImageTexture GetTopTexture(TerrainType terrain)
	{
		return terrain switch
		{
			TerrainType.Open     => GrassTop(),
			TerrainType.Forest   => GrassTop(), // forest uses grass + tree decor
			TerrainType.Water    => WaterTop(),
			TerrainType.Rock     => StoneTop(),
			TerrainType.Sand     => SandTop(),
			TerrainType.Building => StoneTop(),
			_ => GrassTop()
		};
	}

	/// <summary>Get the side-face texture for a terrain type.</summary>
	public static ImageTexture GetSideTexture(TerrainType terrain)
	{
		return terrain switch
		{
			TerrainType.Rock     => StoneWallSide(),
			TerrainType.Building => StoneWallSide(),
			_ => CliffSide()
		};
	}
}
