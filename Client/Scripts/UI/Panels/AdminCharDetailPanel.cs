using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Per-character admin detail window. Opened from context menu or server tools search.
/// Shows identity, editable stats, RPP/TP controls, rank, ban, reset.
/// </summary>
public partial class AdminCharDetailPanel : WindowPanel
{
	// ─── THEME ───
	static Color BdAccent => UITheme.Accent;
	static Color ColGold => new("D4A843");
	static Color ColGreen => new("66BB6A");
	static Color ColRed => new("CC4444");
	static Color TxBright => UITheme.TextBright;
	static Color TxDim => UITheme.TextDim;
	static Color TxSec => UITheme.TextSecondary;

	// ─── STATE ───
	string _charId = "";
	string _charName = "";
	string _accountId = "";
	bool _loading = false;

	// ─── FIELDS ───
	Label _headerName, _headerInfo, _feedbackLabel;
	readonly Dictionary<string, LineEdit> _statFields = new();
	LineEdit _rppField, _tpField, _levelField;
	OptionButton _rankDrop;
	VBoxContainer _root;

	static readonly string[] StatKeys = { "strength", "vitality", "dexterity", "agility", "ether_control", "mind" };
	static readonly string[] StatAbbr = { "STR", "VIT", "DEX", "AGI", "ETC", "MND" };
	static readonly string[] Ranks = { "Aspirant", "Sworn", "Warden", "Banneret", "Justicar" };

	public AdminCharDetailPanel()
	{
		WindowTitle = "Admin Details";
		DefaultSize = new Vector2(380, 560);
		DefaultPosition = new Vector2(480, 40);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_root = content;
		content.AddThemeConstantOverride("separation", 6);
		content.AddChild(PlaceholderText("Select a character..."));
	}

	/// <summary>Open this panel for a specific character ID.</summary>
	public void OpenForCharacter(string characterId, string characterName = "")
	{
		_charId = characterId;
		_charName = characterName;
		WindowTitle = $"Admin: {characterName}";

		if (!IsOpen) Open();
		LoadCharacterData();
	}

	// ═════════════════════════════════════════════════════════════
	//  LOAD DATA
	// ═════════════════════════════════════════════════════════════

