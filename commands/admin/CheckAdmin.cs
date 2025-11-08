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
        //Check if user is apart of the admin table
        Module.AdminRow? adminRow = _ctx.Db.admin.identity.Find(_ctx.Sender);
        if (adminRow == null)
        {
            respond("You are not an admin.");
            return;
        }
        
        respond("You are an admin.");
    }

}