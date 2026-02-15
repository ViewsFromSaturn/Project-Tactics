using Godot;
using System;

namespace ProjectTactics.Player;

/// <summary>
/// Player controller for overworld movement.
/// Handles input, 8-directional movement, basic animation states,
/// and interaction triggers.
/// 
/// Scene structure (CharacterBody2D):
///   PlayerController (this script)
///   ├── Sprite2D (or AnimatedSprite2D)
///   ├── CollisionShape2D
///   ├── Camera2D (optional, or separate CameraController)
///   └── InteractionArea (Area2D)
///       └── CollisionShape2D
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	// ─── EXPORTS ────────────────────────────────────────────────
	[Export] public float WalkSpeed { get; set; } = 120.0f;
	[Export] public float RunSpeed { get; set; } = 200.0f;
	[Export] public float Acceleration { get; set; } = 800.0f;
	[Export] public float Friction { get; set; } = 1000.0f;

	// ─── SIGNALS ────────────────────────────────────────────────
	[Signal] public delegate void DirectionChangedEventHandler(Vector2 direction);
	[Signal] public delegate void MovementStateChangedEventHandler(MovementState state);
	[Signal] public delegate void InteractionTriggeredEventHandler();

	// ─── STATE ──────────────────────────────────────────────────
	public enum MovementState { Idle, Walking, Running }
	public MovementState CurrentMovementState { get; private set; } = MovementState.Idle;
	public Vector2 FacingDirection { get; private set; } = Vector2.Down;

	private bool _canMove = true;
	private bool _isRunning = false;

	// ─── NODE REFERENCES ────────────────────────────────────────
	private AnimatedSprite2D _sprite;

	// ═════════════════════════════════════════════════════════════
	//  LIFECYCLE
	// ═════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		// Try to get animated sprite (optional — works without it)
		_sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite2D");

		GD.Print("[PlayerController] Ready.");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_canMove) return;

		Vector2 inputDir = GetInputDirection();
		_isRunning = Input.IsActionPressed("run"); // Shift key by default

		float speed = _isRunning ? RunSpeed : WalkSpeed;

		if (inputDir != Vector2.Zero)
		{
			// Accelerate toward input direction
			Velocity = Velocity.MoveToward(inputDir * speed, (float)(Acceleration * delta));
			UpdateFacingDirection(inputDir);
			SetMovementState(_isRunning ? MovementState.Running : MovementState.Walking);
		}
		else
		{
			// Decelerate to stop
			Velocity = Velocity.MoveToward(Vector2.Zero, (float)(Friction * delta));
			if (Velocity.Length() < 5f)
			{
				Velocity = Vector2.Zero;
				SetMovementState(MovementState.Idle);
			}
		}

		MoveAndSlide();
		UpdateAnimation();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Interaction (E key or Enter)
		if (@event.IsActionPressed("interact"))
		{
			EmitSignal(SignalName.InteractionTriggered);
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  INPUT
	// ═════════════════════════════════════════════════════════════

	private Vector2 GetInputDirection()
	{
		Vector2 dir = Vector2.Zero;

		if (Input.IsActionPressed("move_up"))    dir.Y -= 1;
		if (Input.IsActionPressed("move_down"))  dir.Y += 1;
		if (Input.IsActionPressed("move_left"))  dir.X -= 1;
		if (Input.IsActionPressed("move_right")) dir.X += 1;

		return dir.Normalized();
	}

	// ═════════════════════════════════════════════════════════════
	//  FACING / ANIMATION
	// ═════════════════════════════════════════════════════════════

	private void UpdateFacingDirection(Vector2 inputDir)
	{
		if (inputDir == Vector2.Zero) return;

		// Snap to 4 or 8 directions for sprite animation
		FacingDirection = inputDir;
		EmitSignal(SignalName.DirectionChanged, inputDir);
	}

	private void SetMovementState(MovementState newState)
	{
		if (CurrentMovementState == newState) return;
		CurrentMovementState = newState;
		EmitSignal(SignalName.MovementStateChanged, (int)newState);
	}

	/// <summary>
	/// Update sprite animation based on movement state and facing direction.
	/// Uses naming convention: "idle_down", "walk_right", "run_up", etc.
	/// </summary>
	private void UpdateAnimation()
	{
		if (_sprite == null) return;

		string directionName = GetDirectionName();
		string stateName = CurrentMovementState switch
		{
			MovementState.Idle    => "idle",
			MovementState.Walking => "walk",
			MovementState.Running => "run",
			_ => "idle"
		};

		string animName = $"{stateName}_{directionName}";

		// Only change animation if it exists and is different
		if (_sprite.SpriteFrames != null &&
			_sprite.SpriteFrames.HasAnimation(animName) &&
			_sprite.Animation != animName)
		{
			_sprite.Play(animName);
		}
	}

	/// <summary>
	/// Convert facing direction vector to animation name suffix.
	/// Supports 4-directional: up, down, left, right.
	/// </summary>
	private string GetDirectionName()
	{
		// Use dominant axis for 4-directional sprites
		if (Mathf.Abs(FacingDirection.X) > Mathf.Abs(FacingDirection.Y))
		{
			return FacingDirection.X > 0 ? "right" : "left";
		}
		else
		{
			return FacingDirection.Y > 0 ? "down" : "up";
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  PUBLIC CONTROL
	// ═════════════════════════════════════════════════════════════

	/// <summary>
	/// Enable or disable player movement (for menus, cutscenes, combat).
	/// </summary>
	public void SetMovementEnabled(bool enabled)
	{
		_canMove = enabled;
		if (!enabled)
		{
			Velocity = Vector2.Zero;
			SetMovementState(MovementState.Idle);
		}
	}

	/// <summary>
	/// Teleport player to a position.
	/// </summary>
	public void TeleportTo(Vector2 position)
	{
		GlobalPosition = position;
	}

	/// <summary>
	/// Get the interaction area (for detecting NPCs, doors, etc).
	/// </summary>
	public Area2D GetInteractionArea()
	{
		return GetNodeOrNull<Area2D>("InteractionArea");
	}
}