	private async void LoadCharacterData()
	{
		if (_loading || string.IsNullOrEmpty(_charId)) return;
		_loading = true;

		ClearRoot();
		_root.AddChild(PlaceholderText("Loading..."));

		var api = Networking.ApiClient.Instance;
		if (api == null) { ShowError("No API connection"); _loading = false; return; }

		var resp = await api.AdminGetCharacter(_charId);
		_loading = false;

		if (!resp.Success) { ShowError($"Error: {resp.Error}"); return; }

		try
		{
			using var doc = JsonDocument.Parse(resp.Body);
			var c = doc.RootElement.GetProperty("character");
			BuildDetail(c);
		}
		catch (Exception e)
		{
			ShowError($"Parse error: {e.Message}");
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  BUILD UI
	// ═════════════════════════════════════════════════════════════

	private void BuildDetail(JsonElement c)
	{
		ClearRoot();
		_statFields.Clear();

		_accountId = c.TryGetProperty("account_id", out var aid) ? aid.GetString() ?? "" : "";
		string name = c.TryGetProperty("name", out var n) ? n.GetString() : _charName;
		string race = c.TryGetProperty("race", out var r) ? r.GetString() : "?";
		string city = c.TryGetProperty("city", out var ci) ? ci.GetString() : "?";
		string rank = c.TryGetProperty("rp_rank", out var rk) ? rk.GetString() : "Aspirant";
		int level = c.TryGetProperty("character_level", out var lv) ? lv.GetInt32() : 1;
		int rpp = c.TryGetProperty("rpp", out var rp) ? rp.GetInt32() : 0;
		int tp = c.TryGetProperty("training_points_bank", out var tb) ? tb.GetInt32() : 0;

		// ── HEADER ──
		_headerName = new Label();
		_headerName.Text = name;
		_headerName.AddThemeFontSizeOverride("font_size", 16);
		_headerName.AddThemeColorOverride("font_color", TxBright);
		if (UITheme.FontTitleMedium != null) _headerName.AddThemeFontOverride("font", UITheme.FontTitleMedium);
		_root.AddChild(_headerName);

		_headerInfo = new Label();
		_headerInfo.Text = $"Lv.{level} {rank} · {race} · {city}";
		_headerInfo.AddThemeFontSizeOverride("font_size", 11);
		_headerInfo.AddThemeColorOverride("font_color", TxSec);
		if (UITheme.FontBody != null) _headerInfo.AddThemeFontOverride("font", UITheme.FontBody);
		_root.AddChild(_headerInfo);

		// ── FEEDBACK ──
		_feedbackLabel = new Label();
		_feedbackLabel.Text = "";
		_feedbackLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_feedbackLabel.AddThemeFontSizeOverride("font_size", 11);
		_root.AddChild(_feedbackLabel);

		_root.AddChild(ThinSeparator());

		// ── STATS ──
		SectionHeader("STATS");

		// Two columns: 3 stats each
		for (int row = 0; row < 3; row++)
		{
			var hbox = new HBoxContainer();
			hbox.AddThemeConstantOverride("separation", 12);
			_root.AddChild(hbox);

			for (int col = 0; col < 2; col++)
			{
				int idx = row + col * 3;
				string key = StatKeys[idx];
				string abbr = StatAbbr[idx];
				int val = c.TryGetProperty(key, out var sv) ? sv.GetInt32() : 0;

				var pair = new HBoxContainer();
				pair.AddThemeConstantOverride("separation", 4);
				pair.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				hbox.AddChild(pair);

				var lbl = SmallLabel($"{abbr}:", 11, TxDim);
				lbl.CustomMinimumSize = new Vector2(36, 0);
				pair.AddChild(lbl);

				var field = StyledLineEdit(val.ToString(), 50);
				_statFields[key] = field;
				pair.AddChild(field);
			}
		}

		_root.AddChild(ActionButton("Save Stats", async () =>
		{
			var stats = new Dictionary<string, int>();
			foreach (var (key, field) in _statFields)
			{
				if (int.TryParse(field.Text.Trim(), out int v))
					stats[key] = v;
			}
			if (stats.Count == 0) { SetFeedback("No valid stats.", ColRed); return; }
			var resp = await Networking.ApiClient.Instance.AdminSetStats(_charId, stats);
			HandleResponse(resp);
		}, BdAccent));

		_root.AddChild(ThinSeparator());

		// ── RPP ──
		SectionHeader("RPP");
		var rppRow = new HBoxContainer();
		rppRow.AddThemeConstantOverride("separation", 6);
		_root.AddChild(rppRow);

		rppRow.AddChild(SmallLabel($"Current: {rpp}", 11, TxSec));
		rppRow.AddChild(Spacer());

		_rppField = StyledLineEdit("0", 50);
		rppRow.AddChild(_rppField);

		var rppBtns = new HBoxContainer();
		rppBtns.AddThemeConstantOverride("separation", 4);
		_root.AddChild(rppBtns);

		rppBtns.AddChild(QuickBtn("+10", () => QuickRpp("grant", 10)));
		rppBtns.AddChild(QuickBtn("+50", () => QuickRpp("grant", 50)));
		rppBtns.AddChild(QuickBtn("+100", () => QuickRpp("grant", 100)));
		rppBtns.AddChild(Spacer());
		rppBtns.AddChild(ActionButton("Set", async () =>
		{
			if (!int.TryParse(_rppField.Text.Trim(), out int amt)) { SetFeedback("Invalid RPP.", ColRed); return; }
			var resp = await Networking.ApiClient.Instance.AdminSetRpp(_charId, "set", amt);
			HandleResponse(resp);
		}, BdAccent, compact: true));
		rppBtns.AddChild(ActionButton("Grant", async () =>
		{
			if (!int.TryParse(_rppField.Text.Trim(), out int amt)) { SetFeedback("Invalid RPP.", ColRed); return; }
			var resp = await Networking.ApiClient.Instance.AdminSetRpp(_charId, "grant", amt);
			HandleResponse(resp);
		}, ColGreen, compact: true));

		_root.AddChild(ThinSeparator());

		// ── TP ──
		SectionHeader("TRAINING POINTS");
		var tpRow = new HBoxContainer();
		tpRow.AddThemeConstantOverride("separation", 6);
		_root.AddChild(tpRow);

		tpRow.AddChild(SmallLabel($"Current: {tp}", 11, TxSec));
		tpRow.AddChild(Spacer());

		_tpField = StyledLineEdit("0", 50);
		tpRow.AddChild(_tpField);

		var tpBtns = new HBoxContainer();
		tpBtns.AddThemeConstantOverride("separation", 4);
		_root.AddChild(tpBtns);

		tpBtns.AddChild(QuickBtn("+10", () => QuickTp("grant", 10)));
		tpBtns.AddChild(QuickBtn("+50", () => QuickTp("grant", 50)));
		tpBtns.AddChild(QuickBtn("+100", () => QuickTp("grant", 100)));
		tpBtns.AddChild(Spacer());
		tpBtns.AddChild(ActionButton("Set", async () =>
		{
			if (!int.TryParse(_tpField.Text.Trim(), out int amt)) { SetFeedback("Invalid TP.", ColRed); return; }
			var resp = await Networking.ApiClient.Instance.AdminSetTp(_charId, "set", amt);
			HandleResponse(resp);
		}, BdAccent, compact: true));
		tpBtns.AddChild(ActionButton("Grant", async () =>
		{
			if (!int.TryParse(_tpField.Text.Trim(), out int amt)) { SetFeedback("Invalid TP.", ColRed); return; }
			var resp = await Networking.ApiClient.Instance.AdminSetTp(_charId, "grant", amt);
			HandleResponse(resp);
		}, ColGreen, compact: true));

		_root.AddChild(ThinSeparator());

		// ── RANK & LEVEL ──
		SectionHeader("RANK & LEVEL");
		var rlRow = new HBoxContainer();
		rlRow.AddThemeConstantOverride("separation", 8);
		_root.AddChild(rlRow);

		rlRow.AddChild(SmallLabel("Rank:", 11, TxDim));
		_rankDrop = new OptionButton();
		_rankDrop.CustomMinimumSize = new Vector2(110, 28);
		_rankDrop.AddThemeFontSizeOverride("font_size", 11);
		for (int i = 0; i < Ranks.Length; i++)
		{
			_rankDrop.AddItem(Ranks[i], i);
			if (Ranks[i] == rank) _rankDrop.Selected = i;
		}
		rlRow.AddChild(_rankDrop);

		rlRow.AddChild(Spacer());

		rlRow.AddChild(SmallLabel("Lv:", 11, TxDim));
		_levelField = StyledLineEdit(level.ToString(), 45);
		rlRow.AddChild(_levelField);

		_root.AddChild(ActionButton("Apply Rank & Level", async () =>
		{
			string newRank = Ranks[_rankDrop.Selected];
			int? newLevel = int.TryParse(_levelField.Text.Trim(), out int lv) ? lv : null;
			var resp = await Networking.ApiClient.Instance.AdminSetLevel(_charId, newLevel, newRank);
			HandleResponse(resp);
		}, BdAccent));

		_root.AddChild(ThinSeparator());

		// ── ACTIONS ──
		SectionHeader("ACTIONS");
		var actRow = new HBoxContainer();
		actRow.AddThemeConstantOverride("separation", 8);
		_root.AddChild(actRow);

		actRow.AddChild(ActionButton("Reset Training", async () =>
		{
			var resp = await Networking.ApiClient.Instance.AdminResetTraining(_charId);
			HandleResponse(resp);
		}, ColGold, compact: true));

		actRow.AddChild(ActionButton("Ban Account", () => ShowBanConfirm(), ColRed, compact: true));

		// ── REFRESH ──
		_root.AddChild(Spacer(8));
		var refreshRow = new HBoxContainer();
		refreshRow.AddThemeConstantOverride("separation", 8);
		_root.AddChild(refreshRow);
		refreshRow.AddChild(Spacer());
		refreshRow.AddChild(ActionButton("↻ Refresh", () => LoadCharacterData(), TxDim, compact: true));
	}

	// ═════════════════════════════════════════════════════════════
	//  QUICK ACTIONS
	// ═════════════════════════════════════════════════════════════

	private async void QuickRpp(string mode, int amount)
	{
		var resp = await Networking.ApiClient.Instance.AdminSetRpp(_charId, mode, amount);
		HandleResponse(resp);
	}

	private async void QuickTp(string mode, int amount)
	{
		var resp = await Networking.ApiClient.Instance.AdminSetTp(_charId, mode, amount);
		HandleResponse(resp);
	}

	// ═════════════════════════════════════════════════════════════
	//  BAN CONFIRMATION
	// ═════════════════════════════════════════════════════════════

	private void ShowBanConfirm()
	{
		if (string.IsNullOrEmpty(_accountId))
		{
			SetFeedback("No account ID available for this character.", ColRed);
			return;
		}

		// Create confirmation overlay
		var overlay = new ColorRect();
		overlay.Color = new Color(0, 0, 0, 0.5f);
		overlay.AnchorRight = 1;
		overlay.AnchorBottom = 1;
		overlay.MouseFilter = MouseFilterEnum.Stop;
		overlay.Name = "BanConfirmOverlay";

		var dialog = new PanelContainer();
		dialog.CustomMinimumSize = new Vector2(280, 0);
		dialog.AnchorLeft = 0.5f; dialog.AnchorRight = 0.5f;
		dialog.AnchorTop = 0.5f; dialog.AnchorBottom = 0.5f;
		dialog.OffsetLeft = -140; dialog.OffsetRight = 140;
		dialog.OffsetTop = -60; dialog.OffsetBottom = 60;

		var dStyle = new StyleBoxFlat();
		dStyle.BgColor = UITheme.BgPanel;
		dStyle.SetCornerRadiusAll(8);
		dStyle.SetBorderWidthAll(2);
		dStyle.BorderColor = ColRed;
		dStyle.ContentMarginLeft = 20;
		dStyle.ContentMarginRight = 20;
		dStyle.ContentMarginTop = 16;
		dStyle.ContentMarginBottom = 16;
		dialog.AddThemeStyleboxOverride("panel", dStyle);

		var vb = new VBoxContainer();
		vb.AddThemeConstantOverride("separation", 12);
		dialog.AddChild(vb);

		var msg = new Label();
		msg.Text = $"Ban the account owning \"{_charName}\"?\nThis will prevent login.";
		msg.AutowrapMode = TextServer.AutowrapMode.Word;
		msg.AddThemeFontSizeOverride("font_size", 13);
		msg.AddThemeColorOverride("font_color", TxBright);
		msg.HorizontalAlignment = HorizontalAlignment.Center;
		vb.AddChild(msg);

		var btns = new HBoxContainer();
		btns.AddThemeConstantOverride("separation", 12);
		btns.Alignment = BoxContainer.AlignmentMode.Center;
		vb.AddChild(btns);

		btns.AddChild(ActionButton("Cancel", () =>
		{
			overlay.QueueFree();
		}, TxDim, compact: true));

		btns.AddChild(ActionButton("Confirm Ban", async () =>
		{
			overlay.QueueFree();
			var resp = await Networking.ApiClient.Instance.AdminBan(_accountId, true);
			HandleResponse(resp);
		}, ColRed, compact: true));

		overlay.AddChild(dialog);

		// Add to the FloatingWindow's top-level parent so it covers content
		GetTree().CurrentScene.AddChild(overlay);
	}

	// ═════════════════════════════════════════════════════════════
	//  RESPONSE HANDLING
	// ═════════════════════════════════════════════════════════════

	private void HandleResponse(Networking.ApiResponse resp)
	{
		if (resp.Success)
		{
			try
			{
				using var doc = JsonDocument.Parse(resp.Body);
				if (doc.RootElement.TryGetProperty("message", out var msg))
					SetFeedback($"✓ {msg.GetString()}", ColGreen);
				else
					SetFeedback("✓ Success", ColGreen);

				// Sync local PlayerData if this is the active character
				if (doc.RootElement.TryGetProperty("character", out var c))
					SyncLocal(c);
			}
			catch { SetFeedback("✓ Done", ColGreen); }

			// Reload data to reflect changes
			LoadCharacterData();
		}
		else
		{
			SetFeedback($"✕ {resp.Error}", ColRed);
		}
	}

	private void SyncLocal(JsonElement c)
	{
		var gm = Core.GameManager.Instance;
		if (gm?.ActiveCharacter == null) return;
		if (c.TryGetProperty("id", out var id) && id.GetString() != gm.ActiveCharacterId) return;

		var p = gm.ActiveCharacter;
		if (c.TryGetProperty("strength", out var str)) p.Strength = str.GetInt32();
		if (c.TryGetProperty("vitality", out var vit)) p.Vitality = vit.GetInt32();
		if (c.TryGetProperty("agility", out var agi)) p.Agility = agi.GetInt32();
		if (c.TryGetProperty("dexterity", out var dex)) p.Dexterity = dex.GetInt32();
		if (c.TryGetProperty("mind", out var mnd)) p.Mind = mnd.GetInt32();
		if (c.TryGetProperty("ether_control", out var etc)) p.EtherControl = etc.GetInt32();
		if (c.TryGetProperty("training_points_bank", out var tp)) p.TrainingPointsBank = tp.GetInt32();
		if (c.TryGetProperty("current_hp", out var hp)) p.CurrentHp = hp.GetInt32();
		if (c.TryGetProperty("current_aether", out var ae)) p.CurrentAether = ae.GetInt32();
		if (c.TryGetProperty("rp_rank", out var rank)) p.RpRank = rank.GetString();
		if (c.TryGetProperty("rpp", out var rpp)) gm.ActiveLoadout.Rpp = rpp.GetInt32();
	}

	// ═════════════════════════════════════════════════════════════
	//  FEEDBACK
	// ═════════════════════════════════════════════════════════════

	private void SetFeedback(string text, Color color)
	{
		if (_feedbackLabel == null) return;
		_feedbackLabel.Text = text;
		_feedbackLabel.AddThemeColorOverride("font_color", color);
	}

	// ═════════════════════════════════════════════════════════════
	//  UI HELPERS
	// ═════════════════════════════════════════════════════════════

	private void ClearRoot()
	{
		if (_root == null) return;
		foreach (var c in _root.GetChildren()) c.QueueFree();
	}

	private void ShowError(string error)
	{
		ClearRoot();
		var lbl = new Label();
		lbl.Text = error;
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lbl.AddThemeColorOverride("font_color", ColRed);
		lbl.AddThemeFontSizeOverride("font_size", 12);
		_root.AddChild(lbl);
	}

	private void SectionHeader(string text)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeFontSizeOverride("font_size", 10);
		lbl.AddThemeColorOverride("font_color", ColGold);
		if (UITheme.FontBodySemiBold != null) lbl.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		_root.AddChild(lbl);
	}

	private static Label SmallLabel(string text, int size, Color color)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeFontSizeOverride("font_size", size);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.VerticalAlignment = VerticalAlignment.Center;
		if (UITheme.FontBody != null) lbl.AddThemeFontOverride("font", UITheme.FontBody);
		return lbl;
	}

