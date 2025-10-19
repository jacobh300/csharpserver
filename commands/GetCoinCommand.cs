using SpacetimeDB;


public partial class GetCoinCommand : ReducerCommand
{

    [SpacetimeDB.Reducer]
    public static void reducer_getCoinCommand(ReducerContext ctx)
    {
        GetCoinCommand commandInstance = new GetCoinCommand(ctx);
        commandInstance.run();
    }

    public GetCoinCommand(ReducerContext ctx) : base(ctx)
    {
    }

    protected override void run()
    {
        //Give the user 1 coin
        Module.ItemRow updatedRow = giveItem("coin", 1);
        if(updatedRow == null)
        {
            respond("Error giving coin.");
            return;
        }
        respond($"You have {updatedRow.quantity} coins.");
    }

}