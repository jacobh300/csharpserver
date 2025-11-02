using SpacetimeDB;
using static Module;


/// <summary>
/// User service to handle user-related operations.
/// 
/// Serves the purpose of getting and caching data from other tables
/// specific to a user.
/// 
/// </summary>
public class UserService
{
    private ReducerContext _ctx;
    private Identity _id;
    public ItemTable Items;

    public UserService(ReducerContext ctx)
    {
        _ctx = ctx;
        _id = ctx.Sender;
        Items = new ItemTable(ctx);
    }
}

public class ItemTable
{
    private ReducerContext _ctx;
    private Dictionary<string, Module.ItemRow> rows
    {
        get { return _ctx.Db.item.OwnerIndex.Filter(_ctx.Sender).ToDictionary(item => item.name, item => item); }
    }
    public ItemTable(ReducerContext ctx)
    {
        _ctx = ctx;
    }
    public Module.ItemRow addOrUpdateRow(string itemName, int quantity)
    {
        // Use ItemRows property to get current items
        rows.TryGetValue(itemName, out var existingItem);
        
        if (existingItem == null)
        {

            Module.ItemRow itemAdded = new Module.ItemRow
            {
                owner = _ctx.Sender,
                name = itemName,
                quantity = quantity
            };

            _ctx.Db.item.Insert(itemAdded);
            return itemAdded;

        }
        else
        {
            existingItem.quantity += quantity;

            _ctx.Db.item.id.Update(existingItem);
            return existingItem;
        }
    }
}


