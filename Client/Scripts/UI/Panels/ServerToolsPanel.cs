using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Server Tools panel — admin hub with character search + server-wide commands.
/// Replaces the old monolithic AdminPanel. Opens with backtick.
/// </summary>
public partial class ServerToolsPanel : WindowPanel
{
	// ─── THEME ───
	static Color ColGold => new("D4A843");
	static Color ColGreen => new("66BB6A");
	static Color ColRed => new("CC4444");
	static Color TxBright => UITheme.TextBright;
	static Color TxDim => UITheme.TextDim;
	static Color TxSec => UITheme.TextSecondary;

	// ─── STATE ───
	struct CharEntry { public string Id; public string AccountId; public string Name; public string Rank; public int Level; }
	List<CharEntry> _allChars = new();
	bool _loaded = false;

	// ─── UI ───
	VBoxContainer _root;
	LineEdit _searchField;
	VBoxContainer _resultsBox;
	Label _feedbackLabel;
	LineEdit _announceField;

	// ─── REFERENCE ───
	AdminCharDetailPanel _detailPanel;
	public event Action<string, string> OpenDetailRequested;

	public ServerToolsPanel()
	{
		WindowTitle = "Server Tools";
		DefaultSize = new Vector2(360, 440);
		DefaultPosition = new Vector2(500, 60);
	}

	/// <summary>Set reference to the admin detail panel for opening character details.</summary>
	public void SetDetailPanel(AdminCharDetailPanel panel) => _detailPanel = panel;

	protected override void BuildContent(VBoxContainer content)
	{
		_root = content;
		content.AddThemeConstantOverride("separation", 6);
		content.AddChild(PlaceholderText("Loading characters..."));
	}

	public override void OnOpen()
	{
		base.OnOpen();
		if (!_loaded) LoadCharacters();
	}

	// ═════════════════════════════════════════════════════════════
	//  LOAD
	// ═════════════════════════════════════════════════════════════

	private async void LoadCharacters()
	{
		var api = Networking.ApiClient.Instance;
		if (api == null) { ShowError("No API."); return; }

		var resp = await api.AdminListCharacters();
		if (!resp.Success) { ShowError($"Error: {resp.Error}"); return; }

		_allChars.Clear();
		try
		{
			using var doc = JsonDocument.Parse(resp.Body);
			foreach (var c in doc.RootElement.GetProperty("characters").EnumerateArray())
			{
				_allChars.Add(new CharEntry
				{
					Id = c.GetProperty("id").GetString() ?? "",
					Name = c.GetProperty("name").GetString() ?? "?",
					Rank = c.TryGetProperty("rp_rank", out var rk) ? rk.GetString() ?? "Aspirant" : "Aspirant",
					Level = c.TryGetProperty("character_level", out var lv) ? lv.GetInt32() : 1,
				});
			}
		}
		catch (Exception e) { ShowError($"Parse: {e.Message}"); return; }

		_loaded = true;
		BuildUI();
	}

	// ═════════════════════════════════════════════════════════════
	//  BUILD
	// ═════════════════════════════════════════════════════════════

	private void BuildUI()
	{
		ClearRoot();

		// ── SEARCH ──
		SectionHeader("SEARCH CHARACTER");

		_searchField = new LineEdit();
		_searchField.PlaceholderText = "Type a name...";
		_searchField.CustomMinimumSize = new Vector2(0, 30);
		_searchField.AddThemeFontSizeOverride("font_size", 12);
		StyleInput(_searchField);
		_searchField.TextChanged += OnSearchChanged;
		_root.AddChild(_searchField);

		_resultsBox = new VBoxContainer();
		_resultsBox.AddThemeConstantOverride("separation", 2);
		_root.AddChild(_resultsBox);

		// Show all characters initially
		PopulateResults("");

		_root.AddChild(ThinSeparator());

		// ── FEEDBACK ──
		_feedbackLabel = new Label();
		_feedbackLabel.Text = "";
		_feedbackLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_feedbackLabel.AddThemeFontSizeOverride("font_size", 11);
		_root.AddChild(_feedbackLabel);

		// ── SERVER ANNOUNCE ──
		SectionHeader("SERVER ANNOUNCE");

		_announceField = new LineEdit();
		_announceField.PlaceholderText = "Broadcast message...";
		_announceField.CustomMinimumSize = new Vector2(0, 30);
		_announceField.AddThemeFontSizeOverride("font_size", 12);
		StyleInput(_announceField);
		_root.AddChild(_announceField);

		var announceBtn = MakeButton("Broadcast", ColGold);
		announceBtn.Pressed += OnAnnounce;
		_root.AddChild(announceBtn);

		_root.AddChild(ThinSeparator());

		// ── SERVER ACTIONS ──
		SectionHeader("SERVER ACTIONS");
		var refreshBtn = MakeButton("↻ Refresh Character List", TxDim);
		refreshBtn.Pressed += () => { _loaded = false; LoadCharacters(); };
		_root.AddChild(refreshBtn);
	}

