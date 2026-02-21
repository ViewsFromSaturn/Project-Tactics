using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ProjectTactics.Networking;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Admin Panel — server-authoritative commands for staff moderation.
/// Single scrollable layout with all commands. Hotkey: ` (backtick, admin-only).
/// All actions go through the Flask backend — nothing is client-side only.
/// </summary>
public partial class AdminPanel : WindowPanel
{
	// ═══ THEME (matches AbilityShopPanel palette) ═══
	static readonly Color BgCard      = new(0.235f, 0.255f, 0.314f, 0.10f);
	static readonly Color BgCardHover = new(0.235f, 0.255f, 0.314f, 0.20f);
	static readonly Color BdSubtle    = new(0.235f, 0.255f, 0.314f, 0.35f);
	static readonly Color BdAccent    = new("8B5CF6");
	static readonly Color TxBright    = new("EEEEE8");
	static readonly Color TxPrimary   = new("D4D2CC");
	static readonly Color TxDim       = new("64647A");
	static readonly Color ColGold     = new("D4A843");
	static readonly Color ColGreen    = new("5CB85C");
	static readonly Color ColRed      = new("CC4444");

	// ═══ STATE ═══
	struct CharEntry { public string Id; public string AccountId; public string Name; public string Rank; public int Level; }

	List<CharEntry> _characters = new();
	CharEntry? _selected = null;
	string _selectedId => _selected?.Id ?? "";
	

	VBoxContainer _root;
	Label _feedbackLabel;
	OptionButton _charDropdown;

	public AdminPanel()
	{
		PanelTitle = "⚙ ADMIN PANEL";
		DefaultWidth = 480;
		DefaultHeight = 640;
	}

	protected override void BuildContent(VBoxContainer content)
	{
		_root = content;
		content.AddThemeConstantOverride("separation", 6);
		content.AddChild(PlaceholderText("Loading characters..."));
	}

	public override void OnOpen()
	{
		base.OnOpen();
		LoadCharacters();
	}

	// ═══════════════════════════════════════════════════════════════
	//  LOAD ALL CHARACTERS
	// ═══════════════════════════════════════════════════════════════

	private async void LoadCharacters()
	{
		var api = ApiClient.Instance;
		if (api == null || !api.IsAdmin) return;

		var resp = await api.AdminListCharacters();
		if (!resp.Success)
		{
			RebuildWithError($"Failed to load characters: {resp.Error}");
			return;
		}

		_characters.Clear();
		using var doc = JsonDocument.Parse(resp.Body);
		foreach (var c in doc.RootElement.GetProperty("characters").EnumerateArray())
		{
			_characters.Add(new CharEntry
			{
				Id = c.GetProperty("id").GetString(),
				Name = c.GetProperty("name").GetString(),
				Rank = c.GetProperty("rp_rank").GetString(),
				Level = c.GetProperty("character_level").GetInt32(),
			});
		}

		// Auto-select first or current character
		var activeId = Core.GameManager.Instance?.ActiveCharacterId;
		_selected = _characters.FirstOrDefault(c => c.Id == activeId);
		if (_selected == null && _characters.Count > 0)
			_selected = _characters[0];

		RebuildAll();
	}

	// ═══════════════════════════════════════════════════════════════
	//  REBUILD UI
	// ═══════════════════════════════════════════════════════════════

