using Godot;

namespace NarutoRP.Player;

/// <summary>
/// Smooth follow camera for the overworld.
/// Attach this to a Camera2D node as a child of the scene root (NOT the player).
/// Assign the player node in the inspector or it auto-finds the player.
/// 
/// Features:
///   - Smooth lerp follow
///   - Zoom in/out
///   - Camera bounds (optional, set via TileMap limits)
///   - Shake effect (for combat hits, explosions)
/// </summary>
public partial class CameraController : Camera2D
{
	// ─── EXPORTS ────────────────────────────────────────────────
	[Export] public NodePath TargetPath { get; set; }
	[Export] public float FollowSpeed { get; set; } = 5.0f;
	[Export] public float DefaultZoom { get; set; } = 2.0f;
	[Export] public float MinZoom { get; set; } = 1.0f;
	[Export] public float MaxZoom { get; set; } = 4.0f;
	[Export] public float ZoomStep { get; set; } = 0.1f;
	[Export] public float ZoomSpeed { get; set; } = 8.0f;

	// ─── STATE ──────────────────────────────────────────────────
	private Node2D _target;
	private float _targetZoom;

	// Shake
	private float _shakeDuration = 0f;
	private float _shakeIntensity = 0f;
	private RandomNumberGenerator _rng = new();

	// ═════════════════════════════════════════════════════════════
	//  LIFECYCLE
	// ═════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		_targetZoom = DefaultZoom;
		Zoom = new Vector2(DefaultZoom, DefaultZoom);
		MakeCurrent();

		// Try to find target
		if (TargetPath != null && !TargetPath.IsEmpty)
		{
			_target = GetNode<Node2D>(TargetPath);
		}

		GD.Print("[CameraController] Ready.");
	}

	public override void _Process(double delta)
	{
		FollowTarget(delta);
		HandleZoom(delta);
		HandleShake(delta);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Mouse wheel zoom
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
				_targetZoom = Mathf.Clamp(_targetZoom + ZoomStep, MinZoom, MaxZoom);
			else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
				_targetZoom = Mathf.Clamp(_targetZoom - ZoomStep, MinZoom, MaxZoom);
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  FOLLOW
	// ═════════════════════════════════════════════════════════════

	private void FollowTarget(double delta)
	{
		if (_target == null) return;

		GlobalPosition = GlobalPosition.Lerp(
			_target.GlobalPosition,
			(float)(FollowSpeed * delta)
		);
	}

	// ═════════════════════════════════════════════════════════════
	//  ZOOM
	// ═════════════════════════════════════════════════════════════

	private void HandleZoom(double delta)
	{
		Zoom = Zoom.Lerp(
			new Vector2(_targetZoom, _targetZoom),
			(float)(ZoomSpeed * delta)
		);
	}

	// ═════════════════════════════════════════════════════════════
	//  SCREEN SHAKE
	// ═════════════════════════════════════════════════════════════

	private void HandleShake(double delta)
	{
		if (_shakeDuration <= 0) return;

		_shakeDuration -= (float)delta;

		Offset = new Vector2(
			_rng.RandfRange(-_shakeIntensity, _shakeIntensity),
			_rng.RandfRange(-_shakeIntensity, _shakeIntensity)
		);

		if (_shakeDuration <= 0)
		{
			Offset = Vector2.Zero;
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  PUBLIC API
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Set the camera follow target at runtime.
	/// </summary>
	public void SetTarget(Node2D target)
	{
		_target = target;
	}

	/// <summary>
	/// Trigger a screen shake (for hits, explosions, dramatic moments).
	/// </summary>
	public void Shake(float intensity = 4.0f, float duration = 0.3f)
	{
		_shakeIntensity = intensity;
		_shakeDuration = duration;
	}

	/// <summary>
	/// Snap camera immediately to target (no lerp).
	/// Useful for scene transitions.
	/// </summary>
	public void SnapToTarget()
	{
		if (_target != null)
			GlobalPosition = _target.GlobalPosition;
	}

	/// <summary>
	/// Set zoom level directly.
	/// </summary>
	public void SetZoom(float zoom)
	{
		_targetZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
	}
}
