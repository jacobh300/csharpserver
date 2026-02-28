using System.Numerics;
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
    
    [Table(Name = "game_tick_schedule", Public = true, Scheduled = nameof(gameTick), ScheduledAt = nameof(ScheduledAt))]
    public partial struct GameTickSchedule
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong id;
        public ScheduleAt ScheduledAt;
        public Timestamp lastExecuted;
        public long tick;
        public long lastTick;
        public long lastSecondTick;
    }

    [Table(Name = "player_move_updates", Public = true)]
    public partial class PlayerMoveUpdate
    {
        [SpacetimeDB.PrimaryKey]
        public Identity player;
        public MoveStateType moveType; 
        public DbVector3 origin;
        public DbVector3 velocity;
        public float yaw;
        /// <summary>
        /// In microseconds.
        /// </summary>
        public long timestamp;
        public float duration;
        public DbVector3 lastValidPosition;
        public int suspiciousActivityCount;
    }


    #endregion

    #region Reducers
    [SpacetimeDB.Reducer]
    public static void SetName(ReducerContext ctx, string name)
    {
        name = Helpers.ValidateName(name);
        var user = ctx.Db.UserRow.identity.Find(ctx.Sender);
        if (user is not null)
        {
            user.name = name;
            ctx.Db.UserRow.identity.Update(user);
        }
    }
    [SpacetimeDB.Reducer]
    public static void SendMessage(ReducerContext ctx, string text)
    {
        text = Helpers.ValidateMessage(text);

        ctx.Db.MessageRow.Insert(
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
        var user = ctx.Db.UserRow.identity.Find(ctx.Sender);
        if (user is not null)
        {
            user.online = true;
            ctx.Db.UserRow.identity.Update(user);

            

        }       
        else
        {
            ctx.Db.UserRow.Insert
            (
                new UserRow
                {
                    name = null,
                    identity = ctx.Sender,
                    online = true
                }
            );

            //If this is the first user, make them an admin
            if (ctx.Db.UserRow.Iter().Count() == 1)
            {
                ctx.Db.AdminRow.Insert
                (
                    new AdminRow
                    {
                        identity = ctx.Sender
                    }
                );
                Log.Info($"First user connected, granted admin: {ctx.Sender}");
            }  

            ctx.Db.PlayerMoveUpdate.Insert(new PlayerMoveUpdate
            {
                player = ctx.Sender,
                origin = new DbVector3(0, 0, 0),
                velocity = new DbVector3(0, 0, 0),
                moveType = MoveStateType.Run,
                timestamp = 0,
                lastValidPosition = new DbVector3(0, 0, 0),
                suspiciousActivityCount = 0,
            }); 
        }
    }

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        var user = ctx.Db.UserRow.identity.Find(ctx.Sender);

        if (user is not null)
        {
            // This user should exist, so set `Online: false`.
            user.online = false;
            ctx.Db.UserRow.identity.Update(user);
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
        var currentTime = ctx.Timestamp;
        var thiryFramesPerSecondTickRate = new TimeDuration { Microseconds = 33333 }; 
        ctx.Db.GameTickSchedule.Insert(new GameTickSchedule
        {
            id = 0,
            ScheduledAt = new ScheduleAt.Interval(thiryFramesPerSecondTickRate),
            lastExecuted = currentTime,
            tick = 0,
            lastTick = 0,
        });

    } 
    
    [SpacetimeDB.Reducer]
    public static void gameTick(ReducerContext ctx, GameTickSchedule schedule)
    {
        Game.tick(ctx, schedule);
    }
    
    #endregion

    }
