using SpacetimeDB;

public partial class CommandCompanionGet : ReducerCommand
{

    [SpacetimeDB.Reducer]
    public static void reducer_companionGet(ReducerContext ctx)
    {
        CommandCompanionGet commandInstance = new CommandCompanionGet(ctx);
        commandInstance.run();
    }


    public CommandCompanionGet(ReducerContext ctx) : base(ctx)
    {

    }

    protected override void run()
    {
        //Gives the user a random companion
        CompanionService companionService = new CompanionService(_ctx);
        Module.CompanionRow? newCompanion = companionService.CreateRandomCompanionForUser(_user);
        if(newCompanion == null)
        {
            respond("Error creating companion.");
            return;
        }
        respond($"You have received a new companion: {newCompanion.name} at location ({newCompanion.position.x}, {newCompanion.position.y}).");
    }
}