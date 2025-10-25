using SpacetimeDB;


public partial class GetCopperCommand : ReducerCommand
{

    [SpacetimeDB.Reducer]
    public static void reducer_getCopperCommand(ReducerContext ctx)
    {
        GetCopperCommand commandInstance = new GetCopperCommand(ctx);
        commandInstance.run();
    }

    public GetCopperCommand(ReducerContext ctx) : base(ctx)
    {
    }

    protected override void run()
    {
        //Give the user 1 copper
        Module.ItemRow updatedRow = giveItem("copper", 1);
        if(updatedRow == null)
        {
            respond("Error giving copper.");
            return;
        }
        respond($"You have {updatedRow.quantity} copper.");
    }

}