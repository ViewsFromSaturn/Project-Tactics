using Godot;

namespace ProjectTactics.UI.Panels;

/// <summary>
/// Chronicle Keeper — AI lore oracle interface.
/// Shows a star icon, lore-flavored intro, scrollable chat area, and input field.
/// Matches Image 3: centered star, "The void stirs." intro, text input at bottom.
/// Hotkey: K
/// </summary>
public partial class ChronicleKeeperPanel : WindowPanel
{
	private RichTextLabel _chatLog;
	private LineEdit _inputField;

	public ChronicleKeeperPanel()
	{
		WindowTitle = "Chronicle Keeper";
		DefaultSize = new Vector2(380, 520);
	}

	protected override void BuildContent(VBoxContainer content)
	{
		// Chat log area (scrollable)
		_chatLog = new RichTextLabel();
		_chatLog.BbcodeEnabled = true;
		_chatLog.FitContent = false;
		_chatLog.ScrollFollowing = true;
		_chatLog.SizeFlagsVertical = SizeFlags.ExpandFill;
		_chatLog.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_chatLog.CustomMinimumSize = new Vector2(0, 300);
		_chatLog.AddThemeColorOverride("default_color", UITheme.Text);
		_chatLog.AddThemeFontSizeOverride("normal_font_size", 13);
		if (UITheme.FontBody != null) _chatLog.AddThemeFontOverride("normal_font", UITheme.FontBody);

		// Transparent background
		var logStyle = new StyleBoxFlat();
		logStyle.BgColor = Colors.Transparent;
		_chatLog.AddThemeStyleboxOverride("normal", logStyle);

		content.AddChild(_chatLog);

		// Show the intro
		ShowIntro();

		// ─── Input row at bottom ───
		var inputRow = new HBoxContainer();
		inputRow.AddThemeConstantOverride("separation", 6);
		content.AddChild(inputRow);

		_inputField = new LineEdit();
		_inputField.PlaceholderText = "Ask the Chronicle Keeper...";
		_inputField.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_inputField.CustomMinimumSize = new Vector2(0, 38);
		_inputField.AddThemeFontSizeOverride("font_size", 13);
		_inputField.AddThemeColorOverride("font_color", UITheme.Text);
		_inputField.AddThemeColorOverride("font_placeholder_color", UITheme.TextDim);
		if (UITheme.FontBody != null) _inputField.AddThemeFontOverride("font", UITheme.FontBody);

		var inputStyle = new StyleBoxFlat();
		inputStyle.BgColor = UITheme.BgInput;
		inputStyle.SetCornerRadiusAll(8);
		inputStyle.SetBorderWidthAll(1);
		inputStyle.BorderColor = UITheme.BorderSubtle;
		inputStyle.ContentMarginLeft = 12;
		inputStyle.ContentMarginRight = 12;
		_inputField.AddThemeStyleboxOverride("normal", inputStyle);

		var inputFocus = (StyleBoxFlat)inputStyle.Duplicate();
		inputFocus.BorderColor = UITheme.BorderFocus;
		_inputField.AddThemeStyleboxOverride("focus", inputFocus);

		_inputField.TextSubmitted += OnSubmit;
		inputRow.AddChild(_inputField);

		// Send button
		var sendBtn = new Button();
		sendBtn.Text = "▶";
		sendBtn.CustomMinimumSize = new Vector2(38, 38);
		sendBtn.AddThemeFontSizeOverride("font_size", 16);
		sendBtn.AddThemeColorOverride("font_color", UITheme.TextDim);
		sendBtn.AddThemeColorOverride("font_hover_color", UITheme.TextBright);

		var sendStyle = new StyleBoxFlat();
		sendStyle.BgColor = UITheme.CardBg;
		sendStyle.SetCornerRadiusAll(8);
		sendBtn.AddThemeStyleboxOverride("normal", sendStyle);

		var sendHover = (StyleBoxFlat)sendStyle.Duplicate();
		sendHover.BgColor = UITheme.CardHoverBg;
		sendBtn.AddThemeStyleboxOverride("hover", sendHover);

		sendBtn.Pressed += () => OnSubmit(_inputField.Text);
		inputRow.AddChild(sendBtn);
	}

	private void ShowIntro()
	{
		_chatLog.Clear();

		// Centered star icon + intro text
		string starColor = UITheme.TextDim.ToHtml(false);
		string accentColor = UITheme.AccentViolet.ToHtml(false);
		string dimColor = UITheme.TextSecondary.ToHtml(false);

		_chatLog.AppendText("\n\n\n\n");
		_chatLog.AppendText($"[center][font_size=36][color=#{starColor}]✦[/color][/font_size][/center]\n\n");
		_chatLog.AppendText($"[center][color=#{accentColor}]●[/color] [b]The void stirs.[/b][/center]\n");
		_chatLog.AppendText($"[center][color=#{dimColor}]A new Chronicle begins. Ask the Keeper[/color][/center]\n");
		_chatLog.AppendText($"[center][color=#{dimColor}]anything about this dying world.[/color][/center]\n");
	}

	private void OnSubmit(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		_inputField.Text = "";

		// User message
		string userColor = UITheme.TextBright.ToHtml(false);
		_chatLog.AppendText($"\n[color=#{userColor}][b]You:[/b] {text}[/color]\n");

		// Placeholder keeper response — future: hook to AI backend
		string keeperColor = UITheme.AccentViolet.ToHtml(false);
		string dimColor = UITheme.TextSecondary.ToHtml(false);
		_chatLog.AppendText($"\n[color=#{keeperColor}]●[/color] [color=#{dimColor}][i]The Keeper contemplates your question...[/i][/color]\n");

		_inputField.GrabFocus();
	}

	public override void OnOpen()
	{
		_inputField?.GrabFocus();
	}
}
