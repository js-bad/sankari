namespace Sankari;

public interface IPlayerAnimations : IEntityAnimationDash, IEntityAnimation
{ }

public interface IPlayerCommands : IEntityDash, IEntityWallJumpable, IEntityGroundJumpable, IEntityMovement
{ }

public partial class Player : Entity, IPlayerAnimations, IPlayerCommands
{
	// Static
	public static Vector2 RespawnPosition { get; set; }

	public static bool HasTouchedCheckpoint { get; set; }

	// IEntityJumpable
	public bool InWallJumpArea { get; set; }

	// IEntityDash
	public bool CurrentlyDashing { get; set; }

	// IEntityAnimation
	public AnimatedSprite2D AnimatedSprite { get; set; }

	// Not in a interface
	public GTimer        TimerNetSend                       { get; set; }
	public LevelScene    LevelScene                         { get; set; }
	public Vector2       PrevNetPos                         { get; set; }
	public MovementInput PlayerInput                        { get; set; }
	public int           HorizontalDeadZone                 { get; set; } = 25;
	public GTween        DieTween                           { get; set; }
	public GTimer        DontCheckPlatformAfterDashDuration { get; set; }
	public GTimer        PreventMovementTimer               { get; set; }

	private bool preventMovement = false;
	public void PreInit(LevelScene levelScene) => LevelScene = levelScene;

	public override void Init()
	{
		Commands[EntityCommandType.Dash]          = new EntityCommandDash(this);
		Commands[EntityCommandType.WallJump]      = new EntityCommandWallJump(this);
		Commands[EntityCommandType.GroundJump]    = new EntityCommandGroundJump(this);

		Animations[EntityAnimationType.Idle]      = new EntityAnimationIdle(this);
		Animations[EntityAnimationType.Walking]   = new EntityAnimationWalking(this);
		Animations[EntityAnimationType.Running]   = new EntityAnimationRunning(this);
		Animations[EntityAnimationType.JumpStart] = new EntityAnimationJumpStart(this);
		Animations[EntityAnimationType.JumpFall]  = new EntityAnimationJumpFall(this);
		Animations[EntityAnimationType.Dash]      = new EntityAnimationDash(this);

		CurrentAnimation = EntityAnimationType.Idle;

		if (GameManager.PlayerManager.ActiveCheckpoint)
			Position = GameManager.PlayerManager.RespawnPosition;

		TimerNetSend   = new GTimer(this, nameof(NetUpdate), NetIntervals.HEARTBEAT, true, Net.IsMultiplayer());
		AnimatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		DieTween       = new GTween(this);
		Timers         = new GTimers(this);

		// dont go under platform at the end of a dash for X ms
		GetCommandClass<EntityCommandDash>(EntityCommandType.Dash).DashDurationDone += OnDashDone;
		GetCommandClass<EntityCommandWallJump>(EntityCommandType.WallJump).WallJump += OnWallJump;
		DontCheckPlatformAfterDashDuration = new GTimer(this, 500, false, false);
		PreventMovementTimer = new GTimer(this, new Callable(PreventMovementFinished), 50, false, false);
	}

	public override void UpdatePhysics()
	{
		PlayerInput = MovementUtils.GetPlayerMovementInput(); // PlayerInput = ... needs to go before base._PhysicsProcess(delta)

		UpdateMoveDirection(PlayerInput);

		if (!CurrentlyDashing && !DontCheckPlatformAfterDashDuration.IsActive())
			UpdateUnderPlatform(PlayerInput);

		// jump is handled before all movement restrictions
		if (PlayerInput.IsJump)
		{
			if (!IsNearGround()) // Wall jump
			{
				Commands[EntityCommandType.WallJump].Start();
			}
			else
			{
				Commands[EntityCommandType.GroundJump].Start();
			}
		}

		if (PlayerInput.IsDash)
			Commands[EntityCommandType.Dash].Start();

		Velocity = new Vector2(MoveDeadZone(Velocity.x, HorizontalDeadZone), Velocity.y); // must be after ClampAndDampen(...)
	}

	public override void UpdateGround()
	{
		if (PlayerInput.IsSprint)
		{
			MaxSpeed = MaxSpeedSprint;
			Commands.Values.ForEach(cmd => cmd.UpdateGroundSprinting(Delta));
		}
		else
		{
			MaxSpeed = MaxSpeedWalk;
			Commands.Values.ForEach(cmd => cmd.UpdateGroundWalking(Delta));
		}
	}

	public override void UpdateAir()
	{
		if (PlayerInput.IsFastFall)
			Velocity = Velocity + new Vector2(0, 10);
	}

	/// <summary>
	/// Called when a Dash Command finishes as Dash
	/// </summary>
	public void OnDashDone(object _, EventArgs _2) => DontCheckPlatformAfterDashDuration.Start();

	public void OnWallJump(object _, EventArgs _2)
	{
		GameManager.EventsPlayer.Notify(EventPlayer.OnJump);

		// Lock movement
		preventMovement = true;
		PreventMovementTimer.Start(); 
	}

	public void PreventMovementFinished()
	{
		preventMovement = false;
	}

	private float MoveDeadZone(float horzVelocity, int deadzone)
	{
		if (MoveDir.x == 0 && horzVelocity >= -deadzone && horzVelocity <= deadzone)
			return horzVelocity * 0.5f;

		return horzVelocity;
	}

	private void NetUpdate()
	{
		if (Position != PrevNetPos)
			Net.Client.Send(ClientPacketOpcode.PlayerPosition, new CPacketPlayerPosition
			{
				Position = Position
			});

		PrevNetPos = Position;
	}

	private async void UpdateUnderPlatform(MovementInput input)
	{
		var collision = RaycastsGround[0].GetCollider();

		if (collision is TileMap tilemap)
		{
			if (input.IsDown && tilemap.IsInGroup("Platform"))
			{
				// Player is in layer 1
				// Enemies are in layer 2

				tilemap.EnableLayers(2); // this disables layer 1 (the layer the player is in)
				await Task.Delay(1000);
				tilemap.EnableLayers(1, 2); // re-enable layers 1 and 2

				// This works but isn't the best. For example for multiplayer, if all "OtherPlayer"s are
				// in layer 1, one player disabling the layer will disable the platform for all players.
				// Also what if we want to move this implementation to enemies? If a enemy disables the
				// layer 2, all other enemies on that platform will fall as well!
			}
		}
	}

	private void UpdateMoveDirection(MovementInput input)
	{
		if (!preventMovement)
		{
			var x = -Convert.ToInt32(input.IsLeft) + Convert.ToInt32(input.IsRight);
			var y = -Convert.ToInt32(input.IsUp) + Convert.ToInt32(input.IsDown);
			MoveDir = new Vector2(x, y);
		}
		else
		{
			MoveDir = MovementUtils.GetDirection(Velocity);
		}

	}

	public override void Kill() => new PlayerCommandDeath(this).Start();

	public async Task FinishedLevel()
	{
		HaltLogic = true;
		await LevelManager.CompleteLevel(LevelManager.CurrentLevel);
		HaltLogic = false;
	}

	private void _on_Player_Area_area_entered(Area2D area)
	{
		if (HaltLogic)
			return;

		if (area.IsInGroup("WallJumpArea"))
			InWallJumpArea = true;
	}

	private void _on_Area_area_exited(Area2D area)
	{
		if (area.IsInGroup("WallJumpArea"))
			InWallJumpArea = false;
	}
}
