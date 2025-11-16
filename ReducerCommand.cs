using SpacetimeDB;


public abstract class ReducerCommand
{
    protected ReducerContext _ctx;
    protected UserContext _user;

    public ReducerCommand(ReducerContext ctx)
    {
        _ctx = ctx;
        _user = new UserContext(ctx);
    }

    protected void respond(string data)
    {
        ReducerResponse response = new ReducerResponse(_ctx, data);
        response.send();
    }

    protected abstract void run();
}