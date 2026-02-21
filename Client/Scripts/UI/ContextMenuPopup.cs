using Godot;
using System;
using System.Collections.Generic;

namespace ProjectTactics.UI;

/// <summary>
/// Right-click context menu for players. Shows on player sprites in overworld
/// and on character names in chat. Admin users see extra moderation options.
/// </summary>
public partial class ContextMenuPopup : PanelContainer
{
	// ─── THEME ───
	static Color MenuBg => UITheme.BgPanel;
	static Color MenuBorder => UITheme.BorderMedium;
	static Color TextNormal => UITheme.Text;
	static Color TextBright => UITheme.TextBright;
	static Color TextDim => UITheme.TextDim;
	static Color TextAdmin => new("D4A843");
	static Color AccentViolet => UITheme.Accent;
	static Color AccentRed => new("CC4444");
	static Color SepColor => UITheme.BorderSubtle;
	static Color HoverBg => UITheme.BgPanelHover;

	// ─── STATE ───
	string _targetCharId = "";
	string _targetCharName = "";
	string _targetAccountId = "";
	bool _isAdmin = false;
	bool _isOpen = false;

	VBoxContainer _items;

	// ─── EVENTS ───
	public event Action<string, string> ViewProfileRequested;     // charId, name
	public event Action<string, string> SendMessageRequested;     // charId, name
	public event Action<string, string> AdminDetailsRequested;    // charId, name
	public event Action<string, string> BanRequested;             // charId, name

	public override void _Ready()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = new Vector2(200, 0);
		ZIndex = 200;

		var style = new StyleBoxFlat();
		style.BgColor = MenuBg;
		style.SetCornerRadiusAll(6);
		style.BorderColor = MenuBorder;
		style.SetBorderWidthAll(1);
		style.ContentMarginLeft = 4;
		style.ContentMarginRight = 4;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 6;
		style.ShadowColor = new Color(0, 0, 0, 0.3f);
		style.ShadowSize = 4;
		AddThemeStyleboxOverride("panel", style);

		_items = new VBoxContainer();
		_items.AddThemeConstantOverride("separation", 0);
		AddChild(_items);
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!_isOpen) return;

		// Dismiss on any click outside
		if (ev is InputEventMouseButton mb && mb.Pressed)
		{
			if (!GetGlobalRect().HasPoint(mb.GlobalPosition))
			{
				Close();
				GetViewport().SetInputAsHandled();
			}
		}
		else if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  PUBLIC API
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Show context menu for a character at the given screen position.
	/// </summary>
	public void Show(string characterId, string characterName, Vector2 screenPos,
		string accountId = "", bool isAdmin = false)
	{
		_targetCharId = characterId;
		_targetCharName = characterName;
		_targetAccountId = accountId;
		_isAdmin = isAdmin;

		RebuildItems();
		PositionAtCursor(screenPos);

		Visible = true;
		_isOpen = true;
	}

	public void Close()
	{
		Visible = false;
		_isOpen = false;
	}

	// ═════════════════════════════════════════════════════════════
	//  BUILD MENU ITEMS
	// ═════════════════════════════════════════════════════════════

	private void RebuildItems()
	{
		foreach (var c in _items.GetChildren()) c.QueueFree();

		// Header — character name
		var header = new Label();
		header.Text = $"  {_targetCharName}";
		header.AddThemeFontSizeOverride("font_size", 12);
		header.AddThemeColorOverride("font_color", TextBright);
		if (UITheme.FontBodySemiBold != null) header.AddThemeFontOverride("font", UITheme.FontBodySemiBold);
		header.CustomMinimumSize = new Vector2(0, 28);
		header.VerticalAlignment = VerticalAlignment.Center;
		_items.AddChild(header);

		_items.AddChild(MakeSep());

		// ── PLAYER OPTIONS ──
		AddItem("View Profile", "👤", TextNormal, () =>
		{
			ViewProfileRequested?.Invoke(_targetCharId, _targetCharName);
			Close();
		});

		AddItem("Send Message", "💬", TextNormal, () =>
		{
			SendMessageRequested?.Invoke(_targetCharId, _targetCharName);
			Close();
		});

		AddItem("Invite to Party", "👥", TextDim, null, disabled: true); // future

		// ── ADMIN OPTIONS ──
		if (_isAdmin)
		{
			_items.AddChild(MakeSep());

			var adminLabel = new Label();
			adminLabel.Text = "  ADMIN";
			adminLabel.AddThemeFontSizeOverride("font_size", 9);
			adminLabel.AddThemeColorOverride("font_color", TextAdmin);
			adminLabel.CustomMinimumSize = new Vector2(0, 20);
			adminLabel.VerticalAlignment = VerticalAlignment.Center;
			_items.AddChild(adminLabel);

			AddItem("Open Admin Details", "⚙", TextAdmin, () =>
			{
				AdminDetailsRequested?.Invoke(_targetCharId, _targetCharName);
				Close();
			});

			AddItem("Set Rank", "📋", TextAdmin, () =>
			{
				// Open rank submenu or go straight to admin details
				AdminDetailsRequested?.Invoke(_targetCharId, _targetCharName);
				Close();
			});

			AddItem("Ban Account", "⛔", AccentRed, () =>
			{
				BanRequested?.Invoke(_targetCharId, _targetCharName);
				Close();
			});
		}
	}

	private void AddItem(string text, string icon, Color color, Action callback, bool disabled = false)
	{
		var btn = new Button();
		btn.Text = $"  {icon}  {text}";
		btn.Alignment = HorizontalAlignment.Left;
		btn.CustomMinimumSize = new Vector2(0, 30);
		btn.AddThemeFontSizeOverride("font_size", 12);
		btn.AddThemeColorOverride("font_color", disabled ? TextDim : color);
		btn.AddThemeColorOverride("font_hover_color", TextBright);
		btn.FocusMode = FocusModeEnum.None;

		if (UITheme.FontBody != null) btn.AddThemeFontOverride("font", UITheme.FontBody);

		var normal = new StyleBoxFlat();
		normal.BgColor = Colors.Transparent;
		normal.ContentMarginLeft = 4;
		normal.ContentMarginRight = 4;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = new StyleBoxFlat();
		hover.BgColor = HoverBg;
		hover.SetCornerRadiusAll(4);
		hover.ContentMarginLeft = 4;
		hover.ContentMarginRight = 4;
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.AddThemeStyleboxOverride("pressed", normal);

		if (!disabled && callback != null)
			btn.Pressed += () => callback();
		else
			btn.Disabled = true;

		_items.AddChild(btn);
	}

	// ═════════════════════════════════════════════════════════════
	//  HELPERS
	// ═════════════════════════════════════════════════════════════

	private void PositionAtCursor(Vector2 screenPos)
	{
		var viewport = GetViewportRect().Size;
		float w = CustomMinimumSize.X;
		float h = 250f; // estimate

		float x = Mathf.Clamp(screenPos.X, 8, viewport.X - w - 8);
		float y = Mathf.Clamp(screenPos.Y, 8, viewport.Y - h - 8);

		Position = new Vector2(x, y);
	}

	private static PanelContainer MakeSep()
	{
		var sep = new PanelContainer();
		sep.CustomMinimumSize = new Vector2(0, 1);
		var style = new StyleBoxFlat();
		style.BgColor = SepColor;
		style.ContentMarginTop = 3;
		style.ContentMarginBottom = 3;
		sep.AddThemeStyleboxOverride("panel", style);
		return sep;
	}
}
