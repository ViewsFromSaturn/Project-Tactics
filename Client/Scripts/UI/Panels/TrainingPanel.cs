using Godot;
using System.Text.Json;
using System.Collections.Generic;
using ProjectTactics.Systems;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Training panel — allocate banked TP to stats.
/// Shows 12:00 PM EST countdown, soft cap costs, RP session progress.
/// All actual training goes through server API (cheat-proof).
/// </summary>
public partial class TrainingPanel : WindowPanel
{
	private VBoxContainer _trainingContent;
	private Label _tpBadge;
	private Label _feedbackLabel;
	private Label _countdownLabel;
	private Label _rpProgressLabel;
	private Button _applyBtn;

	private readonly Dictionary<string, int> _pendingPoints = new();
	private readonly Dictionary<string, Label> _statValueLabels = new();
	private readonly Dictionary<string, Label> _efficiencyLabels = new();
	private readonly Dictionary<string, Label> _costLabels = new();
	private int _pendingTpCost = 0;
	private bool _applyingBatch = false;

	// Countdown timer
	private double _countdownTimer = 0;

	public TrainingPanel()
	{
		WindowTitle = "Daily Training";
		DefaultSize = new Vector2(360, 520);
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
		// Client-side reset check (server is authoritative, this is just for display)
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p != null)
		{
			var trainer = new DailyTraining();
			trainer.TryDailyReset(p);
		}
		RebuildTraining();
	}

	public override void _Process(double delta)
	{
		// Update countdown every second
		_countdownTimer += delta;
		if (_countdownTimer >= 1.0 && _countdownLabel != null)
		{
			_countdownTimer = 0;
			int secs = DailyTraining.SecondsUntilReset();
			_countdownLabel.Text = $"Reset in {DailyTraining.FormatCountdown(secs)}";
		}
	}

	// ═════════════════════════════════════════════════════════
	//  BUILD
	// ═════════════════════════════════════════════════════════

	private void RebuildTraining()
	{
		if (_trainingContent == null) return;
		foreach (var child in _trainingContent.GetChildren()) child.QueueFree();
		_pendingPoints.Clear();
		_pendingTpCost = 0;
		_statValueLabels.Clear();
		_efficiencyLabels.Clear();
		_costLabels.Clear();

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null)
		{
			_trainingContent.AddChild(PlaceholderText("No character loaded."));
			return;
		}

		// ═══ HEADER: Title + TP Badge ═══
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

		// ═══ EST COUNTDOWN + RP PROGRESS ═══
		var infoRow = new HBoxContainer();
		infoRow.AddThemeConstantOverride("separation", 4);
		_trainingContent.AddChild(infoRow);

		int secs = DailyTraining.SecondsUntilReset();
		_countdownLabel = UITheme.CreateDim($"Reset in {DailyTraining.FormatCountdown(secs)}", 10);
		_countdownLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		infoRow.AddChild(_countdownLabel);

		int dailyCap = p.DailyTpCap;
		int dailyEarned = p.DailyTpEarned;
		string rpText = $"RP: {dailyEarned}/{dailyCap} earned today";
		Color rpColor = dailyEarned >= dailyCap ? UITheme.AccentGreen : UITheme.TextSecondary;
		_rpProgressLabel = UITheme.CreateBody(rpText, 10, rpColor);
		infoRow.AddChild(_rpProgressLabel);

		// Feedback label
		_feedbackLabel = UITheme.CreateBody("", 10, UITheme.Accent);
		_trainingContent.AddChild(_feedbackLabel);

		_trainingContent.AddChild(Spacer(4));
		_trainingContent.AddChild(ThinSeparator());

		// ═══ LEVEL + STATS INFO ═══
		var levelRow = new HBoxContainer();
		levelRow.AddThemeConstantOverride("separation", 4);
		_trainingContent.AddChild(levelRow);

		var lvlLabel = UITheme.CreateBody($"Character Level: {p.CharacterLevel}", 11, UITheme.TextSecondary);
		lvlLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		levelRow.AddChild(lvlLabel);

		int lowest = GetLowestStat(p);
		var lowLabel = UITheme.CreateDim($"Lowest stat: {lowest}", 10);
		levelRow.AddChild(lowLabel);

		_trainingContent.AddChild(Spacer(2));

		// ═══ STAT ROWS ═══
		AddStatRow("Strength", "STR", "strength", p.Strength, p);
		AddStatRow("Vitality", "VIT", "vitality", p.Vitality, p);
		AddStatRow("Agility", "AGI", "agility", p.Agility, p);
		AddStatRow("Dexterity", "DEX", "dexterity", p.Dexterity, p);
		AddStatRow("Mind", "MND", "mind", p.Mind, p);
		AddStatRow("Ether Control", "ETH", "ether_control", p.EtherControl, p);

		// ═══ SOFT CAP LEGEND ═══
		_trainingContent.AddChild(Spacer(4));
		var capInfo = UITheme.CreateDim(
			"Soft cap: gap 0-9 = 1 TP, gap 10-19 = 2 TP, gap 20+ = 4 TP per point.", 10);
		capInfo.AutowrapMode = TextServer.AutowrapMode.Word;
		_trainingContent.AddChild(capInfo);

		var carryInfo = UITheme.CreateDim("Unspent TP carries over between days.", 10);
		_trainingContent.AddChild(carryInfo);

		// ═══ APPLY BUTTON ═══
		_trainingContent.AddChild(Spacer(6));
		_applyBtn = UITheme.CreatePrimaryButton("Apply Training (0 TP)", 12);
		_applyBtn.CustomMinimumSize = new Vector2(0, 36);
		_applyBtn.Disabled = true;
		_applyBtn.Pressed += ExecuteTrain;
		_trainingContent.AddChild(_applyBtn);
	}

	// ═════════════════════════════════════════════════════════
	//  STAT ROW
	// ═════════════════════════════════════════════════════════

	private void AddStatRow(string name, string abbr, string statKey, int value, Core.PlayerData p)
	{
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 0);
		_trainingContent.AddChild(card);

		// Main row: Name (ABBR)   value   cost   [+]
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

		// Value
		var valLabel = new Label();
		valLabel.Text = value.ToString();
		valLabel.AddThemeFontSizeOverride("font_size", 18);
		valLabel.AddThemeColorOverride("font_color", UITheme.TextBright);
		if (UITheme.FontNumbersMedium != null) valLabel.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		valLabel.CustomMinimumSize = new Vector2(32, 0);
		valLabel.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(valLabel);
		_statValueLabels[statKey] = valLabel;

		// + Button
		var plusBtn = UITheme.CreateGhostButton("+", 16, UITheme.Accent);
		plusBtn.CustomMinimumSize = new Vector2(28, 28);
		string capturedKey = statKey;
		plusBtn.Pressed += () => OnPlusPressed(capturedKey);
		row.AddChild(plusBtn);

		// Efficiency + cost label below
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

		if (gap >= 20)
		{
			cost = 4;
			effText = $"  ⚠ 25% eff — costs {cost} TP per point";
			effColor = UITheme.AccentRuby;
		}
		else if (gap >= 10)
		{
			cost = 2;
			effText = $"  ⚠ 50% eff — costs {cost} TP per point";
			effColor = UITheme.AccentGold;
		}
		else
		{
			cost = 1;
			effText = $"  100% eff — costs {cost} TP per point";
			effColor = UITheme.TextDim;
		}

		label.Text = effText;
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", effColor);
		if (UITheme.FontBody != null) label.AddThemeFontOverride("font", UITheme.FontBody);
	}

	// ═════════════════════════════════════════════════════════
	//  + BUTTON — queue pending allocation
	// ═════════════════════════════════════════════════════════

	private void OnPlusPressed(string statKey)
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return;

		// Calculate cost using PENDING values for lowest stat
		int currentVal = GetStatValue(statKey, p) + _pendingPoints.GetValueOrDefault(statKey, 0);
		int lowest = GetLowestStatWithPending(p);
		int gap = currentVal - lowest;
		int cost = gap >= 20 ? 4 : gap >= 10 ? 2 : 1;

		if (_pendingTpCost + cost > p.TrainingPointsBank) return;

		_pendingPoints[statKey] = _pendingPoints.GetValueOrDefault(statKey, 0) + 1;
		_pendingTpCost += cost;

		// Update value display
		if (_statValueLabels.TryGetValue(statKey, out var valLabel))
			valLabel.Text = (GetStatValue(statKey, p) + _pendingPoints[statKey]).ToString();

		// Refresh ALL efficiency labels — raising one stat can change the gap for others
		RefreshAllEfficiencyLabels(p);

		_applyBtn.Text = $"Apply Training ({_pendingTpCost} TP)";
		_applyBtn.Disabled = false;
		_tpBadge.Text = $"{p.TrainingPointsBank - _pendingTpCost} TP";
	}

	/// <summary>Refresh every stat's efficiency label using pending values.</summary>
	private void RefreshAllEfficiencyLabels(Core.PlayerData p)
	{
		string[] keys = { "strength", "vitality", "agility", "dexterity", "mind", "ether_control" };
		int lowest = GetLowestStatWithPending(p);
		foreach (var key in keys)
		{
			if (!_efficiencyLabels.TryGetValue(key, out var effLabel)) continue;
			int val = GetStatValue(key, p) + _pendingPoints.GetValueOrDefault(key, 0);
			UpdateEfficiencyLabelFromGap(effLabel, val - lowest);
		}
	}

	/// <summary>Update an efficiency label from a pre-calculated gap value.</summary>
	private void UpdateEfficiencyLabelFromGap(Label label, int gap)
	{
		int cost; string effText; Color effColor;
		if (gap >= 20)
		{
			cost = 4;
			effText = $"  ⚠ 25% eff — costs {cost} TP per point";
			effColor = UITheme.AccentRuby;
		}
		else if (gap >= 10)
		{
			cost = 2;
			effText = $"  ⚠ 50% eff — costs {cost} TP per point";
			effColor = UITheme.AccentGold;
		}
		else
		{
			cost = 1;
			effText = $"  100% eff — costs {cost} TP per point";
			effColor = UITheme.TextDim;
		}
		label.Text = effText;
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", effColor);
		if (UITheme.FontBody != null) label.AddThemeFontOverride("font", UITheme.FontBody);
	}

	// ═════════════════════════════════════════════════════════
	//  EXECUTE — server-authoritative training
	// ═════════════════════════════════════════════════════════

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

			// Send all points for this stat in one request
			var resp = await api.TrainStat(gm.ActiveCharacterId, statKey, points);
			if (resp.Success)
			{
				using var doc = JsonDocument.Parse(resp.Body);
				var root = doc.RootElement;

				// Server returns the full updated character
				if (root.TryGetProperty("character", out var c))
				{
					var p = gm.ActiveCharacter;
					p.Strength = c.GetProperty("strength").GetInt32();
					p.Vitality = c.GetProperty("vitality").GetInt32();
					p.Agility = c.GetProperty("agility").GetInt32();
					p.Dexterity = c.GetProperty("dexterity").GetInt32();
					p.Mind = c.GetProperty("mind").GetInt32();
					p.EtherControl = c.GetProperty("ether_control").GetInt32();
					p.TrainingPointsBank = c.GetProperty("training_points_bank").GetInt32();
					p.CurrentHp = c.GetProperty("current_hp").GetInt32();
					p.CurrentStamina = c.GetProperty("current_stamina").GetInt32();
					p.CurrentAether = c.GetProperty("current_aether").GetInt32();

					if (c.TryGetProperty("daily_tp_earned", out var dte))
						p.DailyTpEarned = dte.GetInt32();
					if (c.TryGetProperty("daily_rp_sessions", out var drs))
						p.DailyRpSessions = drs.GetInt32();
					if (c.TryGetProperty("last_reset_date", out var lrd))
						p.LastResetDate = lrd.GetString();
				}

				// Log server message
				if (root.TryGetProperty("message", out var msg))
					GD.Print($"[Training] {msg.GetString()}");
			}
			else
			{
				anyFailed = true;
				_feedbackLabel.Text = resp.Error;
				_feedbackLabel.AddThemeColorOverride("font_color", UITheme.AccentRuby);
				break;
			}
		}

		_applyingBatch = false;
		if (!anyFailed)
		{
			_feedbackLabel.Text = "Training complete!";
			_feedbackLabel.AddThemeColorOverride("font_color", UITheme.AccentGreen);
		}
		RebuildTraining();
	}

	// ═════════════════════════════════════════════════════════
	//  HELPERS
	// ═════════════════════════════════════════════════════════

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

	/// <summary>Lowest stat including pending (uncommitted) allocations.</summary>
	private int GetLowestStatWithPending(Core.PlayerData p)
	{
		string[] keys = { "strength", "vitality", "agility", "dexterity", "mind", "ether_control" };
		int min = int.MaxValue;
		foreach (var k in keys)
		{
			int val = GetStatValue(k, p) + _pendingPoints.GetValueOrDefault(k, 0);
			if (val < min) min = val;
		}
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
