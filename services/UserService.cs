

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

    public UserService(ReducerContext ctx)
    {
        _ctx = ctx;
        _id = ctx.Sender;
    }


    public List<Module.ItemRow> GetUserItems()
    {
        // Not yet implemented
        return new List<Module.ItemRow>();
    }

}