using Godot;
using System.Text.Json;
using System.Collections.Generic;

namespace ProjectTactics.UI.Panels;

public partial class TrainingPanel : WindowPanel
{
	private VBoxContainer _trainingContent;
	private Label _tpBadge;
	private Label _feedbackLabel;
	private Button _applyBtn;

	private readonly Dictionary<string, int> _pendingPoints = new();
	private int _pendingTpCost = 0;
	private bool _applyingBatch = false;

	public TrainingPanel()
	{
		WindowTitle = "Daily Training";
		DefaultSize = new Vector2(340, 480);
		DefaultPosition = new Vector2(460, 60);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_trainingContent = content;
		content.AddThemeConstantOverride("separation", 0);
		content.AddChild(PlaceholderText("Loading training data..."));
	}

	protected override void OnDataChanged()
	{
		if (_applyingBatch) return;
		base.OnDataChanged();
	}

	public override void OnOpen()
	{
		base.OnOpen();
		RebuildTraining();
	}

	private void RebuildTraining()
	{
		if (_trainingContent == null) return;
		foreach (var child in _trainingContent.GetChildren()) child.QueueFree();
		_pendingPoints.Clear();
		_pendingTpCost = 0;
		_statValueLabels.Clear();
		_efficiencyLabels.Clear();

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) { _trainingContent.AddChild(PlaceholderText("No character loaded.")); return; }

