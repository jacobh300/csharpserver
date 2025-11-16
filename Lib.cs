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

    [SpacetimeDB.Table(Name = "companion", Public = true)]
    public partial class CompanionRow
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public int id;
        [SpacetimeDB.Index.BTree(Name = "OwnerIndex")]
        public Identity owner;
        public string name = "";
        public DbVector2 position;
    }




    [Table(Name = "admin", Public = false)]
    public partial class AdminRow
    {
        [SpacetimeDB.PrimaryKey]
        public Identity identity;
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
        [SpacetimeDB.Index.BTree(Name = "OwnerIndex")]
        public Identity owner;
        
        public int item_type_id;
        public int quantity;
    }

    [SpacetimeDB.Table(Name = "item_types", Public = true)]
    public partial class ItemType
    {
        [SpacetimeDB.PrimaryKey]
        public int id;
        [SpacetimeDB.Unique]
        public string name = "";
    }
    
        #endregion

        #region Reducers
        [SpacetimeDB.Reducer]
        public static void SetName(ReducerContext ctx, string name)
        {
            name = Helpers.ValidateName(name);
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
            text = Helpers.ValidateMessage(text);

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

                //If this is the first user, make them an admin
                if (ctx.Db.user.Iter().Count() == 1)
                {
                    ctx.Db.admin.Insert
                    (
                        new AdminRow
                        {
                            identity = ctx.Sender
                        }
                    );
                    Log.Info($"First user connected, granted admin: {ctx.Sender}");
                }   
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

        [Reducer(ReducerKind.Init)]
        public static void InitializeModule(ReducerContext ctx)
        {
            // This method is called when the module is first loaded.
            Log.Info("Module initialized.");
        } 
        
        #endregion

    }
