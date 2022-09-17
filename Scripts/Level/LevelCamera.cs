namespace Sankari;

public partial class LevelCamera : Camera2D
{
    private Player player;

    public override void _Ready()
    {
        player = GetNode<Player>("../Player");
		Current = true;
    }

    public override void _Process(double delta) => Position = player.Position;

	public void StopFollowingPlayer() => SetProcess(false);
    public void StartFollowingPlayer() => SetProcess(true);
}