	private void RebuildAll()
	{
		if (_root == null) return;
		foreach (var c in _root.GetChildren()) c.QueueFree();

		// ── FEEDBACK LABEL ──
		_feedbackLabel = new Label();
		_feedbackLabel.Text = "";
		_feedbackLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_feedbackLabel.AddThemeColorOverride("font_color", ColGreen);
		_feedbackLabel.AddThemeFontSizeOverride("font_size", 11);
		_root.AddChild(_feedbackLabel);

		// ── CHARACTER SELECTOR ──
		_root.AddChild(SectionLabel("TARGET CHARACTER"));
		_charDropdown = new OptionButton();
		_charDropdown.CustomMinimumSize = new Vector2(0, 32);
		_charDropdown.AddThemeFontSizeOverride("font_size", 12);
		for (int i = 0; i < _characters.Count; i++)
		{
			var ch = _characters[i];
			_charDropdown.AddItem($"{ch.Name}  (Lv.{ch.Level} {ch.Rank})", i);
			if (_selected.HasValue && ch.Id == _selected.Value.Id)
				_charDropdown.Selected = i;
		}
		_charDropdown.ItemSelected += idx =>
		{
			_selected = _characters[(int)idx];
			SetFeedback($"Selected: {_selected.Value.Name}", ColGold);
		};
		StyleOptionButton(_charDropdown);
		_root.AddChild(_charDropdown);

		_root.AddChild(ThinSep());

		// ── 1. RPP ──
		_root.AddChild(SectionLabel("1. RPP — ROLEPLAY POINTS"));
		var rppRow = InputRow("Amount:", "0");
		var rppMode = ModeSelector("set", "grant", "remove");
		_root.AddChild(rppRow.Container);
		_root.AddChild(rppMode.Container);
		_root.AddChild(ActionButton("Apply RPP", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			int amt = ParseInt(rppRow.Input);
			var resp = await ApiClient.Instance.AdminSetRpp(_selectedId, rppMode.Value(), amt);
			ShowResult(resp);
		}));

		_root.AddChild(ThinSep());

