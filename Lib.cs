using SpacetimeDB;
#pragma warning disable STDB_UNSTABLE

public static partial class Module
{

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter RESPONSE_FILTER = new Filter.Sql(
        "SELECT * FROM response WHERE response.identity = :sender"
    );



    #region Tables
    [SpacetimeDB.Table(Name = "user", Public = true)]
    public partial class UserRow
    {
        [SpacetimeDB.PrimaryKey]
        public Identity identity;
        public string? name;
        public bool online;
    }

    [Table(Name = "message", Public = true)]
    public partial class MessageRow
    {
        public Identity sender;
        public Timestamp sent;
        public string text = "";
    }

    [SpacetimeDB.Table(Name = "response", Public = true)]
    public partial class ResponseRow
    {
        [SpacetimeDB.AutoInc]
        public int id;
        public Identity identity;
        public Timestamp sent;
        public string data = "";
    }

    [SpacetimeDB.Table(Name = "item", Public = true)]
    public partial class ItemRow
    {
        [SpacetimeDB.AutoInc]
        [SpacetimeDB.PrimaryKey]
        public int id;
        public Identity owner;
        public string name = "";
        public int quantity;
    }
    
    #endregion

    #region Reducers
    [SpacetimeDB.Reducer]
    public static void SetName(ReducerContext ctx, string name)
    {
        name = ValidateName(name);
        var user = ctx.Db.user.identity.Find(ctx.Sender);
        if (user is not null)
        {
            user.name = name;
            ctx.Db.user.identity.Update(user);
        }
    }
    [SpacetimeDB.Reducer]
    public static void SendMessage(ReducerContext ctx, string text)
    {
        text = ValidateMessage(text);

        ctx.Db.message.Insert(
            new MessageRow
            {
                sender = ctx.Sender,
                sent = ctx.Timestamp,
                text = text
            });
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        Log.Info($"Client connected: {ctx.Sender}");
        var user = ctx.Db.user.identity.Find(ctx.Sender);
        if (user is not null)
        {
            user.online = true;
            ctx.Db.user.identity.Update(user);
        }
        else
        {
            ctx.Db.user.Insert
            (
                new UserRow
                {
                    name = null,
                    identity = ctx.Sender,
                    online = true
                }
            );
        }
    }

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        var user = ctx.Db.user.identity.Find(ctx.Sender);

        if (user is not null)
        {
            // This user should exist, so set `Online: false`.
            user.online = false;
            ctx.Db.user.identity.Update(user);
        }
        else
        {
            // User does not exist, log warning
            Log.Warn("Warning: No user found for disconnected client.");
        }
    }
    #endregion

    #region Private functions
    /// <summary>
    /// Takes a name and checks if it's acceptable to use.
    /// </summary>
    /// <param name="name">Input name</param>
    /// <returns>Return validated name</returns>
    /// <exception cref="Exception"></exception>
    private static string ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new Exception("Name cannot be empty");
        return name;
    }

    /// <summary>
    /// Takes a message and checks if it's acceptable to use.
    /// </summary>
    /// <param name="text">Input text</param>
    /// <returns>Return validated message</returns>
    /// <exception cref="ArgumentException"></exception>
    private static string ValidateMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Messages must not be empty");
        }
        return text;
    }

    #endregion
}
