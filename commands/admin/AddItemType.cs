using SpacetimeDB;


public partial class AddItemType : ReducerCommand
{
    private int _id;
    private string _name;

    [SpacetimeDB.Reducer]
    public static void reducer_addItemType(ReducerContext ctx, int id, string name)
    {
        AddItemType commandInstance = new AddItemType(ctx, id, name);
        commandInstance.run();
    }

    public AddItemType(ReducerContext ctx, int id, string name) : base(ctx)
    {
        _id = id;
        _name = name;
    }

    protected override void run()
    {
        if (Helpers.IsAdmin(_ctx) == false)
        {
            respond("You must be an admin to use this command.");
            return;
        }


        //Check if item tpye already exists
        foreach (Module.ItemType itemRow in _ctx.Db.item_types.Iter())
        {
            if (itemRow.name == _name || itemRow.id == _id)
            {
                respond("Item type already exists: " + _name);
                return;
            }
        }

        Module.ItemType itemType = _ctx.Db.item_types.Insert
        (
            new Module.ItemType
            {
                id = _id,
                name = _name
            }
        );
        

        if (itemType == null)
        {
            respond("Error adding item type.");
            return;
        }
        
        respond("Added item type: " + itemType.name);
    }

}