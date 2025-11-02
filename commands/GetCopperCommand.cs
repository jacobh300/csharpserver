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
        Module.ItemRow updatedRow = _user.Items.addOrUpdateRow("copper", 1);
        if (updatedRow == null)
        {
            respond("Error giving copper.");
            return;
        }
        respond($"You have {updatedRow.quantity} copper.");
    }

}