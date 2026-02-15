using Godot;

namespace NarutoRP.UI;

/// <summary>
/// Debug HUD overlay for testing.
/// Shows player stats, daily points, derived stats in real-time.
/// Toggle with F1 key. Remove this before production.
///
/// Attach to a CanvasLayer > Control node.
/// </summary>
public partial class DebugOverlay : Control
{
	private Label _statsLabel;
	private bool _visible = true;

	public override void _Ready()
	{
		// Create label dynamically
		_statsLabel = new Label();
		_statsLabel.Position = new Vector2(10, 10);
		_statsLabel.AddThemeColorOverride("font_color", Colors.LimeGreen);
		_statsLabel.AddThemeFontSizeOverride("font_size", 14);
		AddChild(_statsLabel);
	}

	public override void _Process(double delta)
	{
		if (!_visible || Core.GameManager.Instance?.ActiveCharacter == null)
		{
			_statsLabel.Text = "";
			return;
		}

		var p = Core.GameManager.Instance.ActiveCharacter;

		_statsLabel.Text =
$@"═══ {p.CharacterName} ═══
Clan: {p.ClanName} | Village: {p.Village}
Rank: {p.RpRank} | Char Lv: {p.CharacterLevel}
Daily Pts: {p.DailyPointsRemaining}

── TRAINING STATS ──
STR: {p.Strength}  SPD: {p.Speed}  AGI: {p.Agility}
END: {p.Endurance}  STA: {p.Stamina}  CKC: {p.ChakraControl}

── DERIVED STATS ──
HP:  {p.CurrentHp}/{p.MaxHp}   CKR: {p.CurrentChakra}/{p.MaxChakra}
ATK: {p.Atk}  DEF: {p.Def}  JATK: {p.Jatk}  JDEF: {p.Jdef}
AVD: {p.Avd}  ACC: {p.Acc}  CRIT: {p.CritPercent}%
MOVE: {p.Move}  JUMP: {p.Jump}  RT: {p.BaseRt}
Regen: {p.ChakraRegen}/turn

── CONTROLS ──
WASD/Arrows: Move | Shift: Run
F1: Toggle Debug | F3: Save | F4: Load";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed)
		{
			switch (key.Keycode)
			{
				case Key.F1:
					_visible = !_visible;
					break;

				case Key.F3:
					TestSave();
					break;

				case Key.F4:
					TestLoad();
					break;
			}
		}
	}

	private void TestSave()
	{
		var gm = Core.GameManager.Instance;
		if (gm?.ActiveCharacter == null) return;
		gm.SaveCharacter(gm.ActiveCharacter, "1");
		GD.Print("[Debug] Saved!");
	}

	private void TestLoad()
	{
		var gm = Core.GameManager.Instance;
		if (gm == null) return;

		var data = gm.LoadCharacterFromFile("1");
		if (data != null)
		{
			gm.LoadCharacter(data, "debug");
			GD.Print("[Debug] Loaded!");
		}
		else
		{
			GD.Print("[Debug] No save found!");
		}
	}
}
