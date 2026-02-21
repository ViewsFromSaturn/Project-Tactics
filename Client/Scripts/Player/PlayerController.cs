using Godot;
using System;

namespace ProjectTactics.Player;

/// <summary>
/// Player controller for overworld movement.
/// Uses Sprite2D with hframes/vframes spritesheet.
/// Sheet layout: 5 cols × 4 rows
///   Row 0 = Front (down), Row 1 = Right, Row 2 = Left, Row 3 = Back (up)
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

	// ─── SPRITE ANIMATION ───────────────────────────────────────
	private Sprite2D _sprite;
	private int _cols = 5;
	private int _currentRow = 0;     // 0=front, 1=right, 2=left, 3=back
	private int _currentCol = 0;
	private float _animTimer = 0f;
	private float _animSpeed = 0.15f; // seconds per frame

	private int GetRow(string dir) => dir switch
	{
		"down"  => 0,
		"right" => 1,
		"left"  => 2,
		"up"    => 3,
		_ => 0
	};

	// ═════════════════════════════════════════════════════════════
	//  LIFECYCLE
	// ═════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite != null)
		{
			_cols = _sprite.Hframes;
			_sprite.Frame = 0;
		}
		GD.Print("[PlayerController] Ready.");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_canMove) return;

		if (UI.ChatPanel.IsUiFocused || UI.OverworldHUD.IsAnyTextFieldFocused)
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, (float)(Friction * delta));
			if (Velocity.Length() < 5f)
			{
				Velocity = Vector2.Zero;
				SetMovementState(MovementState.Idle);
			}
			MoveAndSlide();
			UpdateAnimation((float)delta);
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
		UpdateAnimation((float)delta);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (UI.ChatPanel.IsUiFocused || UI.OverworldHUD.IsAnyTextFieldFocused) return;

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

	private void UpdateAnimation(float delta)
	{
		if (_sprite == null) return;

		string dirName = GetDirectionName();
		_currentRow = GetRow(dirName);

		if (CurrentMovementState == MovementState.Idle)
		{
			_currentCol = 0;
			_animTimer = 0f;
		}
		else
		{
			float spd = CurrentMovementState == MovementState.Running ? _animSpeed * 0.6f : _animSpeed;
			_animTimer += delta;
			if (_animTimer >= spd)
			{
				_animTimer -= spd;
				_currentCol = (_currentCol + 1) % _cols;
			}
		}

		_sprite.Frame = _currentRow * _cols + _currentCol;
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