		// ═══ HEADER ═══
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 6);
		_trainingContent.AddChild(headerRow);

		var title = new Label();
		title.Text = "Allocate Training Points";
		title.AddThemeFontSizeOverride("font_size", 14);
		title.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontBodyMedium != null) title.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		headerRow.AddChild(title);

		_tpBadge = UITheme.CreateNumbers($"{p.TrainingPointsBank} TP", 12, UITheme.Accent);
		headerRow.AddChild(_tpBadge);

		_feedbackLabel = UITheme.CreateBody("", 10, UITheme.Accent);
		_trainingContent.AddChild(_feedbackLabel);

		_trainingContent.AddChild(Spacer(4));
		_trainingContent.AddChild(ThinSeparator());

		// ═══ STAT ROWS — compact single-line each ═══
		AddStatRow("Strength", "STR", "strength", p.Strength, p);
		AddStatRow("Vitality", "VIT", "vitality", p.Vitality, p);
		AddStatRow("Agility", "AGI", "agility", p.Agility, p);
		AddStatRow("Dexterity", "DEX", "dexterity", p.Dexterity, p);
		AddStatRow("Mind", "MND", "mind", p.Mind, p);
		AddStatRow("Ether Control", "ETH", "ether_control", p.EtherControl, p);

		// ═══ SOFT CAP INFO ═══
		_trainingContent.AddChild(Spacer(4));
		var capInfo = UITheme.CreateDim("Soft caps apply 5+ points above your lowest stat.", 10);
		capInfo.AutowrapMode = TextServer.AutowrapMode.Word;
		_trainingContent.AddChild(capInfo);

		// ═══ APPLY BUTTON ═══
		_trainingContent.AddChild(Spacer(6));
		_applyBtn = UITheme.CreatePrimaryButton($"Apply Training (0 TP)", 12);
		_applyBtn.CustomMinimumSize = new Vector2(0, 36);
		_applyBtn.Disabled = true;
		_applyBtn.Pressed += ExecuteTrain;
		_trainingContent.AddChild(_applyBtn);
	}

	private readonly Dictionary<string, Label> _statValueLabels = new();
	private readonly Dictionary<string, Label> _efficiencyLabels = new();

	private void AddStatRow(string name, string abbr, string statKey, int value, Core.PlayerData p)
	{
		// Each stat: one main row + efficiency subtitle, separated by thin line
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 0);
		_trainingContent.AddChild(card);

		// Main row: Name (ABBR)   value   [+]
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		row.CustomMinimumSize = new Vector2(0, 32);
		card.AddChild(row);

		var nameLabel = new Label();
		nameLabel.Text = name;
		nameLabel.AddThemeFontSizeOverride("font_size", 13);
		nameLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontBodyMedium != null) nameLabel.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		row.AddChild(nameLabel);

		var abbrLabel = UITheme.CreateDim($"({abbr})", 10);
		abbrLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(abbrLabel);

		var spacer = new Control();
		spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(spacer);

		var valLabel = new Label();
		valLabel.Text = value.ToString();
		valLabel.AddThemeFontSizeOverride("font_size", 18);
		valLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontNumbersMedium != null) valLabel.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		valLabel.CustomMinimumSize = new Vector2(32, 0);
		valLabel.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(valLabel);
		_statValueLabels[statKey] = valLabel;

		var plusBtn = UITheme.CreateGhostButton("+", 16, UITheme.Accent);
		plusBtn.CustomMinimumSize = new Vector2(28, 28);
		string capturedKey = statKey;
		plusBtn.Pressed += () => OnPlusPressed(capturedKey);
		row.AddChild(plusBtn);

		// Efficiency label below
		var effLabel = new Label();
		UpdateEfficiencyLabel(effLabel, value, p);
		card.AddChild(effLabel);
		_efficiencyLabels[statKey] = effLabel;

		_trainingContent.AddChild(ThinSeparator());
	}

	private void UpdateEfficiencyLabel(Label label, int statValue, Core.PlayerData p)
	{
		int gap = statValue - GetLowestStat(p);
		int cost; string effText; Color effColor;

		if (gap >= 20)      { cost = 4; effText = $"⚠ 25% eff ({cost} pt)"; effColor = UITheme.AccentRed; }
		else if (gap >= 10) { cost = 2; effText = $"⚠ 75% eff ({cost} pt)"; effColor = UITheme.AccentGold; }
		else                { cost = 1; effText = $"Normal ({cost} pt)"; effColor = UITheme.TextDim; }

		label.Text = effText;
		label.AddThemeFontSizeOverride("font_size", 10);
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

		if (_pendingTpCost + cost > p.TrainingPointsBank) return;

		_pendingPoints[statKey] = _pendingPoints.GetValueOrDefault(statKey, 0) + 1;
		_pendingTpCost += cost;

		if (_statValueLabels.TryGetValue(statKey, out var valLabel))
			valLabel.Text = (GetStatValue(statKey, p) + _pendingPoints[statKey]).ToString();

		_applyBtn.Text = $"Apply Training ({_pendingTpCost} TP)";
		_applyBtn.Disabled = false;
		_tpBadge.Text = $"{p.TrainingPointsBank - _pendingTpCost} TP";
	}

	private async void ExecuteTrain()
	{
		var api = Networking.ApiClient.Instance;
		var gm = Core.GameManager.Instance;
		if (api == null || !api.IsLoggedIn || gm?.ActiveCharacterId == null) return;

		_applyBtn.Disabled = true;
		_applyingBatch = true;
		_feedbackLabel.Text = "Training...";
		_feedbackLabel.AddThemeColorOverride("font_color", UITheme.TextDim);

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
					p.Vitality = c.GetProperty("vitality").GetInt32();
					p.Agility = c.GetProperty("agility").GetInt32();
					p.Dexterity = c.GetProperty("dexterity").GetInt32();
					p.Mind = c.GetProperty("mind").GetInt32();
					p.EtherControl = c.GetProperty("ether_control").GetInt32();
					p.TrainingPointsBank = c.GetProperty("daily_points_remaining").GetInt32();
					p.CurrentHp = c.GetProperty("current_hp").GetInt32();
					p.CurrentAether = c.GetProperty("current_ether").GetInt32();
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

		_applyingBatch = false;
		if (!anyFailed)
		{
			_feedbackLabel.Text = "Training complete!";
			_feedbackLabel.AddThemeColorOverride("font_color", UITheme.AccentGreen);
		}
		RebuildTraining();
	}

	private int GetLowestStat(Core.PlayerData p)
	{
		int min = p.Strength;
		if (p.Vitality < min) min = p.Vitality;
		if (p.Agility < min) min = p.Agility;
		if (p.Dexterity < min) min = p.Dexterity;
		if (p.Mind < min) min = p.Mind;
		if (p.EtherControl < min) min = p.EtherControl;
		return min;
	}

	private int GetStatValue(string statKey, Core.PlayerData p) => statKey switch
	{
		"strength" => p.Strength, "vitality" => p.Vitality,
		"agility" => p.Agility, "dexterity" => p.Dexterity,
		"mind" => p.Mind, "ether_control" => p.EtherControl,
		_ => 0
	};
}
