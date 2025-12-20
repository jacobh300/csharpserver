using SpacetimeDB;

public partial class UpdateSpeed : ReducerCommand
{

    private float _speed;

    [SpacetimeDB.Reducer]
    public static void reducer_updateSpeed(ReducerContext ctx, float speed)
    {
        UpdateSpeed commandInstance = new UpdateSpeed(ctx, speed);
        commandInstance.run();
    }

    public UpdateSpeed(ReducerContext ctx, float speed) : base(ctx)
    {
        _speed = speed;
    }

    //Use the Input table instead to store player input for better synchronization
    protected override void run()
    {
        //Set all players speed
        foreach(var player in _ctx.Db.player_transform.Iter())
        {
            player.moveSpeed = _speed;
            _ctx.Db.player_transform.player.Update(player);
        }
    }

}