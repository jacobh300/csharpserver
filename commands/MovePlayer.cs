using SpacetimeDB;

public partial class MovePlayer : ReducerCommand
{

    private DbVector2 _direction;

    [SpacetimeDB.Reducer]
    public static void reducer_move(ReducerContext ctx, DbVector2 direction)
    {
        MovePlayer commandInstance = new MovePlayer(ctx, direction);
        commandInstance.run();
    }

    [SpacetimeDB.Reducer]
    public static void reducer_defaultMove(ReducerContext ctx)
    {
        MovePlayer commandInstance = new MovePlayer(ctx, new DbVector2(1, 0));
        commandInstance.run();
    }

    public MovePlayer(ReducerContext ctx, DbVector2 direction) : base(ctx)
    {
        //_direction = direction;
        _direction  = DbVector2.normalize(direction);  
        // Normalize direction to -1, 0, or 1
        //_direction.x = _direction.x > 0 ? 1 : (_direction.x < 0 ? -1 : 0);
        //_direction.y = _direction.y > 0 ? 1 : (_direction.y < 0 ? -1 : 0);
    }

    protected override void run()
    {
        // Set the players velocity based on the input direction
        Module.PlayerTransformRow? transform = _ctx.Db.player_transform.player.Find(_user.Id);
        if (transform == null)
        {
            respond("Player transform not found.");
            return;
        }

        // Set direction to either 1 or 0


        transform.velocity = new DbVector3
        {
            x = _direction.x * 0.02f, // Move speed multiplier
            y = _direction.y * 0.02f,
            z = 0.0f
        };

        _ctx.Db.player_transform.player.Update(transform);
    }

}