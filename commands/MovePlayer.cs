using SpacetimeDB;

public partial class MovePlayer : ReducerCommand
{

    private DbVector2 _direction;
    private UInt32 _sequence;
    private float _yaw;

    [SpacetimeDB.Reducer]
    public static void reducer_move(ReducerContext ctx, DbVector2 direction, float yaw, UInt32 sequence)
    {
        MovePlayer commandInstance = new MovePlayer(ctx, direction, yaw, sequence);
        commandInstance.run();
    }

    [SpacetimeDB.Reducer]
    public static void reducer_defaultMove(ReducerContext ctx)
    {
        MovePlayer commandInstance = new MovePlayer(ctx, new DbVector2(1, 0), 0, 0);
        commandInstance.run();
    }

    public MovePlayer(ReducerContext ctx, DbVector2 direction, float yaw, UInt32 sequence) : base(ctx)
    {
        //_direction = direction;
        _direction  = DbVector2.normalize(direction);  
        _sequence = sequence;
        _yaw = yaw;
    }

    //Use the Input table instead to store player input for better synchronization
    protected override void run()
    {
        Module.PlayerInputRow inputRow = new Module.PlayerInputRow();
        inputRow.id = 0;
        inputRow.player = _user.Id;
        inputRow.input = DbVector2.normalize(_direction);
        inputRow.yaw = _yaw;
        inputRow.last_position = _ctx.Db.PlayerTransformRow.player.Find(_user.Id)?.position ?? new DbVector3(0,0,0);
        inputRow.sequence = _sequence;
        _ctx.Db.PlayerInputRow.Insert(inputRow);
    }

}