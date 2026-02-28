using SpacetimeDB;

public class ReducerResponse
{
    protected string _data;
    protected ReducerContext _ctx;
    public ReducerResponse(ReducerContext ctx, string data)
    {
        _ctx = ctx;
        _data = data;
    }

    public virtual void send()
    {
        // Default implementation sends response data to the user who sent the command
        sendResponseToUser(_ctx, _data);
    }

    protected static void sendResponseToUser(ReducerContext ctx, string eventData)
    {
        ctx.Db.ResponseRow.Insert(
            new Module.ResponseRow
            {
                identity = ctx.Sender,
                sent = ctx.Timestamp,
                data = eventData
            });

        Log.Info($"Event sent to {ctx.Sender}: {eventData}");
    }
}