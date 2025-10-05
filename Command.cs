using SpacetimeDB;

public abstract class Command
{
    public Command()
    {

    }

    public virtual void Execute(ReducerContext ctx, string[] args)
    {
        // Default implementation (can be overridden by subclasses)
        // For example, log the command execution
        Log.Info($"Executing command with args: {string.Join(", ", args)}");
    }

    protected static void sendEventToUser(ReducerContext ctx, Identity identity, string eventData)
    {
        ctx.Db.events.Insert(
            new Module.EventRow
            {
                identity = identity,
                data = eventData
            });

        Log.Info($"Event sent to {identity}: {eventData}");
    }
}



public class HelpCommand : Command
{
    public HelpCommand()
    {

    }

    public override void Execute(ReducerContext ctx, string[] args)
    {
        base.Execute(ctx, args);
        sendEventToUser(ctx, ctx.Sender, "Available commands: help, list, msg");
    }
}


public class FindUser : Command
{
    public FindUser()
    {

    }

    public override void Execute(ReducerContext ctx, string[] args)
    {
        base.Execute(ctx, args);
        if (args.Length < 1)
        {
            sendEventToUser(ctx, ctx.Sender, "Usage: finduser <username>");
            return;
        }

        string username = args[0];
        foreach (var user in ctx.Db.user.Iter())
        {
            if (user.name == username)
            {
                sendEventToUser(ctx, ctx.Sender, $"User found: {username} (Online: {user.online})");
                return;
            }
        }
        
        sendEventToUser(ctx, ctx.Sender, $"User not found: {username}");
        
    }
}


