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
public class UserContext
{
    private ReducerContext _ctx;
    private Identity _id;
    public Identity Id { get { return _id; } }
    private Dictionary<int, Module.ItemRow> _items
    {
        get { return _ctx.Db.ItemRow.owner.Filter(_ctx.Sender).ToDictionary(item => item.item_type_id, item => item); }
    }

    public Dictionary<int, Module.ItemRow> ItemRows
    {
        get { return _items; }
    }

    public UserContext(ReducerContext ctx)
    {
        _ctx = ctx;
        _id = ctx.Sender;
    }
}
