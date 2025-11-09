using SpacetimeDB;


public partial class CheckAdmin : ReducerCommand
{

    [SpacetimeDB.Reducer]
    public static void reducer_checkAdmin(ReducerContext ctx)
    {
        CheckAdmin commandInstance = new CheckAdmin(ctx);
        commandInstance.run();
    }

    public CheckAdmin(ReducerContext ctx) : base(ctx)
    {
    }

    protected override void run()
    {
        if(Helpers.IsAdmin(_ctx) == false)
        {
            respond("You are not an admin.");
            return;
        }
        
        respond("You are an admin.");
    }

}