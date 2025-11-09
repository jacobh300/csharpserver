using SpacetimeDB;
using SpacetimeDB.Internal.TableHandles;
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
    public UserItems Items;

    public UserService(ReducerContext ctx)
    {
        _ctx = ctx;
        _id = ctx.Sender;
        Items = new UserItems(ctx);
    }
}

public class UserItems
{
    private ReducerContext _ctx;
    private Dictionary<int, Module.ItemRow> rows
    {
        get { return _ctx.Db.item.OwnerIndex.Filter(_ctx.Sender).ToDictionary(item => item.item_type_id, item => item); }
    }

    public UserItems(ReducerContext ctx)
    {
        _ctx = ctx;
    }

    public Module.ItemRow addOrUpdateRow(int itemId, int quantity)
    {
        // Use ItemRows property to get current items
        rows.TryGetValue(itemId, out var existingItem);

        if (existingItem == null)
        {

            Module.ItemRow itemAdded = new Module.ItemRow
            {
                owner = _ctx.Sender,
                item_type_id = itemId,
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

