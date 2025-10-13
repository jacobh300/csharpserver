using SpacetimeDB;

public partial class HelpCommand : ReducerCommand
{
    [SpacetimeDB.Reducer]
    public static void reducer_helpCommand(ReducerContext ctx)
    {
        HelpCommand commandInstance = new HelpCommand(ctx);
        commandInstance.run();
    }

    public HelpCommand(ReducerContext ctx) : base(ctx) { }

    protected override void run()
    {
        respond("Available commands: help, getUser <username>");
    }
}