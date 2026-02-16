using Godot;
using System;

namespace ProjectTactics.UI;

/// <summary>
/// HSL Color Wheel picker — draws a circular hue/saturation wheel with
/// a brightness slider. Matches the HUD v4 mockup's canvas-based wheel.
/// </summary>
public partial class ColorWheelPicker : VBoxContainer
{
	[Signal] public delegate void ColorChangedEventHandler(Color color);

	// ═══ STATE ═══
	private float _hue = 0f;        // 0-360
	private float _saturation = 0f; // 0-100
	private float _brightness = 82f; // 0-100 (lightness)
	private Color _currentColor = new("d4d2cc");
	private bool _wheelDragging = false;

	// ═══ UI REFS ═══
	private TextureRect _wheelRect;
	private ColorRect _cursor;
	private HSlider _brightnessSlider;
	private Label _brightnessLabel;
	private ColorRect _previewSwatch;
	private LineEdit _hexInput;
	private Label _previewName;

	// Wheel geometry
	private const int WheelSize = 160;
	private const int WheelRadius = 78;
	private const int CursorSize = 12;

	public Color CurrentColor => _currentColor;

	public override void _Ready()
	{
		AddThemeConstantOverride("separation", 8);
		BuildWheel();
		BuildBrightnessRow();
		BuildPreviewRow();

		// Apply any values set before _Ready via SetColor()
		_brightnessSlider.SetValueNoSignal(_brightness);
		DrawWheel();
		UpdateCursorPosition();
		UpdatePreview();
	}

	/// <summary>Set the color externally (e.g. from saved settings).</summary>
	public void SetColor(Color color)
	{
		var (h, s, l) = RgbToHsl(color.R, color.G, color.B);
		_hue = h;
		_saturation = s;
		_brightness = l;
		_currentColor = color;

		// UI may not exist yet if called before _Ready
		if (_brightnessSlider == null) return;

		_brightnessSlider.SetValueNoSignal(_brightness);
		DrawWheel();
		UpdateCursorPosition();
		UpdatePreview();
	}

	// ═════════════════════════════════════════════════════════
	//  BUILD
	// ═════════════════════════════════════════════════════════

	private void BuildWheel()
	{
		// Container for wheel + cursor overlay
		var wheelContainer = new Control();
		wheelContainer.CustomMinimumSize = new Vector2(WheelSize, WheelSize);
		wheelContainer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		AddChild(wheelContainer);

		// Wheel image
		_wheelRect = new TextureRect();
		_wheelRect.CustomMinimumSize = new Vector2(WheelSize, WheelSize);
		_wheelRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_wheelRect.MouseFilter = MouseFilterEnum.Stop;
		_wheelRect.GuiInput += OnWheelInput;
		wheelContainer.AddChild(_wheelRect);

		// Cursor dot
		_cursor = new ColorRect();
		_cursor.CustomMinimumSize = new Vector2(CursorSize, CursorSize);
		_cursor.Size = new Vector2(CursorSize, CursorSize);
		_cursor.Color = _currentColor;
		_cursor.MouseFilter = MouseFilterEnum.Ignore;
		wheelContainer.AddChild(_cursor);
	}

	private void BuildBrightnessRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		AddChild(row);

		_brightnessLabel = new Label();
		_brightnessLabel.Text = "BRIGHT";
		_brightnessLabel.CustomMinimumSize = new Vector2(50, 0);
		_brightnessLabel.AddThemeFontSizeOverride("font_size", 9);
		_brightnessLabel.AddThemeColorOverride("font_color", UITheme.TextDim);
		if (UITheme.FontNumbersMedium != null) _brightnessLabel.AddThemeFontOverride("font", UITheme.FontNumbersMedium);
		row.AddChild(_brightnessLabel);

		_brightnessSlider = new HSlider();
		_brightnessSlider.MinValue = 20;
		_brightnessSlider.MaxValue = 95;
		_brightnessSlider.Value = _brightness;
		_brightnessSlider.Step = 1;
		_brightnessSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_brightnessSlider.CustomMinimumSize = new Vector2(0, 14);

		// Style the slider
		var sliderBg = new StyleBoxFlat();
		sliderBg.BgColor = new Color(0.92f, 0.92f, 0.93f, 1f);
		sliderBg.SetCornerRadiusAll(3);
		sliderBg.ContentMarginTop = 4;
		sliderBg.ContentMarginBottom = 4;
		_brightnessSlider.AddThemeStyleboxOverride("slider", sliderBg);

