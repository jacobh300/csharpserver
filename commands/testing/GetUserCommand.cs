using SpacetimeDB;

public partial class GetUserCommand : ReducerCommand
{

    [SpacetimeDB.Reducer]
    public static void reducer_getUserCommand(ReducerContext ctx, string username)
    {
        GetUserCommand commandInstance = new GetUserCommand(ctx, username);
        commandInstance.run();
    }

    string _username;

    public GetUserCommand(ReducerContext ctx, string username) : base(ctx)
    {
        _username = username;
    }

    protected override void run()
    {
        foreach (var user in _ctx.Db.UserRow.Iter())
        {
            if (user.name == _username)
            {
                respond($"User found: {_username} (Online: {user.online})");
                return;
            }
        }

        respond($"User not found: {_username}");
    }

}