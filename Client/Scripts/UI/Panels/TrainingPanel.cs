using Godot;
using System.Text.Json;
using System.Collections.Generic;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Daily Training — matches Image 1 right side mockup:
/// "Allocate Training Points" header with TP badge,
/// each stat as a card with big number + efficiency label + plus button,
/// soft cap info, and "Apply Training (X TP)" button at bottom.
/// Hotkey: V
/// </summary>
public partial class TrainingPanel : WindowPanel
{
	private VBoxContainer _trainingContent;
	private Label _tpBadge;
	private Label _feedbackLabel;
	private Button _applyBtn;

	// Track pending allocations before applying
	private readonly Dictionary<string, int> _pendingPoints = new();
	private int _pendingTpCost = 0;

	public TrainingPanel()
	{
		WindowTitle = "Daily Training";
		DefaultSize = new Vector2(380, 560);
		DefaultPosition = new Vector2(460, 60);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_trainingContent = content;
		content.AddChild(PlaceholderText("Loading training data..."));
	}

	public override void OnOpen()
	{
		if (_trainingContent == null) return;

		foreach (var child in _trainingContent.GetChildren())
			child.QueueFree();

		_pendingPoints.Clear();
		_pendingTpCost = 0;

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null)
		{
			_trainingContent.AddChild(PlaceholderText("No character loaded."));
			return;
		}

