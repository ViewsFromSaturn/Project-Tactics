using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectTactics.Combat;

/// <summary>Tracks all combat statistics during a battle.</summary>
public class BattleStats
{
	public int TurnsElapsed;
	public int TotalTilesTraversed;
	public int PlayerDamageDealt;
	public int EnemyDamageDealt;
	public int HighestSingleHit;
	public string HighestHitSkill = "—";
	public int CriticalHits;
	public int PlayerDodges;   // times player units dodged
	public int EnemiesDefeated;
	public int AlliesFallen;
	public int StaminaSpent;
	public int AetherSpent;
	public float BattleStartTime;

	// Per-unit damage tracking for MVP
	public readonly Dictionary<string, int> DamageByUnit = new();
	public readonly Dictionary<string, string> UnitNames = new();

	public void RecordDamage(BattleUnit attacker, int dmg, string skillName, bool crit)
	{
		if (attacker.Team == UnitTeam.TeamA)
		{
			PlayerDamageDealt += dmg;
			if (!DamageByUnit.ContainsKey(attacker.CharacterId)) DamageByUnit[attacker.CharacterId] = 0;
			DamageByUnit[attacker.CharacterId] += dmg;
			UnitNames[attacker.CharacterId] = attacker.Name;
		}
		else
		{
			EnemyDamageDealt += dmg;
		}

		if (crit) CriticalHits++;

		if (dmg > HighestSingleHit)
		{
			HighestSingleHit = dmg;
			HighestHitSkill = skillName;
		}
	}

	public void RecordDodge(BattleUnit dodger)
	{
		if (dodger.Team == UnitTeam.TeamA) PlayerDodges++;
	}

	public void RecordKill(BattleUnit attacker, BattleUnit victim)
	{
		if (attacker.Team == UnitTeam.TeamA) EnemiesDefeated++;
		else AlliesFallen++;
	}

	public void RecordMove(int tiles) => TotalTilesTraversed += tiles;

	public void RecordStaminaSpent(int amount) => StaminaSpent += amount;
	public void RecordAetherSpent(int amount) => AetherSpent += amount;

	public string GetMvpName()
	{
		if (DamageByUnit.Count == 0) return "—";
		var top = DamageByUnit.OrderByDescending(kv => kv.Value).First();
		return UnitNames.TryGetValue(top.Key, out var name) ? name : "—";
	}

	public string GetBattleDuration(float currentTime)
	{
		float elapsed = currentTime - BattleStartTime;
		int mins = (int)(elapsed / 60f);
		int secs = (int)(elapsed % 60f);
		return $"{mins:D2}:{secs:D2}";
	}
}

/// <summary>
/// Full-screen KH-style Battle Report overlay.
/// Left: stat rows with dotted leaders. Right: art placeholder.
/// </summary>
public static class BattleReport
{
	// ─── COLORS ───
	static readonly Color BgDark = new("090912f0");
	static readonly Color BorderAccent = new("7c5cbf");
	static readonly Color TitleGold = new("d4a843");
	static readonly Color StatLabel = new("888899");
	static readonly Color StatValue = new("e8e8f0");
	static readonly Color StatHighlight = new("d4a843");
	static readonly Color DotColor = new("333344");
	static readonly Color VictoryGreen = new("44cc55");
	static readonly Color DefeatRed = new("cc3333");
	static readonly Color FledGray = new("8888aa");
	static readonly Color ArtBg = new("0c0c18");

	public enum BattleResult { Victory, Defeat, Fled }

	/// <summary>Player identity for the right panel.</summary>
	public struct PlayerInfo
	{
		public string Name;
		public string Race;
		public string Rank;
		public string SpriteSheetPath; // fallback if no illustration
	}

