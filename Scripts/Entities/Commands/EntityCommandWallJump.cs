﻿namespace Sankari;

public interface IEntityWallJumpable : IEntityMoveable
{
	// Left wall checks
	public List<RayCast2D> RayCast2DWallChecksLeft { get; }

	// Right wall checks
	public List<RayCast2D> RayCast2DWallChecksRight { get; }

	// Is entity within wall jump-able area
	public bool InWallJumpArea { get; }

	// Wall direction
	public int WallDir { get; set; }

	// Is the entity falling?
	public bool IsFalling();

	// Horizontal wall jump force
	public int JumpForceWallHorz { get; set; }

	// Vertical wall jump force
	public int JumpForceWallVert { get; set; }

	// Max speed due to gravity
	public int ModGravityMaxSpeed { get; set; }
}

public class EntityCommandWallJump : EntityCommand<IEntityWallJumpable>
{
	public int MaxGravitySpeedSliding { get; set; } = 20; 
	public int MaxGravitySpeedSlidingFast { get; set; } = 220; 
	private int PreviousWallOnJump { get; set; }

	private bool wasSliding = false;
	private float previousXDir = 0;

	public EntityCommandWallJump(IEntityWallJumpable entity) : base(entity) { }

	public override void Start()
	{
		if (Entity.InWallJumpArea)
		{
			// If the entity is on a wall, prevent entity from wall jumping on the same wall twice
			if (Entity.WallDir != 0)
			{
				// wall jump
				GameManager.EventsPlayer.Notify(EventPlayer.OnJump);

				Entity.AnimatedSprite.FlipH = Entity.WallDir == 1; // flip sprite on wall jump

				var velocity = Entity.Velocity;
				velocity.x += -Entity.JumpForceWallHorz * Entity.WallDir;
				velocity.y = -Entity.JumpForceWallVert;
				if (PreviousWallOnJump == Entity.WallDir)
				{
					velocity.y /= 2;
				}
				Entity.Velocity = velocity;

				PreviousWallOnJump = Entity.WallDir;
			}
		}
		else
			Entity.AnimatedSprite.FlipH = false;
	}

	public override void Update(float delta)
	{
		Entity.WallDir = UpdateWallDirection();
		if (Entity.IsOnGround())
		{
			PreviousWallOnJump = 0;
			wasSliding = false;
			return;
		}

		var isSliding = IsSliding();

		if (isSliding)
		{
			var velocity = Entity.Velocity;
			// Snap to nearest wall if we weren't sliding
			if (!wasSliding)
			{
				var collider = GetCollidingWall();

				if (collider != default)
				{
					Entity.GlobalPosition = new Vector2(collider.GetCollisionPoint().x, Entity.GlobalPosition.y);
				}
			}

			if (Entity.IsFalling())
			{
				Entity.ModGravityMaxSpeed = MaxGravitySpeedSliding;

				// fast fall
				if (Entity is Player player)
					if (player.PlayerInput.IsDown)
						Entity.ModGravityMaxSpeed = MaxGravitySpeedSlidingFast;

				// Slow down the player. (Faking Friction)
				if (velocity.y != Entity.ModGravityMaxSpeed)
					velocity = velocity.MoveToward(new Vector2(velocity.x, Entity.ModGravityMaxSpeed), 2000 * delta);
			}
			Entity.Velocity = velocity;
		}

		wasSliding = isSliding;
	}

	/// <summary>
	/// Gets the raycast which is colliding
	/// </summary>
	private RayCast2D GetCollidingWall()
	{
		if (Entity.WallDir == Vector2.Left.x)
			return CollectionExtensions.GetAnyRayCastCollider(Entity.RayCast2DWallChecksLeft);

		else if (Entity.WallDir == Vector2.Right.x)
			return CollectionExtensions.GetAnyRayCastCollider(Entity.RayCast2DWallChecksRight);

		return default;
	}

	/// <summary>
	/// Checks if the Entity should be sliding
	/// </summary>
	private bool IsSliding()
	{
		var velocityDir = MovementUtils.GetDirection(Entity.Velocity);

		// MoveDir takes priority
		if (Entity.MoveDir.x != 0)
			previousXDir = Entity.MoveDir.x;
		else if (velocityDir.x != 0)
			previousXDir = velocityDir.x;

		var isSliding = wasSliding;

		if (Entity.InWallJumpArea && Entity.WallDir != 0)
		{
			if (previousXDir == Entity.WallDir)
				isSliding = true;
			else if (previousXDir != 0)
				isSliding = false;
		}
		else
			isSliding = false;

		return isSliding;
	}

	private int UpdateWallDirection()
	{
		var left = CollectionExtensions.IsAnyRayCastColliding(Entity.RayCast2DWallChecksLeft);
		var right = CollectionExtensions.IsAnyRayCastColliding(Entity.RayCast2DWallChecksRight);

		return -Convert.ToInt32(left) + Convert.ToInt32(right);
	}
}