		// ═══ HEADER ═══
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 8);
		_trainingContent.AddChild(headerRow);

		var title = new Label();
		title.Text = "Allocate Training Points";
		title.AddThemeFontSizeOverride("font_size", 17);
		title.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontTitleMedium != null) title.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		headerRow.AddChild(title);

		// TP badge
		_tpBadge = UITheme.CreateNumbers($"{p.DailyPointsRemaining} TP", 13, UITheme.Accent);
		headerRow.AddChild(_tpBadge);

		// Feedback label
		_feedbackLabel = UITheme.CreateBody("", 11, UITheme.Accent);
		_trainingContent.AddChild(_feedbackLabel);

		_trainingContent.AddChild(Spacer(4));

		// ═══ STAT CARDS ═══
		AddStatCard("Strength", "STR", "strength", p.Strength, p);
		AddStatCard("Speed", "SPD", "speed", p.Speed, p);
		AddStatCard("Agility", "AGI", "agility", p.Agility, p);
		AddStatCard("Endurance", "END", "endurance", p.Endurance, p);
		AddStatCard("Stamina", "STA", "stamina", p.Stamina, p);
		AddStatCard("Ether Control", "ETH", "ether_control", p.EtherControl, p);

		// ═══ SOFT CAP INFO ═══
		_trainingContent.AddChild(Spacer(4));
		var capInfo = UITheme.CreateDim("Soft caps apply when stats are 5+ points above your lowest stat.", 11);
		capInfo.AutowrapMode = TextServer.AutowrapMode.Word;
		_trainingContent.AddChild(capInfo);

		// ═══ APPLY BUTTON ═══
		_trainingContent.AddChild(Spacer(6));
		_applyBtn = UITheme.CreatePrimaryButton($"Apply Training (0 TP)", 14);
		_applyBtn.CustomMinimumSize = new Vector2(0, 44);
		_applyBtn.Disabled = true;
		_applyBtn.Pressed += ExecuteTrain;
		_trainingContent.AddChild(_applyBtn);
	}

	// ─── Stat card: Name (ABBR) + big value + efficiency + plus btn ───
	private readonly Dictionary<string, Label> _statValueLabels = new();
	private readonly Dictionary<string, Label> _efficiencyLabels = new();

	private void AddStatCard(string name, string abbr, string statKey, int value, Core.PlayerData p)
	{
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 2);
		_trainingContent.AddChild(card);

		// Top row: Name (ABBR) + plus button
		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 6);
		card.AddChild(topRow);

		var nameLabel = new Label();
		nameLabel.Text = name;
		nameLabel.AddThemeFontSizeOverride("font_size", 14);
		nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontBodyMedium != null) nameLabel.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		topRow.AddChild(nameLabel);

		var abbrLabel = UITheme.CreateDim($"({abbr})", 11);
		abbrLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		topRow.AddChild(abbrLabel);

		var spacer = new Control();
		spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		topRow.AddChild(spacer);

		var plusBtn = UITheme.CreateGhostButton("+", 18, UITheme.Accent);
		plusBtn.CustomMinimumSize = new Vector2(32, 32);
		string capturedKey = statKey;
		plusBtn.Pressed += () => OnPlusPressed(capturedKey);
		topRow.AddChild(plusBtn);

		// Bottom row: big value number + efficiency text
		var bottomRow = new HBoxContainer();
		bottomRow.AddThemeConstantOverride("separation", 16);
		card.AddChild(bottomRow);

		var valLabel = new Label();
		valLabel.Text = value.ToString();
		valLabel.AddThemeFontSizeOverride("font_size", 28);
		valLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontNumbersMedium != null) valLabel.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		bottomRow.AddChild(valLabel);
		_statValueLabels[statKey] = valLabel;

		// Efficiency label
		var effLabel = new Label();
		effLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		UpdateEfficiencyLabel(effLabel, value, p);
		bottomRow.AddChild(effLabel);
		_efficiencyLabels[statKey] = effLabel;

		// Separator between stats
		_trainingContent.AddChild(Spacer(2));
	}

	private void UpdateEfficiencyLabel(Label label, int statValue, Core.PlayerData p)
	{
		int gap = statValue - GetLowestStat(p);
		int cost;
		string effText;
		Color effColor;

		if (gap >= 20)
		{
			cost = 4;
			effText = $"⚠ 25% efficiency ({cost} pt)";
			effColor = UITheme.AccentRed;
		}
		else if (gap >= 10)
		{
			cost = 2;
			effText = $"⚠ 75% efficiency ({cost} pt)";
			effColor = UITheme.AccentGold;
		}
		else
		{
			cost = 1;
			effText = $"Normal ({cost} pt)";
			effColor = UITheme.TextDim;
		}

		label.Text = effText;
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", effColor);
		if (UITheme.FontBody != null) label.AddThemeFontOverride("font", UITheme.FontBody);
	}

	private void OnPlusPressed(string statKey)
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return;

		int currentVal = GetStatValue(statKey, p) + (_pendingPoints.GetValueOrDefault(statKey, 0));
		int gap = currentVal - GetLowestStat(p);
		int cost = gap >= 20 ? 4 : gap >= 10 ? 2 : 1;

		if (_pendingTpCost + cost > p.DailyPointsRemaining) return;

		_pendingPoints[statKey] = _pendingPoints.GetValueOrDefault(statKey, 0) + 1;
		_pendingTpCost += cost;

		// Update display
		if (_statValueLabels.TryGetValue(statKey, out var valLabel))
			valLabel.Text = (GetStatValue(statKey, p) + _pendingPoints[statKey]).ToString();

		_applyBtn.Text = $"Apply Training ({_pendingTpCost} TP)";
		_applyBtn.Disabled = false;
		_tpBadge.Text = $"{p.DailyPointsRemaining - _pendingTpCost} TP";
	}

	private async void ExecuteTrain()
	{
		var api = Networking.ApiClient.Instance;
		var gm = Core.GameManager.Instance;
		if (api == null || !api.IsLoggedIn || gm?.ActiveCharacterId == null) return;

		_applyBtn.Disabled = true;
		_feedbackLabel.Text = "Training...";
		_feedbackLabel.AddThemeColorOverride("font_color", UITheme.TextDim);

		// Send each pending stat allocation to server
		bool anyFailed = false;
		foreach (var (statKey, points) in _pendingPoints)
		{
			if (points <= 0) continue;

			for (int i = 0; i < points; i++)
			{
				var resp = await api.TrainStat(gm.ActiveCharacterId, statKey, 1);

				if (resp.Success)
				{
					using var doc = JsonDocument.Parse(resp.Body);
					var c = doc.RootElement.GetProperty("character");

					var p = gm.ActiveCharacter;
					p.Strength = c.GetProperty("strength").GetInt32();
					p.Speed = c.GetProperty("speed").GetInt32();
					p.Agility = c.GetProperty("agility").GetInt32();
					p.Endurance = c.GetProperty("endurance").GetInt32();
					p.Stamina = c.GetProperty("stamina").GetInt32();
					p.EtherControl = c.GetProperty("ether_control").GetInt32();
					p.DailyPointsRemaining = c.GetProperty("daily_points_remaining").GetInt32();
					p.CurrentHp = c.GetProperty("current_hp").GetInt32();
					p.CurrentEther = c.GetProperty("current_ether").GetInt32();
				}
				else
				{
					anyFailed = true;
					_feedbackLabel.Text = resp.Error;
					_feedbackLabel.AddThemeColorOverride("font_color", UITheme.Error);
					break;
				}
			}
			if (anyFailed) break;
		}

		if (!anyFailed)
		{
			_feedbackLabel.Text = "Training complete!";
			_feedbackLabel.AddThemeColorOverride("font_color", UITheme.AccentGreen);
		}

		// Refresh the panel
		OnOpen();
	}

	// ─── Helpers ───

	private int GetLowestStat(Core.PlayerData p)
	{
		int min = p.Strength;
		if (p.Speed < min) min = p.Speed;
		if (p.Agility < min) min = p.Agility;
		if (p.Endurance < min) min = p.Endurance;
		if (p.Stamina < min) min = p.Stamina;
		if (p.EtherControl < min) min = p.EtherControl;
		return min;
	}

	private int GetStatValue(string statKey, Core.PlayerData p)
	{
		return statKey switch
		{
			"strength" => p.Strength,
			"speed" => p.Speed,
			"agility" => p.Agility,
			"endurance" => p.Endurance,
			"stamina" => p.Stamina,
			"ether_control" => p.EtherControl,
			_ => 0
		};
	}
}