		var grabber = new StyleBoxFlat();
		grabber.BgColor = Colors.White;
		grabber.SetCornerRadiusAll(7);
		grabber.ContentMarginLeft = 6;
		grabber.ContentMarginRight = 6;
		grabber.ContentMarginTop = 6;
		grabber.ContentMarginBottom = 6;
		grabber.BorderColor = UITheme.BorderMedium;
		grabber.SetBorderWidthAll(2);
		_brightnessSlider.AddThemeStyleboxOverride("grabber_area", grabber);

		_brightnessSlider.ValueChanged += OnBrightnessChanged;
		row.AddChild(_brightnessSlider);
	}

	private void BuildPreviewRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		AddChild(row);

		_previewSwatch = new ColorRect();
		_previewSwatch.CustomMinimumSize = new Vector2(28, 28);
		_previewSwatch.Color = _currentColor;
		row.AddChild(_previewSwatch);

		_hexInput = new LineEdit();
		_hexInput.Text = "#d4d2cc";
		_hexInput.MaxLength = 7;
		_hexInput.CustomMinimumSize = new Vector2(80, 0);
		_hexInput.AddThemeFontSizeOverride("font_size", 12);
		_hexInput.AddThemeColorOverride("font_color", UITheme.TextBright);
		_hexInput.AddThemeColorOverride("caret_color", UITheme.Accent);
		if (UITheme.FontNumbersMedium != null) _hexInput.AddThemeFontOverride("font", UITheme.FontNumbersMedium);

		var hexStyle = new StyleBoxFlat();
		hexStyle.BgColor = UITheme.BgInput;
		hexStyle.SetCornerRadiusAll(4);
		hexStyle.ContentMarginLeft = 8;
		hexStyle.ContentMarginRight = 8;
		hexStyle.ContentMarginTop = 4;
		hexStyle.ContentMarginBottom = 4;
		hexStyle.BorderColor = UITheme.BorderSubtle;
		hexStyle.SetBorderWidthAll(1);
		_hexInput.AddThemeStyleboxOverride("normal", hexStyle);
		var hexFocus = (StyleBoxFlat)hexStyle.Duplicate();
		hexFocus.BorderColor = UITheme.BorderFocus;  // Violet
		_hexInput.AddThemeStyleboxOverride("focus", hexFocus);

		_hexInput.TextSubmitted += OnHexSubmitted;
		row.AddChild(_hexInput);

		_previewName = new Label();
		_previewName.Text = _pendingName;
		_previewName.AddThemeFontSizeOverride("font_size", 12);
		_previewName.AddThemeColorOverride("font_color", _currentColor);
		if (UITheme.FontBodyMedium != null) _previewName.AddThemeFontOverride("font", UITheme.FontBodyMedium);
		row.AddChild(_previewName);
	}

	// ═════════════════════════════════════════════════════════
	//  DRAW WHEEL
	// ═════════════════════════════════════════════════════════

	private void DrawWheel()
	{
		var image = Image.CreateEmpty(WheelSize, WheelSize, false, Image.Format.Rgba8);
		int cx = WheelSize / 2, cy = WheelSize / 2;

		for (int y = 0; y < WheelSize; y++)
		{
			for (int x = 0; x < WheelSize; x++)
			{
				float dx = x - cx, dy = y - cy;
				float dist = MathF.Sqrt(dx * dx + dy * dy);

				if (dist <= WheelRadius)
				{
					float angle = MathF.Atan2(dy, dx);
					float hue = ((angle * 180f / MathF.PI) + 360f) % 360f;
					float sat = (dist / WheelRadius) * 100f;
					var (r, g, b) = HslToRgb(hue, sat, _brightness);
					image.SetPixel(x, y, new Color(r / 255f, g / 255f, b / 255f, 1f));
				}
				else if (dist <= WheelRadius + 1.5f)
				{
					// Anti-alias edge
					float alpha = Math.Max(0f, 1f - (dist - WheelRadius) / 1.5f);
					float angle = MathF.Atan2(dy, dx);
					float hue = ((angle * 180f / MathF.PI) + 360f) % 360f;
					var (r, g, b) = HslToRgb(hue, 100f, _brightness);
					image.SetPixel(x, y, new Color(r / 255f, g / 255f, b / 255f, alpha));
				}
				// else: transparent (default)
			}
		}

		var tex = ImageTexture.CreateFromImage(image);
		_wheelRect.Texture = tex;
	}

	// ═════════════════════════════════════════════════════════
	//  INPUT
	// ═════════════════════════════════════════════════════════

	private void OnWheelInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_wheelDragging = true;
				PickFromWheel(mb.Position);
			}
			else
			{
				_wheelDragging = false;
			}
		}
		else if (ev is InputEventMouseMotion mm && _wheelDragging)
		{
			PickFromWheel(mm.Position);
		}
	}

	private void PickFromWheel(Vector2 pos)
	{
		int cx = WheelSize / 2, cy = WheelSize / 2;
		float dx = pos.X - cx, dy = pos.Y - cy;
		float dist = MathF.Min(MathF.Sqrt(dx * dx + dy * dy), WheelRadius);
		float angle = MathF.Atan2(dy, dx);

		_hue = ((angle * 180f / MathF.PI) + 360f) % 360f;
		_saturation = (dist / WheelRadius) * 100f;

		UpdateColorFromHsl();
		UpdateCursorPosition();
	}

	private void OnBrightnessChanged(double value)
	{
		_brightness = (float)value;
		DrawWheel();
		UpdateColorFromHsl();
	}

	private void OnHexSubmitted(string hex)
	{
		hex = hex.Trim();
		if (!hex.StartsWith('#')) hex = "#" + hex;
		if (hex.Length == 7 && Color.HtmlIsValid(hex))
		{
			var c = new Color(hex);
			var (h, s, l) = RgbToHsl(c.R, c.G, c.B);
			_hue = h;
			_saturation = s;
			_brightness = l;
			_brightnessSlider.Value = _brightness;
			DrawWheel();
			UpdateCursorPosition();
			_currentColor = c;
			UpdatePreview();
			EmitSignal(SignalName.ColorChanged, _currentColor);
		}
		_hexInput.ReleaseFocus();
	}

	// ═════════════════════════════════════════════════════════
	//  UPDATE
	// ═════════════════════════════════════════════════════════

	private void UpdateColorFromHsl()
	{
		var (r, g, b) = HslToRgb(_hue, _saturation, _brightness);
		_currentColor = new Color(r / 255f, g / 255f, b / 255f, 1f);
		UpdatePreview();
		EmitSignal(SignalName.ColorChanged, _currentColor);
	}

	private void UpdatePreview()
	{
		_previewSwatch.Color = _currentColor;
		_hexInput.Text = "#" + _currentColor.ToHtml(false);
		_previewName.AddThemeColorOverride("font_color", _currentColor);
		_cursor.Color = _currentColor;
	}

	private void UpdateCursorPosition()
	{
		int cx = WheelSize / 2, cy = WheelSize / 2;
		float rad = _hue * MathF.PI / 180f;
		float dist = (_saturation / 100f) * WheelRadius;
		float x = cx + MathF.Cos(rad) * dist - CursorSize / 2f;
		float y = cy + MathF.Sin(rad) * dist - CursorSize / 2f;
		_cursor.Position = new Vector2(x, y);
	}

	/// <summary>Update the preview name label text.</summary>
	public void SetPreviewName(string name)
	{
		_pendingName = name;
		if (_previewName != null) _previewName.Text = name;
	}

	private string _pendingName = "Alaric";

	// ═════════════════════════════════════════════════════════
	//  COLOR MATH
	// ═════════════════════════════════════════════════════════

	private static (float r, float g, float b) HslToRgb(float h, float s, float l)
	{
		s /= 100f; l /= 100f;
		float c = (1f - MathF.Abs(2f * l - 1f)) * s;
		float x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
		float m = l - c / 2f;
		float r, g, b;

		if (h < 60) { r = c; g = x; b = 0; }
		else if (h < 120) { r = x; g = c; b = 0; }
		else if (h < 180) { r = 0; g = c; b = x; }
		else if (h < 240) { r = 0; g = x; b = c; }
		else if (h < 300) { r = x; g = 0; b = c; }
		else { r = c; g = 0; b = x; }

		return (MathF.Round((r + m) * 255f), MathF.Round((g + m) * 255f), MathF.Round((b + m) * 255f));
	}

	private static (float h, float s, float l) RgbToHsl(float r, float g, float b)
	{
		float max = MathF.Max(r, MathF.Max(g, b));
		float min = MathF.Min(r, MathF.Min(g, b));
		float l = (max + min) / 2f;
		float h = 0, s = 0;

		if (max != min)
		{
			float d = max - min;
			s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

			if (max == r) h = ((g - b) / d + (g < b ? 6f : 0f)) * 60f;
			else if (max == g) h = ((b - r) / d + 2f) * 60f;
			else h = ((r - g) / d + 4f) * 60f;
		}

		return (h, s * 100f, l * 100f);
	}
}
