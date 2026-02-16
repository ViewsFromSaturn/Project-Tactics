using Godot;
using System.Text.Json;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Training Panel — daily TP allocation screen.
/// Shows a confirmation popup before each stat point spend.
/// Each confirmed click calls the server's TrainStat endpoint (server-authoritative).
/// Hotkey: V | Slides: Right
/// </summary>
public partial class TrainingPanel : SlidePanel
{
	private VBoxContainer _trainingContent;
	private Label _tpRemainingLabel;
	private Label _levelLabel;
	private Label _feedbackLabel;

	// Confirm dialog refs
	private PanelContainer _confirmDialog;
	private Label _confirmText;
	private string _pendingStat;
	private Label _pendingValLabel;
	private Button _pendingBtn;

	public TrainingPanel()
	{
		PanelTitle = "Training";
		Direction = SlideDirection.Right;
		PanelWidth = 340;
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_trainingContent = content;
		content.AddChild(PlaceholderText("Open with a character loaded to train."));
		BuildConfirmDialog();
	}

	protected override void OnOpen()
	{
		foreach (var child in _trainingContent.GetChildren())
			child.QueueFree();

		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null)
		{
			_trainingContent.AddChild(PlaceholderText("No character loaded."));
			return;
		}

		// Header info
		_levelLabel = UITheme.CreateBody($"Character Level: {p.CharacterLevel}", 13, UITheme.Text);
		_trainingContent.AddChild(_levelLabel);

		_tpRemainingLabel = UITheme.CreateNumbers($"{p.DailyPointsRemaining} TP remaining", 16, UITheme.Accent);
		_trainingContent.AddChild(_tpRemainingLabel);

		// Feedback label for server responses
		_feedbackLabel = UITheme.CreateBody("", 11, UITheme.Accent);
		_trainingContent.AddChild(_feedbackLabel);

		_trainingContent.AddChild(UITheme.CreateSpacer(4));
		_trainingContent.AddChild(ThinSeparator());
		_trainingContent.AddChild(SectionHeader("Allocate Points"));

		// Stat rows with + buttons
		AddStatAllocRow("Strength", "strength");
		AddStatAllocRow("Speed", "speed");
		AddStatAllocRow("Agility", "agility");
		AddStatAllocRow("Endurance", "endurance");
		AddStatAllocRow("Stamina", "stamina");
		AddStatAllocRow("Ether Control", "ether_control");

		_trainingContent.AddChild(UITheme.CreateSpacer(8));
		_trainingContent.AddChild(ThinSeparator());

