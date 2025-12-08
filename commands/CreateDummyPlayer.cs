using SpacetimeDB;

public partial class CreateDummyPlayer : ReducerCommand
{

    private DbVector2 _direction;


    [SpacetimeDB.Reducer]
    public static void reducer_createDummy(ReducerContext ctx, DbVector2 direction)
    {
        CreateDummyPlayer commandInstance = new CreateDummyPlayer(ctx, direction);
        commandInstance.run();
    }

    [SpacetimeDB.Reducer]
    public static void reducer_createDummyDefault(ReducerContext ctx)
    {
        CreateDummyPlayer commandInstance = new CreateDummyPlayer(ctx, new DbVector2(1, 0));
        commandInstance.run();
    }

    public CreateDummyPlayer(ReducerContext ctx, DbVector2 direction) : base(ctx)
    {
        //_direction = direction;
        _direction  = DbVector2.normalize(direction);  
    }

    //Use the Input table instead to store player input for better synchronization
    protected override void run()
    {
        Module.EntityTransformRow newEntity = new Module.EntityTransformRow();
        newEntity.type = "DummyPlayer";
        newEntity.position = new DbVector3
        {
            x = 0,
            y = 0,
            z = 0
        };
        newEntity.velocity = new DbVector3
        {
            x = _direction.x,
            y = 0,
            z = _direction.y
        };

        _ctx.Db.entity_transform.Insert(newEntity);
        respond($"Created Dummy Player Entity with ID: {newEntity.id}");
    }

}