	private static LineEdit StyledLineEdit(string text, float minWidth)
	{
		var edit = new LineEdit();
		edit.Text = text;
		edit.CustomMinimumSize = new Vector2(minWidth, 26);
		edit.AddThemeFontSizeOverride("font_size", 11);
		edit.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BgInput;
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 6;
		style.ContentMarginRight = 6;
		edit.AddThemeStyleboxOverride("normal", style);

		var focus = (StyleBoxFlat)style.Duplicate();
		focus.BorderColor = UITheme.Accent;
		edit.AddThemeStyleboxOverride("focus", focus);

		return edit;
	}

	private Button ActionButton(string text, Action callback, Color color, bool compact = false)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = compact ? new Vector2(0, 26) : new Vector2(0, 32);
		btn.AddThemeFontSizeOverride("font_size", compact ? 10 : 12);
		btn.AddThemeColorOverride("font_color", TxBright);
		if (!compact) btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		btn.FocusMode = FocusModeEnum.None;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(color, 0.25f);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(1);
		style.BorderColor = new Color(color, 0.5f);
		style.ContentMarginLeft = compact ? 8 : 12;
		style.ContentMarginRight = compact ? 8 : 12;
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = new Color(color, 0.4f);
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.Pressed += () => callback();
		return btn;
	}

	private static Control Spacer(float height = 0)
	{
		var s = new Control();
		s.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (height > 0) s.CustomMinimumSize = new Vector2(0, height);
		return s;
	}

	private Button QuickBtn(string text, Action callback)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(42, 24);
		btn.AddThemeFontSizeOverride("font_size", 10);
		btn.AddThemeColorOverride("font_color", TxBright);
		btn.FocusMode = FocusModeEnum.None;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(ColGreen, 0.2f);
		style.SetCornerRadiusAll(3);
		style.SetBorderWidthAll(1);
		style.BorderColor = new Color(ColGreen, 0.4f);
		style.ContentMarginLeft = 4;
		style.ContentMarginRight = 4;
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = new Color(ColGreen, 0.35f);
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.Pressed += () => callback();
		return btn;
	}

	// Suppress base class data-change rebuild — we manage our own data
	protected override void OnDataChanged() { }
}
