using SpacetimeDB;

public partial class MovePlayer : ReducerCommand
{

    private DbVector2 _direction;
    private UInt32 _sequence;

    [SpacetimeDB.Reducer]
    public static void reducer_move(ReducerContext ctx, DbVector2 direction, UInt32 sequence)
    {
        MovePlayer commandInstance = new MovePlayer(ctx, direction, sequence);
        commandInstance.run();
    }

    [SpacetimeDB.Reducer]
    public static void reducer_defaultMove(ReducerContext ctx)
    {
        MovePlayer commandInstance = new MovePlayer(ctx, new DbVector2(1, 0), 0);
        commandInstance.run();
    }

    public MovePlayer(ReducerContext ctx, DbVector2 direction, UInt32 sequence) : base(ctx)
    {
        //_direction = direction;
        _direction  = DbVector2.normalize(direction);  
        _sequence = sequence;
    }

    //protected override void run()
    //{
    //    // Set the players velocity based on the input direction
    //    Module.PlayerTransformRow? transform = _ctx.Db.player_transform.player.Find(_user.Id);
    //    if (transform == null)
    //    {
    //        respond("Player transform not found.");
    //        return;
    //    }
    //
    //    // Set direction to either 1 or 0
    //    transform.sequence = _sequence;
    //
    //    transform.velocity = new DbVector3
    //    {
    //        x = _direction.x,
    //        y = 0,
    //        z = _direction.y
    //    };
    //
    //    _ctx.Db.player_transform.player.Update(transform);
    //}

    //Use the Input table instead to store player input for better synchronization
    protected override void run()
    {
        Module.PlayerInputRow? inputRow = _ctx.Db.player_input.player.Find(_user.Id);
        if (inputRow == null)
        {
            respond("Player input row not found.");
            return;
        }

        // Set direction to either 1 or 0
        inputRow.sequence = _sequence;
        inputRow.input = _direction;
        inputRow.last_position = _ctx.Db.player_transform.player.Find(_user.Id)?.position ?? new DbVector3(0,0,0);
        _ctx.Db.player_input.player.Update(inputRow);
    }

}