	public static CanvasLayer Build(BattleResult result, BattleStats stats, PlayerInfo player, float currentTime, System.Action onReturn)
	{
		var layer = new CanvasLayer();
		layer.Layer = 50;

		// Dim
		var dim = new ColorRect();
		dim.Color = new Color(0, 0, 0, 0.75f);
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(dim);

		// Main container — centered
		var outer = new PanelContainer();
		outer.SetAnchorsPreset(Control.LayoutPreset.Center);
		outer.CustomMinimumSize = new Vector2(820, 480);
		outer.Position = new Vector2(-410, -220);
		var outerStyle = new StyleBoxFlat();
		outerStyle.BgColor = BgDark;
		outerStyle.BorderColor = BorderAccent;
		outerStyle.SetBorderWidthAll(2);
		outerStyle.SetCornerRadiusAll(4);
		outerStyle.SetContentMarginAll(0);
		outer.AddThemeStyleboxOverride("panel", outerStyle);
		layer.AddChild(outer);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		outer.AddChild(hbox);

		// ═══════════════════════════════════════════════
		//  LEFT SIDE — Stats
		// ═══════════════════════════════════════════════
		var leftPanel = new PanelContainer();
		leftPanel.CustomMinimumSize = new Vector2(500, 480);
		leftPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var leftStyle = new StyleBoxFlat();
		leftStyle.BgColor = Colors.Transparent;
		leftStyle.ContentMarginLeft = 32; leftStyle.ContentMarginRight = 24;
		leftStyle.ContentMarginTop = 28; leftStyle.ContentMarginBottom = 20;
		leftPanel.AddThemeStyleboxOverride("panel", leftStyle);
		hbox.AddChild(leftPanel);

		var leftVbox = new VBoxContainer();
		leftVbox.AddThemeConstantOverride("separation", 0);
		leftPanel.AddChild(leftVbox);

		// Title
		var title = new Label();
		title.Text = "BATTLE REPORT";
		title.AddThemeFontSizeOverride("font_size", 26);
		title.AddThemeColorOverride("font_color", TitleGold);
		title.HorizontalAlignment = HorizontalAlignment.Left;
		leftVbox.AddChild(title);

		// Thin separator line
		var sepLine = new ColorRect();
		sepLine.Color = new Color(BorderAccent, 0.4f);
		sepLine.CustomMinimumSize = new Vector2(0, 1);
		leftVbox.AddChild(sepLine);

		// Spacer
		AddSpacer(leftVbox, 12);

		// Result row — color-coded
		Color resultColor = result switch
		{
			BattleResult.Victory => VictoryGreen,
			BattleResult.Defeat => DefeatRed,
			_ => FledGray
		};
		string resultText = result switch
		{
			BattleResult.Victory => "Victory",
			BattleResult.Defeat => "Defeat",
			_ => "Fled"
		};
		AddDottedRow(leftVbox, "Result", resultText, resultColor);
		AddSpacer(leftVbox, 4);

		// All 14 stats
		AddDottedRow(leftVbox, "Turns Elapsed", stats.TurnsElapsed.ToString());
		AddDottedRow(leftVbox, "Tiles Traversed", stats.TotalTilesTraversed.ToString());
		AddDottedRow(leftVbox, "Damage Dealt", stats.PlayerDamageDealt.ToString(), StatHighlight);
		AddDottedRow(leftVbox, "Damage Received", stats.EnemyDamageDealt.ToString());

		string highHitText = stats.HighestSingleHit > 0
			? $"{stats.HighestSingleHit} — {stats.HighestHitSkill}" : "—";
		AddDottedRow(leftVbox, "Highest Single Hit", highHitText, StatHighlight);

		AddDottedRow(leftVbox, "Critical Hits", stats.CriticalHits.ToString());
		AddDottedRow(leftVbox, "Dodges", stats.PlayerDodges.ToString());
		AddDottedRow(leftVbox, "Enemies Defeated", stats.EnemiesDefeated.ToString(),
			stats.EnemiesDefeated > 0 ? VictoryGreen : StatValue);
		AddDottedRow(leftVbox, "Allies Fallen", stats.AlliesFallen.ToString(),
			stats.AlliesFallen > 0 ? DefeatRed : StatValue);
		AddDottedRow(leftVbox, "Stamina Spent", stats.StaminaSpent.ToString());
		AddDottedRow(leftVbox, "Aether Spent", stats.AetherSpent.ToString());
		AddDottedRow(leftVbox, "MVP", stats.GetMvpName(), StatHighlight);
		AddDottedRow(leftVbox, "Battle Duration", stats.GetBattleDuration(currentTime));

		// Bottom spacer + button
		var bottomSpacer = new Control();
		bottomSpacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		leftVbox.AddChild(bottomSpacer);

		var returnBtn = new Button();
		returnBtn.Text = "RETURN TO OVERWORLD";
		returnBtn.CustomMinimumSize = new Vector2(280, 40);
		returnBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		var btnStyle = new StyleBoxFlat();
		btnStyle.BgColor = new Color(BorderAccent, 0.2f);
		btnStyle.BorderColor = BorderAccent;
		btnStyle.SetBorderWidthAll(1);
		btnStyle.SetCornerRadiusAll(4);
		btnStyle.ContentMarginTop = 8; btnStyle.ContentMarginBottom = 8;
		btnStyle.ContentMarginLeft = 20; btnStyle.ContentMarginRight = 20;
		returnBtn.AddThemeStyleboxOverride("normal", btnStyle);
		var btnHover = btnStyle.Duplicate() as StyleBoxFlat;
		btnHover.BgColor = new Color(BorderAccent, 0.35f);
		returnBtn.AddThemeStyleboxOverride("hover", btnHover);
		returnBtn.AddThemeColorOverride("font_color", StatValue);
		returnBtn.AddThemeFontSizeOverride("font_size", 14);
		returnBtn.Pressed += () => onReturn?.Invoke();
		leftVbox.AddChild(returnBtn);

		// ═══════════════════════════════════════════════
		//  RIGHT SIDE — Player character art
		// ═══════════════════════════════════════════════
		var rightPanel = new PanelContainer();
		rightPanel.CustomMinimumSize = new Vector2(320, 480);
		rightPanel.ClipContents = true;
		var rightStyle = new StyleBoxFlat();
		rightStyle.BgColor = ArtBg;
		rightStyle.BorderWidthLeft = 1;
		rightStyle.BorderColor = new Color(BorderAccent, 0.3f);
		rightStyle.SetContentMarginAll(0);
		rightPanel.AddThemeStyleboxOverride("panel", rightStyle);
		hbox.AddChild(rightPanel);

		// Try race illustration first, then sprite sheet fallback
		var artTexture = LoadCharacterArt(player.Race);
		if (artTexture != null)
		{
			var artRect = new TextureRect();
			artRect.Texture = artTexture;
			artRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			artRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			artRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			rightPanel.AddChild(artRect);
		}
		else if (!string.IsNullOrEmpty(player.SpriteSheetPath))
		{
			// Fallback: enlarged front-facing sprite from sheet
			BuildSpriteFallback(rightPanel, player.SpriteSheetPath);
		}
		else
		{
			// No art at all — minimal placeholder
			var ph = new Label();
			ph.Text = "⚔";
			ph.HorizontalAlignment = HorizontalAlignment.Center;
			ph.VerticalAlignment = VerticalAlignment.Center;
			ph.AddThemeFontSizeOverride("font_size", 72);
			ph.AddThemeColorOverride("font_color", new Color(BorderAccent, 0.1f));
			ph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			rightPanel.AddChild(ph);
		}

		// Player identity card — overlaid at bottom of art panel
		var infoOverlay = new PanelContainer();
		infoOverlay.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		infoOverlay.OffsetTop = -90; infoOverlay.OffsetBottom = 0;
		infoOverlay.OffsetLeft = 0; infoOverlay.OffsetRight = 0;
		var infoStyle = new StyleBoxFlat();
		infoStyle.BgColor = new Color(0, 0, 0, 0.7f);
		infoStyle.ContentMarginLeft = 16; infoStyle.ContentMarginRight = 16;
		infoStyle.ContentMarginTop = 12; infoStyle.ContentMarginBottom = 12;
		infoOverlay.AddThemeStyleboxOverride("panel", infoStyle);
		rightPanel.AddChild(infoOverlay);

		var infoVbox = new VBoxContainer();
		infoVbox.AddThemeConstantOverride("separation", 4);
		infoOverlay.AddChild(infoVbox);

		var nameLabel = new Label();
		nameLabel.Text = player.Name;
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.AddThemeColorOverride("font_color", StatValue);
		infoVbox.AddChild(nameLabel);

		var detailLabel = new Label();
		detailLabel.Text = $"{player.Race}  ·  {player.Rank}";
		detailLabel.AddThemeFontSizeOverride("font_size", 12);
		detailLabel.AddThemeColorOverride("font_color", StatLabel);
		infoVbox.AddChild(detailLabel);

		// ═══════════════════════════════════════════════
		//  FADE-IN ANIMATION
		// ═══════════════════════════════════════════════
		dim.Modulate = new Color(1, 1, 1, 0);
		outer.Modulate = new Color(1, 1, 1, 0);
		outer.Position += new Vector2(0, 20); // slide up from below

		// Stagger: dim fades, then panel slides up + fades
		var tween = layer.CreateTween();
		tween.TweenProperty(dim, "modulate:a", 1f, 0.3f);
		tween.TweenProperty(outer, "modulate:a", 1f, 0.5f).SetDelay(0.1f);

		// Parallel slide-up
		var slideTween = layer.CreateTween();
		slideTween.TweenProperty(outer, "position:y", outer.Position.Y - 20f, 0.5f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).SetDelay(0.1f);

		return layer;
	}

