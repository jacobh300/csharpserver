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
        ItemService itemService = new ItemService(_ctx);
        Module.ItemRow? updatedRow = itemService.GiveItemToUser(_user, ItemService.ItemTypeIds.Copper, 1);
        
        if (updatedRow == null)
        {
            respond("Error giving copper.");
            return;
        }
        respond($"You have {updatedRow.quantity} copper.");
    }

}