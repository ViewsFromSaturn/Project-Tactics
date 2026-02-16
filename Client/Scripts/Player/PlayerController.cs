using Godot;
using System;

namespace ProjectTactics.Player;

/// <summary>
/// Player controller for overworld movement.
/// Handles input, 8-directional movement, basic animation states,
/// and interaction triggers.
/// 
/// IMPORTANT: Checks ChatPanel.IsUiFocused to block movement when
/// the player is typing in chat, emote box, or settings fields.
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
		_sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite2D");
		GD.Print("[PlayerController] Ready.");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_canMove) return;

		// ═══ CRITICAL: Block movement when UI text fields are focused ═══
		if (UI.ChatPanel.IsUiFocused)
		{
			// Decelerate to stop while typing
			Velocity = Velocity.MoveToward(Vector2.Zero, (float)(Friction * delta));
			if (Velocity.Length() < 5f)
			{
				Velocity = Vector2.Zero;
				SetMovementState(MovementState.Idle);
			}
			MoveAndSlide();
			UpdateAnimation();
			return;
		}

		Vector2 inputDir = GetInputDirection();
		_isRunning = Input.IsActionPressed("run");

		float speed = _isRunning ? RunSpeed : WalkSpeed;

		if (inputDir != Vector2.Zero)
		{
			Velocity = Velocity.MoveToward(inputDir * speed, (float)(Acceleration * delta));
			UpdateFacingDirection(inputDir);
			SetMovementState(_isRunning ? MovementState.Running : MovementState.Walking);
		}
		else
		{
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
		if (UI.ChatPanel.IsUiFocused) return;

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
		FacingDirection = inputDir;
		EmitSignal(SignalName.DirectionChanged, inputDir);
	}

	private void SetMovementState(MovementState newState)
	{
		if (CurrentMovementState == newState) return;
		CurrentMovementState = newState;
		EmitSignal(SignalName.MovementStateChanged, (int)newState);
	}

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

		if (_sprite.SpriteFrames != null &&
			_sprite.SpriteFrames.HasAnimation(animName) &&
			_sprite.Animation != animName)
		{
			_sprite.Play(animName);
		}
	}

	private string GetDirectionName()
	{
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

	public void SetMovementEnabled(bool enabled)
	{
		_canMove = enabled;
		if (!enabled)
		{
			Velocity = Vector2.Zero;
			SetMovementState(MovementState.Idle);
		}
	}

	public void TeleportTo(Vector2 position)
	{
		GlobalPosition = position;
	}

	public Area2D GetInteractionArea()
	{
		return GetNodeOrNull<Area2D>("InteractionArea");
	}
}