	// ─── DOTTED ROW ─────────────────────────────────
	// "Label ............... Value"
	static void AddDottedRow(VBoxContainer parent, string label, string value, Color? valueColor = null)
	{
		var hbox = new HBoxContainer();
		hbox.CustomMinimumSize = new Vector2(0, 26);
		parent.AddChild(hbox);

		// Label
		var lbl = new Label();
		lbl.Text = label;
		lbl.AddThemeFontSizeOverride("font_size", 14);
		lbl.AddThemeColorOverride("font_color", StatLabel);
		hbox.AddChild(lbl);

		// Dot fill
		var dots = new Label();
		dots.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		dots.HorizontalAlignment = HorizontalAlignment.Center;
		dots.ClipText = true;
		// Generate plenty of dots — ClipText handles overflow
		dots.Text = " · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · ";
		dots.AddThemeFontSizeOverride("font_size", 10);
		dots.AddThemeColorOverride("font_color", DotColor);
		hbox.AddChild(dots);

		// Value
		var val = new Label();
		val.Text = value;
		val.HorizontalAlignment = HorizontalAlignment.Right;
		val.AddThemeFontSizeOverride("font_size", 14);
		val.AddThemeColorOverride("font_color", valueColor ?? StatValue);
		hbox.AddChild(val);
	}