		// ── 2. TP ──
		_root.AddChild(SectionLabel("2. TP — TRAINING POINTS"));
		var tpRow = InputRow("Amount:", "0");
		var tpMode = ModeSelector("set", "grant", "remove");
		_root.AddChild(tpRow.Container);
		_root.AddChild(tpMode.Container);
		_root.AddChild(ActionButton("Apply TP", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			int amt = ParseInt(tpRow.Input);
			var resp = await ApiClient.Instance.AdminSetTp(_selectedId, tpMode.Value(), amt);
			ShowResult(resp);
		}));

		_root.AddChild(ThinSep());

		// ── 3. STATS ──
		_root.AddChild(SectionLabel("3. SET INDIVIDUAL STATS"));
		var strRow = InputRow("STR:", "");
		var vitRow = InputRow("VIT:", "");
		var dexRow = InputRow("DEX:", "");
		var agiRow = InputRow("AGI:", "");
		var etcRow = InputRow("ETC:", "");
		var mndRow = InputRow("MND:", "");
		_root.AddChild(strRow.Container);
		_root.AddChild(vitRow.Container);
		_root.AddChild(dexRow.Container);
		_root.AddChild(agiRow.Container);
		_root.AddChild(etcRow.Container);
		_root.AddChild(mndRow.Container);
		_root.AddChild(ActionButton("Set Stats", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			var stats = new Dictionary<string, int>();
			TryAddStat(stats, "strength", strRow.Input);
			TryAddStat(stats, "vitality", vitRow.Input);
			TryAddStat(stats, "dexterity", dexRow.Input);
			TryAddStat(stats, "agility", agiRow.Input);
			TryAddStat(stats, "ether_control", etcRow.Input);
			TryAddStat(stats, "mind", mndRow.Input);
			if (stats.Count == 0) { _busy = false; SetFeedback("Enter at least one stat value.", ColRed); return; }
			var resp = await ApiClient.Instance.AdminSetStats(_selectedId, stats);
			ShowResult(resp);
		}));

		_root.AddChild(ThinSep());

		// ── 4. LEVEL / RANK ──
		_root.AddChild(SectionLabel("4. SET LEVEL & RANK"));
		var lvlRow = InputRow("Level (1-100):", "");
		_root.AddChild(lvlRow.Container);

		var rankDrop = new OptionButton();
		rankDrop.CustomMinimumSize = new Vector2(0, 28);
		rankDrop.AddThemeFontSizeOverride("font_size", 11);
		foreach (var r in new[] { "", "Aspirant", "Sworn", "Warden", "Banneret", "Justicar" })
			rankDrop.AddItem(string.IsNullOrEmpty(r) ? "(don't change)" : r);
		StyleOptionButton(rankDrop);
		var rankRow = new HBoxContainer(); rankRow.AddThemeConstantOverride("separation", 8);
		rankRow.AddChild(DimLabel("Rank:"));
		rankRow.AddChild(rankDrop);
		_root.AddChild(rankRow);

		_root.AddChild(ActionButton("Set Level/Rank", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			int? level = null;
			string lvlText = lvlRow.Input.Text.Trim();
			if (!string.IsNullOrEmpty(lvlText) && int.TryParse(lvlText, out int lv)) level = lv;
			string rank = rankDrop.Selected > 0 ? rankDrop.GetItemText(rankDrop.Selected) : null;
			if (level == null && rank == null) { _busy = false; SetFeedback("Enter a level or select a rank.", ColRed); return; }
			var resp = await ApiClient.Instance.AdminSetLevel(_selectedId, level, rank);
			ShowResult(resp);
		}));

		_root.AddChild(ThinSep());

		// ── 5. GRANT STATS (additive) ──
		_root.AddChild(SectionLabel("5. GRANT BONUS STATS (ADDITIVE)"));
		var gStrRow = InputRow("STR+:", "");
		var gVitRow = InputRow("VIT+:", "");
		var gDexRow = InputRow("DEX+:", "");
		var gAgiRow = InputRow("AGI+:", "");
		var gEtcRow = InputRow("ETC+:", "");
		var gMndRow = InputRow("MND+:", "");
		_root.AddChild(gStrRow.Container);
		_root.AddChild(gVitRow.Container);
		_root.AddChild(gDexRow.Container);
		_root.AddChild(gAgiRow.Container);
		_root.AddChild(gEtcRow.Container);
		_root.AddChild(gMndRow.Container);
		_root.AddChild(ActionButton("Grant Stats", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			var grants = new Dictionary<string, int>();
			TryAddStat(grants, "strength", gStrRow.Input);
			TryAddStat(grants, "vitality", gVitRow.Input);
			TryAddStat(grants, "dexterity", gDexRow.Input);
			TryAddStat(grants, "agility", gAgiRow.Input);
			TryAddStat(grants, "ether_control", gEtcRow.Input);
			TryAddStat(grants, "mind", gMndRow.Input);
			if (grants.Count == 0) { _busy = false; SetFeedback("Enter at least one stat value.", ColRed); return; }
			var resp = await ApiClient.Instance.AdminGrantStats(_selectedId, grants);
			ShowResult(resp);
		}));

		_root.AddChild(ThinSep());

		// ── 6. BAN / UNBAN ──
		_root.AddChild(SectionLabel("6. BAN / UNBAN ACCOUNT"));
		var banRow = new HBoxContainer(); banRow.AddThemeConstantOverride("separation", 8);
		_root.AddChild(banRow);
		banRow.AddChild(ActionButton("Ban", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			// Need account_id — reload characters to get it
			var entry = _selected.Value;
			var charResp = await ApiClient.Instance.GetCharacter(entry.Id);
			if (!charResp.Success) { _busy = false; SetFeedback($"Error: {charResp.Error}", ColRed); return; }
			using var doc = JsonDocument.Parse(charResp.Body);
			var accId = doc.RootElement.GetProperty("character").GetProperty("account_id").GetString();
			var resp = await ApiClient.Instance.AdminBan(accId, true);
			ShowResult(resp);
		}, ColRed));
		banRow.AddChild(ActionButton("Unban", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			var entry = _selected.Value;
			var charResp = await ApiClient.Instance.GetCharacter(entry.Id);
			if (!charResp.Success) { _busy = false; SetFeedback($"Error: {charResp.Error}", ColRed); return; }
			using var doc = JsonDocument.Parse(charResp.Body);
			var accId = doc.RootElement.GetProperty("character").GetProperty("account_id").GetString();
			var resp = await ApiClient.Instance.AdminBan(accId, false);
			ShowResult(resp);
		}, ColGreen));

		_root.AddChild(ThinSep());

		// ── 7. RESET DAILY TRAINING ──
		_root.AddChild(SectionLabel("7. RESET DAILY TRAINING"));
		_root.AddChild(ActionButton("Reset Training Cooldowns", async () =>
		{
			if (!_selected.HasValue) { SetFeedback("Select a target character.", ColRed); return; }
			var resp = await ApiClient.Instance.AdminResetTraining(_selectedId);
			ShowResult(resp);
		}));

		_root.AddChild(ThinSep());

		// ── 8. ANNOUNCE ──
		_root.AddChild(SectionLabel("8. SERVER ANNOUNCEMENT"));
		var announceInput = new TextEdit();
		announceInput.CustomMinimumSize = new Vector2(0, 60);
		announceInput.PlaceholderText = "Type broadcast message...";
		announceInput.AddThemeFontSizeOverride("font_size", 12);
		announceInput.AddThemeColorOverride("font_color", TxPrimary);
		var aeStyle = new StyleBoxFlat();
		aeStyle.BgColor = new Color(0.08f, 0.08f, 0.12f);
		aeStyle.SetCornerRadiusAll(4);
		aeStyle.SetBorderWidthAll(1);
		aeStyle.BorderColor = BdSubtle;
		aeStyle.SetContentMarginAll(6);
		announceInput.AddThemeStyleboxOverride("normal", aeStyle);
		_root.AddChild(announceInput);
		_root.AddChild(ActionButton("Broadcast", async () =>
		{
			string msg = announceInput.Text.Trim();
			if (string.IsNullOrEmpty(msg)) { _busy = false; SetFeedback("Enter a message.", ColRed); return; }
			var resp = await ApiClient.Instance.AdminAnnounce(msg);
			ShowResult(resp);
			if (resp.Success) announceInput.Text = "";
		}));

		_root.AddChild(Spacer(12));
	}

	private void RebuildWithError(string error)
	{
		if (_root == null) return;
		foreach (var c in _root.GetChildren()) c.QueueFree();
		var lbl = new Label();
		lbl.Text = error;
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lbl.AddThemeColorOverride("font_color", ColRed);
		lbl.AddThemeFontSizeOverride("font_size", 12);
		_root.AddChild(lbl);
	}

	// ═══════════════════════════════════════════════════════════════
	//  FEEDBACK
	// ═══════════════════════════════════════════════════════════════

	private void SetFeedback(string text, Color color)
	{
		if (_feedbackLabel == null) return;
		_feedbackLabel.Text = text;
		_feedbackLabel.AddThemeColorOverride("font_color", color);
	}

	private void ShowResult(ApiResponse resp)
	{
		if (resp.Success)
		{
			try
			{
				using var doc = JsonDocument.Parse(resp.Body);
				if (doc.RootElement.TryGetProperty("message", out var msg))
				{
					SetFeedback($"✓ {msg.GetString()}", ColGreen);
					GD.Print($"[Admin] {msg.GetString()}");
				}
				else
				{
					SetFeedback("✓ Success", ColGreen);
				}

				if (doc.RootElement.TryGetProperty("character", out var c))
					SyncCharacterData(c);
			}
			catch (System.Exception e)
			{
				SetFeedback("✓ Success (parse warning)", ColGreen);
				GD.PrintErr($"[Admin] Parse error: {e.Message}");
			}
		}
		else
		{
			SetFeedback($"✕ {resp.Error}", ColRed);
			GD.PrintErr($"[Admin] Error: {resp.Error}");
		}
	}

	private void SyncCharacterData(JsonElement c)
	{
		var gm = Core.GameManager.Instance;
		if (gm?.ActiveCharacter == null) return;

		if (c.TryGetProperty("id", out var idProp) && idProp.GetString() != gm.ActiveCharacterId) return;

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

		GD.Print("[Admin] Local character data synced.");
	}

	// ═══════════════════════════════════════════════════════════════
	//  UI BUILDERS
	// ═══════════════════════════════════════════════════════════════

	struct InputRowResult { public HBoxContainer Container; public LineEdit Input; }
	struct ModeSelectorResult { public HBoxContainer Container; public OptionButton Dropdown; public string Value() => Dropdown.GetItemText(Dropdown.Selected); }

	private InputRowResult InputRow(string label, string defaultVal)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var lbl = DimLabel(label);
		lbl.CustomMinimumSize = new Vector2(100, 0);
		row.AddChild(lbl);

		var input = new LineEdit();
		input.Text = defaultVal;
		input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		input.CustomMinimumSize = new Vector2(0, 28);
		input.AddThemeFontSizeOverride("font_size", 12);
		input.AddThemeColorOverride("font_color", TxBright);
		var inputStyle = new StyleBoxFlat();
		inputStyle.BgColor = new Color(0.08f, 0.08f, 0.12f);
		inputStyle.SetCornerRadiusAll(4);
		inputStyle.SetBorderWidthAll(1);
		inputStyle.BorderColor = BdSubtle;
		inputStyle.ContentMarginLeft = 6; inputStyle.ContentMarginRight = 6;
		input.AddThemeStyleboxOverride("normal", inputStyle);
		var focusStyle = (StyleBoxFlat)inputStyle.Duplicate();
		focusStyle.BorderColor = BdAccent;
		input.AddThemeStyleboxOverride("focus", focusStyle);
		row.AddChild(input);

		return new InputRowResult { Container = row, Input = input };
	}

	private ModeSelectorResult ModeSelector(params string[] modes)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var lbl = DimLabel("Mode:");
		lbl.CustomMinimumSize = new Vector2(100, 0);
		row.AddChild(lbl);

		var drop = new OptionButton();
		drop.CustomMinimumSize = new Vector2(0, 28);
		drop.AddThemeFontSizeOverride("font_size", 11);
		foreach (var m in modes) drop.AddItem(m);
		StyleOptionButton(drop);
		row.AddChild(drop);

		return new ModeSelectorResult { Container = row, Dropdown = drop };
	}

	private Button ActionButton(string text, Action callback, Color? accentColor = null)
	{
		var color = accentColor ?? BdAccent;
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 32);
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", TxBright);

		var style = new StyleBoxFlat();
		style.BgColor = new Color(color, 0.25f);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(1);
		style.BorderColor = new Color(color, 0.5f);
		style.ContentMarginLeft = 12; style.ContentMarginRight = 12;
		btn.AddThemeStyleboxOverride("normal", style);

		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = new Color(color, 0.4f);
		btn.AddThemeStyleboxOverride("hover", hover);

		var pressed = (StyleBoxFlat)style.Duplicate();
		pressed.BgColor = new Color(color, 0.15f);
		btn.AddThemeStyleboxOverride("pressed", pressed);

		btn.Pressed += () => callback();
		return btn;
	}

	private static Label SectionLabel(string text)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeColorOverride("font_color", ColGold);
		lbl.AddThemeFontSizeOverride("font_size", 11);
		if (UITheme.FontBodySemiBold != null) lbl.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		return lbl;
	}

	private static Label DimLabel(string text)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeColorOverride("font_color", TxDim);
		lbl.AddThemeFontSizeOverride("font_size", 11);
		return lbl;
	}

	private static HSeparator ThinSep()
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("separator", BdSubtle);
		sep.AddThemeConstantOverride("separation", 8);
		return sep;
	}

	private void StyleOptionButton(OptionButton btn)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.08f, 0.08f, 0.12f);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(1);
		style.BorderColor = BdSubtle;
		style.ContentMarginLeft = 8; style.ContentMarginRight = 8;
		btn.AddThemeStyleboxOverride("normal", style);
		btn.AddThemeColorOverride("font_color", TxPrimary);
		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BorderColor = BdAccent;
		btn.AddThemeStyleboxOverride("hover", hover);
	}

	private static int ParseInt(LineEdit input)
	{
		return int.TryParse(input.Text.Trim(), out int val) ? val : 0;
	}

	private static void TryAddStat(Dictionary<string, int> dict, string key, LineEdit input)
	{
		string text = input.Text.Trim();
		if (!string.IsNullOrEmpty(text) && int.TryParse(text, out int val))
			dict[key] = val;
	}
}