	// ═════════════════════════════════════════════════════════════
	//  SEARCH
	// ═════════════════════════════════════════════════════════════

	private void OnSearchChanged(string query)
	{
		PopulateResults(query);
	}

	private void PopulateResults(string query)
	{
		if (_resultsBox == null) return;
		foreach (var c in _resultsBox.GetChildren()) c.QueueFree();

		string q = query.Trim().ToLower();
		int shown = 0;
		int max = 15;

		foreach (var ch in _allChars)
		{
			if (shown >= max) break;
			if (!string.IsNullOrEmpty(q) && !ch.Name.ToLower().Contains(q)) continue;

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);
			row.CustomMinimumSize = new Vector2(0, 28);
			_resultsBox.AddChild(row);

			var nameLbl = new Label();
			nameLbl.Text = $"{ch.Name}  (Lv.{ch.Level} {ch.Rank})";
			nameLbl.AddThemeFontSizeOverride("font_size", 11);
			nameLbl.AddThemeColorOverride("font_color", TxBright);
			if (UITheme.FontBody != null) nameLbl.AddThemeFontOverride("font", UITheme.FontBody);
			nameLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			nameLbl.VerticalAlignment = VerticalAlignment.Center;
			row.AddChild(nameLbl);

			string capturedId = ch.Id;
			string capturedName = ch.Name;
			var detailBtn = MakeButton("Details", UITheme.Accent, compact: true);
			detailBtn.Pressed += () => OpenDetail(capturedId, capturedName);
			row.AddChild(detailBtn);

			shown++;
		}

		if (shown == 0)
		{
			var empty = new Label();
			empty.Text = string.IsNullOrEmpty(q) ? "No characters found." : $"No match for \"{query}\".";
			empty.AddThemeFontSizeOverride("font_size", 11);
			empty.AddThemeColorOverride("font_color", TxDim);
			_resultsBox.AddChild(empty);
		}
	}

	private void OpenDetail(string charId, string charName)
	{
		if (OpenDetailRequested != null)
		{
			OpenDetailRequested.Invoke(charId, charName);
		}
		else if (_detailPanel != null)
		{
			_detailPanel.OpenForCharacter(charId, charName);
		}
		else
		{
			GD.PrintErr("[ServerTools] No detail panel or event handler.");
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  ANNOUNCE
	// ═════════════════════════════════════════════════════════════

	private async void OnAnnounce()
	{
		string msg = _announceField?.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(msg)) { SetFeedback("Enter a message.", ColRed); return; }

		var resp = await Networking.ApiClient.Instance.AdminAnnounce(msg);
		if (resp.Success)
		{
			SetFeedback("✓ Broadcast sent.", ColGreen);
			_announceField.Text = "";

			// Inject into chat as Announce message
			var chatPanel = GetTree().CurrentScene.FindChild("ChatPanel", true, false) as ChatPanel;
			chatPanel?.AddAnnouncement(msg);
		}
		else
		{
			SetFeedback($"✕ {resp.Error}", ColRed);
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  HELPERS
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
		lbl.AddThemeColorOverride("font_color", ColRed);
		lbl.AddThemeFontSizeOverride("font_size", 12);
		_root.AddChild(lbl);
	}

	private void SetFeedback(string text, Color color)
	{
		if (_feedbackLabel == null) return;
		_feedbackLabel.Text = text;
		_feedbackLabel.AddThemeColorOverride("font_color", color);
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

	private static void StyleInput(LineEdit edit)
	{
		var style = new StyleBoxFlat();
		style.BgColor = UITheme.BgInput;
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(1);
		style.BorderColor = UITheme.BorderSubtle;
		style.ContentMarginLeft = 8;
		style.ContentMarginRight = 8;
		edit.AddThemeStyleboxOverride("normal", style);

		var focus = (StyleBoxFlat)style.Duplicate();
		focus.BorderColor = UITheme.Accent;
		edit.AddThemeStyleboxOverride("focus", focus);
	}

	private static Button MakeButton(string text, Color color, bool compact = false)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = compact ? new Vector2(0, 24) : new Vector2(0, 30);
		btn.AddThemeFontSizeOverride("font_size", compact ? 10 : 12);
		btn.AddThemeColorOverride("font_color", UITheme.TextBright);
		btn.FocusMode = FocusModeEnum.None;
		if (!compact) btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

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

		return btn;
	}

	// Suppress base class data-change rebuild
	protected override void OnDataChanged() { }
}