	static void AddSpacer(VBoxContainer parent, float height)
	{
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, height);
		parent.AddChild(spacer);
	}

	// ─── CHARACTER ART LOADER ────────────────────────
	// Looks for: res://Assets/Art/Characters/{race}.png (or jpg/webp)
	// Each race gets one illustration. Shared across all players of that race for now.
	// Later: per-character custom portraits.
	const string CharArtFolder = "res://Assets/Art/Characters";
	static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

	static Texture2D LoadCharacterArt(string race)
	{
		if (string.IsNullOrEmpty(race)) return null;

		foreach (var ext in ImageExtensions)
		{
			string path = $"{CharArtFolder}/{race}{ext}";
			if (ResourceLoader.Exists(path))
			{
				var tex = GD.Load<Texture2D>(path);
				if (tex != null) { GD.Print($"[BattleReport] Art: {path}"); return tex; }
			}
		}
		return null;
	}

	/// <summary>Fallback: extract front-facing frame from sprite sheet, scale up.</summary>
	static void BuildSpriteFallback(PanelContainer parent, string sheetPath)
	{
		var sheet = GD.Load<Texture2D>(sheetPath);
		if (sheet == null) return;

		// Front row = row 0, idle frame = col 0, 5 cols × 4 rows
		int cols = 5, rows = 4;
		int fw = sheet.GetWidth() / cols;
		int fh = sheet.GetHeight() / rows;

		var atlas = new AtlasTexture();
		atlas.Atlas = sheet;
		atlas.Region = new Rect2(0, 0, fw, fh); // frame (0,0) = front idle

		var sprite = new TextureRect();
		sprite.Texture = atlas;
		sprite.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		sprite.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest; // crisp pixel art
		sprite.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		// Inset so sprite doesn't touch edges
		sprite.OffsetLeft = 60; sprite.OffsetRight = -60;
		sprite.OffsetTop = 40; sprite.OffsetBottom = -100;
		parent.AddChild(sprite);
	}
}
