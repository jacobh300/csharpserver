using SpacetimeDB;

public partial class CommandCompanionMove : ReducerCommand
{
    private int _companionId;
    private DbVector2 _newLocation;

    [SpacetimeDB.Reducer]
    public static void reducer_companionMove(ReducerContext ctx, int companionId, int newLocationX, int newLocationY)
    {
        CommandCompanionMove commandInstance = new CommandCompanionMove(ctx);
        commandInstance._companionId = companionId;
        commandInstance._newLocation = new DbVector2(newLocationX, newLocationY);
        commandInstance.run();
    }


    public CommandCompanionMove(ReducerContext ctx) : base(ctx)
    {
    }

    protected override void run()
    {
        //Check if the user owns the companion
        _user.CompanionRows.TryGetValue(_companionId, out Module.CompanionRow? companionRow);
        if(companionRow == null)
        {
            respond("Companion does not exist or you do not own this companion.");
            return;
        }

        CompanionService companionService = new CompanionService(_ctx);
        companionRow = companionService.MoveCompanion(_companionId, _newLocation);

        if(companionRow == null)
        {
            respond("Error moving companion.");
            return;
        }

        respond($"Companion moved to new location ({_newLocation.x}, {_newLocation.y}).");
    }
}