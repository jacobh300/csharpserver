using SpacetimeDB;

public partial class RequestStationaryCast : ReducerCommand
{
    private bool _startCast;

    [SpacetimeDB.Reducer]
    public static void reducer_requestStationaryCast(ReducerContext ctx, bool startCast)
    {
        RequestStationaryCast commandInstance = new RequestStationaryCast(ctx, startCast);
        commandInstance.run();
    }

    public RequestStationaryCast(ReducerContext ctx, bool startCast) : base(ctx)
    {
        _startCast = startCast;
    }

    protected override void run()
    {
        // Get the current move update for the player
        Module.PlayerMoveUpdate? currentMoveUpdate = _ctx.Db.PlayerMoveUpdate.player.Find(_user.Id);
        
        if (currentMoveUpdate == null)
        {
            Log.Warn($"Player {_user.Id} attempted to cast but has no move update entry");
            return;
        }

        if (_startCast)
        {
            // Starting a cast - can only cast while in Run state
            if (currentMoveUpdate.moveType != MoveStateType.Run)
            {
                Log.Warn($"Player {_user.Id} attempted to start cast but is not in Run state (current: {currentMoveUpdate.moveType})");
                return;
            }

            // Check if already casting
            if (currentMoveUpdate.moveType == MoveStateType.Stationary)
            {
                Log.Warn($"Player {_user.Id} attempted to start cast but is already casting");
                return;
            }

            // Transition to Stationary state
            Log.Info($"Player {_user.Id} started casting - transitioning to Stationary state");
            _ctx.Db.PlayerMoveUpdate.player.Update(new Module.PlayerMoveUpdate
            {
                player = _user.Id,
                origin = currentMoveUpdate.origin,
                velocity = new DbVector3(0, 0, 0), // Set velocity to zero while stationary
                moveType = MoveStateType.Stationary,
                yaw = currentMoveUpdate.yaw,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000, // Update timestamp to current time
                lastValidPosition = currentMoveUpdate.lastValidPosition,
                suspiciousActivityCount = currentMoveUpdate.suspiciousActivityCount
            });
        }
        else
        {
            // Ending a cast
            if (currentMoveUpdate.moveType != MoveStateType.Stationary)
            {
                Log.Warn($"Player {_user.Id} attempted to end cast but is not in Stationary state (current: {currentMoveUpdate.moveType})");
                return;
            }

            // Transition back to Run state
            Log.Info($"Player {_user.Id} ended casting - transitioning to Run state");
            _ctx.Db.PlayerMoveUpdate.player.Update(new Module.PlayerMoveUpdate
            {
                player = _user.Id,
                origin = currentMoveUpdate.origin,
                velocity = new DbVector3(0, 0, 0), // Velocity will be updated on next move update
                moveType = MoveStateType.Run,
                yaw = currentMoveUpdate.yaw,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000, // Update timestamp to current time
                lastValidPosition = currentMoveUpdate.lastValidPosition,
                suspiciousActivityCount = currentMoveUpdate.suspiciousActivityCount
            });
        }
    }
}
