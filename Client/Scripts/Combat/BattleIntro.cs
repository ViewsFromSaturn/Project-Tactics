using Godot;

namespace ProjectTactics.Combat;

/// <summary>
/// Battle intro cinematic overlay.
/// Sequence: dim → team banners slide in → "ENGAGE" slam → fade out → battle starts.
/// </summary>
public partial class BattleIntro : CanvasLayer
{
	[Signal] public delegate void IntroFinishedEventHandler();

	// ─── COLORS ───
	static readonly Color PlayerColor = new("4466cc");
	static readonly Color EnemyColor = new("cc3344");
	static readonly Color EngageGold = new("d4a843");

	string _playerName;
	string _enemyName;

	public BattleIntro(string playerName = "YOUR TEAM", string enemyName = "ENEMY FORCES")
	{
		_playerName = playerName;
		_enemyName = enemyName;
		Layer = 60; // above everything
	}

	public override void _Ready()
	{
		PlayIntro();
	}

	void PlayIntro()
	{
		// ─── BLACK BARS (letterbox) ───
		var topBar = new ColorRect();
		topBar.Color = Colors.Black;
		topBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		topBar.CustomMinimumSize = new Vector2(0, 80);
		topBar.Size = new Vector2(1920, 80);
		AddChild(topBar);

		var bottomBar = new ColorRect();
		bottomBar.Color = Colors.Black;
		bottomBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		bottomBar.Position = new Vector2(0, -80);
		bottomBar.AnchorTop = 1; bottomBar.AnchorBottom = 1;
		bottomBar.OffsetTop = -80; bottomBar.OffsetBottom = 0;
		bottomBar.CustomMinimumSize = new Vector2(0, 80);
		AddChild(bottomBar);

		// ─── DIM OVERLAY ───
		var dim = new ColorRect();
		dim.Color = new Color(0, 0, 0, 0.5f);
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		// ─── DIAGONAL SLASH LINE ───
		var slashLine = new ColorRect();
		slashLine.Color = new Color(1, 1, 1, 0.15f);
		slashLine.CustomMinimumSize = new Vector2(3, 800);
		slashLine.SetAnchorsPreset(Control.LayoutPreset.Center);
		slashLine.Position = new Vector2(-1, -400);
		slashLine.Rotation = Mathf.DegToRad(12);
		AddChild(slashLine);

		// ─── LEFT BANNER (player team) ───
		var leftBanner = CreateBanner(_playerName, PlayerColor, true);
		leftBanner.Position = new Vector2(-600, 0); // start offscreen left
		leftBanner.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
		AddChild(leftBanner);

		// ─── RIGHT BANNER (enemy team) ───
		var rightBanner = CreateBanner(_enemyName, EnemyColor, false);
		rightBanner.Position = new Vector2(600, 0); // start offscreen right
		rightBanner.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
		AddChild(rightBanner);

		// ─── "ENGAGE" TEXT ───
		var engageLabel = new Label();
		engageLabel.Text = "ENGAGE";
		engageLabel.HorizontalAlignment = HorizontalAlignment.Center;
		engageLabel.VerticalAlignment = VerticalAlignment.Center;
		engageLabel.AddThemeFontSizeOverride("font_size", 56);
		engageLabel.AddThemeColorOverride("font_color", EngageGold);
		engageLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
		engageLabel.Position = new Vector2(-150, -35);
		engageLabel.CustomMinimumSize = new Vector2(300, 70);
		engageLabel.Modulate = new Color(1, 1, 1, 0); // starts invisible
		AddChild(engageLabel);

		// ─── VS ───
		var vsLabel = new Label();
		vsLabel.Text = "VS";
		vsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		vsLabel.VerticalAlignment = VerticalAlignment.Center;
		vsLabel.AddThemeFontSizeOverride("font_size", 20);
		vsLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
		vsLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
		vsLabel.Position = new Vector2(-25, -60);
		vsLabel.CustomMinimumSize = new Vector2(50, 30);
		vsLabel.Modulate = new Color(1, 1, 1, 0);
		AddChild(vsLabel);

		// ═══════════════════════════════════════════════
		//  ANIMATION TIMELINE
		// ═══════════════════════════════════════════════
		//  0.0s — dim + letterbox appear
		//  0.2s — banners slide in
		//  0.6s — VS appears
		//  1.0s — banners hold, ENGAGE slams in
		//  1.8s — everything fades out
		//  2.3s — intro finished signal

		var t = CreateTween();

		// Phase 1: Banners slide in (0.2 → 0.6)
		t.SetParallel(true);
		t.TweenProperty(leftBanner, "position:x", 40f, 0.4f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).SetDelay(0.2f);
		t.TweenProperty(rightBanner, "position:x", -340f, 0.4f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).SetDelay(0.2f);

		// Phase 2: VS fades in (0.6)
		t.TweenProperty(vsLabel, "modulate:a", 1f, 0.2f).SetDelay(0.6f);

		// Phase 3: ENGAGE slam (1.0)
		t.TweenProperty(engageLabel, "modulate:a", 1f, 0.15f).SetDelay(1.0f);
		// Scale punch — start big, snap to normal
		engageLabel.Scale = new Vector2(1.6f, 1.6f);
		engageLabel.PivotOffset = new Vector2(150, 35);
		t.TweenProperty(engageLabel, "scale", Vector2.One, 0.2f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back).SetDelay(1.0f);

		// Phase 4: Flash
		var flash = new ColorRect();
		flash.Color = new Color(1, 1, 1, 0);
		flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		flash.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(flash);
		t.TweenProperty(flash, "color:a", 0.3f, 0.08f).SetDelay(1.0f);
		t.TweenProperty(flash, "color:a", 0f, 0.2f).SetDelay(1.08f);

		// Phase 5: Hold, then fade everything out (1.8)
		t.SetParallel(false);
		t.TweenInterval(1.8f);

		t.SetParallel(true);
		t.TweenProperty(dim, "modulate:a", 0f, 0.4f);
		t.TweenProperty(leftBanner, "modulate:a", 0f, 0.3f);
		t.TweenProperty(rightBanner, "modulate:a", 0f, 0.3f);
		t.TweenProperty(engageLabel, "modulate:a", 0f, 0.3f);
		t.TweenProperty(vsLabel, "modulate:a", 0f, 0.3f);
		t.TweenProperty(slashLine, "modulate:a", 0f, 0.3f);
		t.TweenProperty(topBar, "position:y", -80f, 0.4f)
			.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
		t.TweenProperty(bottomBar, "offset_top", 0f, 0.4f)
			.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);

		// Phase 6: Done — signal and self-destruct
		t.SetParallel(false);
		t.TweenCallback(Callable.From(() =>
		{
			EmitSignal(SignalName.IntroFinished);
			QueueFree();
		}));
	}

	PanelContainer CreateBanner(string text, Color teamColor, bool isLeft)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(300, 60);
		var style = new StyleBoxFlat();
		style.BgColor = new Color(teamColor, 0.2f);
		if (isLeft)  { style.BorderWidthRight = 3; } 
		else         { style.BorderWidthLeft = 3; }
		style.BorderColor = teamColor;
		style.SetContentMarginAll(12);
		style.ContentMarginLeft = 24; style.ContentMarginRight = 24;
		panel.AddThemeStyleboxOverride("panel", style);

		var label = new Label();
		label.Text = text;
		label.HorizontalAlignment = isLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 22);
		label.AddThemeColorOverride("font_color", teamColor.Lightened(0.4f));
		panel.AddChild(label);

		return panel;
	}
}