		// Soft cap info
		_trainingContent.AddChild(SectionHeader("Soft Cap"));
		var capInfo = UITheme.CreateBody(
			"Stats that exceed your lowest stat by 10+ cost more TP.\n" +
			"0-9 gap: 1 TP · 10-19 gap: 2 TP · 20+ gap: 4 TP", 11, UITheme.TextDim);
		capInfo.AutowrapMode = TextServer.AutowrapMode.Word;
		_trainingContent.AddChild(capInfo);
	}

	private void AddStatAllocRow(string name, string statKey)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		_trainingContent.AddChild(row);

		var nameLabel = UITheme.CreateBody(name, 13, UITheme.Text);
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(nameLabel);

		var valLabel = UITheme.CreateNumbers(GetStatValue(statKey).ToString(), 15, UITheme.TextBright);
		valLabel.HorizontalAlignment = HorizontalAlignment.Center;
		valLabel.CustomMinimumSize = new Vector2(36, 0);
		row.AddChild(valLabel);

		// Calculate and show cost
		var costLabel = UITheme.CreateDim(GetCostText(statKey), 10);
		costLabel.CustomMinimumSize = new Vector2(40, 0);
		costLabel.HorizontalAlignment = HorizontalAlignment.Center;
		row.AddChild(costLabel);

		var plusBtn = UITheme.CreateGhostButton("+", 14, UITheme.Accent);
		plusBtn.CustomMinimumSize = new Vector2(28, 28);
		plusBtn.Pressed += () => ShowConfirm(name, statKey, valLabel, plusBtn);
		row.AddChild(plusBtn);
	}

	private string GetCostText(string statKey)
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return "";
		int gap = GetStatValue(statKey) - GetLowestStat();
		if (gap >= 20) return "(4 TP)";
		if (gap >= 10) return "(2 TP)";
		return "(1 TP)";
	}

	private int GetLowestStat()
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return 0;
		int min = p.Strength;
		if (p.Speed < min) min = p.Speed;
		if (p.Agility < min) min = p.Agility;
		if (p.Endurance < min) min = p.Endurance;
		if (p.Stamina < min) min = p.Stamina;
		if (p.EtherControl < min) min = p.EtherControl;
		return min;
	}

	private int GetStatValue(string statKey)
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null) return 0;
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

	// ═════════════════════════════════════════════════════════
	//  CONFIRMATION DIALOG
	// ═════════════════════════════════════════════════════════

	private void BuildConfirmDialog()
	{
		_confirmDialog = new PanelContainer();
		_confirmDialog.Visible = false;
		_confirmDialog.MouseFilter = MouseFilterEnum.Stop;
		_confirmDialog.ZIndex = 10;

		// Center in panel
		_confirmDialog.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		_confirmDialog.GrowHorizontal = GrowDirection.Both;
		_confirmDialog.GrowVertical = GrowDirection.Both;
		_confirmDialog.OffsetLeft = -120; _confirmDialog.OffsetRight = 120;
		_confirmDialog.OffsetTop = -60; _confirmDialog.OffsetBottom = 60;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.047f, 0.047f, 0.078f, 0.95f);
		style.SetCornerRadiusAll(8);
		style.BorderColor = new Color(0.235f, 0.255f, 0.314f, 0.5f);
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 16; style.ContentMarginRight = 16;
		style.ContentMarginTop = 14; style.ContentMarginBottom = 14;
		_confirmDialog.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		_confirmDialog.AddChild(vbox);

		_confirmText = UITheme.CreateBody("Spend 1 TP on Strength?", 13, UITheme.TextBright);
		_confirmText.HorizontalAlignment = HorizontalAlignment.Center;
		_confirmText.AutowrapMode = TextServer.AutowrapMode.Word;
		vbox.AddChild(_confirmText);

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 10);
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(btnRow);

		var cancelBtn = UITheme.CreateSecondaryButton("Cancel", 12);
		cancelBtn.CustomMinimumSize = new Vector2(80, 32);
		cancelBtn.Pressed += HideConfirm;
		btnRow.AddChild(cancelBtn);

		var confirmBtn = UITheme.CreatePrimaryButton("Confirm", 12);
		confirmBtn.CustomMinimumSize = new Vector2(80, 32);
		confirmBtn.Pressed += ExecuteTrain;
		btnRow.AddChild(confirmBtn);

		// Add as overlay on top of the panel itself
		AddChild(_confirmDialog);
	}

	private void ShowConfirm(string name, string statKey, Label valLabel, Button btn)
	{
		var p = Core.GameManager.Instance?.ActiveCharacter;
		if (p == null || p.DailyPointsRemaining <= 0) return;

		_pendingStat = statKey;
		_pendingValLabel = valLabel;
		_pendingBtn = btn;

		int gap = GetStatValue(statKey) - GetLowestStat();
		int cost = gap >= 20 ? 4 : gap >= 10 ? 2 : 1;
		_confirmText.Text = $"Spend {cost} TP on {name}?\n({p.DailyPointsRemaining} TP remaining)";
		_confirmDialog.Visible = true;
	}

	private void HideConfirm()
	{
		_confirmDialog.Visible = false;
		_pendingStat = null;
		_pendingValLabel = null;
		_pendingBtn = null;
	}

	private async void ExecuteTrain()
	{
		if (_pendingStat == null) return;

		string stat = _pendingStat;
		Label valLabel = _pendingValLabel;
		Button btn = _pendingBtn;
		HideConfirm();

		var api = Networking.ApiClient.Instance;
		var gm = Core.GameManager.Instance;
		if (api == null || !api.IsLoggedIn || gm?.ActiveCharacterId == null) return;

		if (btn != null) btn.Disabled = true;
		_feedbackLabel.Text = "Training...";
		_feedbackLabel.AddThemeColorOverride("font_color", UITheme.TextDim);

		var resp = await api.TrainStat(gm.ActiveCharacterId, stat, 1);

		if (btn != null) btn.Disabled = false;

		if (resp.Success)
		{
			// Update local PlayerData from server response
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

			// Rebuild entire panel to refresh all values and costs
			OnOpen();

			string msg = doc.RootElement.GetProperty("message").GetString();
			_feedbackLabel.Text = msg;
			_feedbackLabel.AddThemeColorOverride("font_color", UITheme.Accent);
		}
		else
		{
			_feedbackLabel.Text = resp.Error;
			_feedbackLabel.AddThemeColorOverride("font_color", UITheme.Error);
		}
	}
}
