using SpacetimeDB;
using SpacetimeDB.Internal.TableHandles;

public partial class MoveUpdate : ReducerCommand
{
    Module.PlayerMoveUpdate? _lastMoveUpdate;
    PlayerMoveRequest _moveUpdate;
    MoveStateType _moveType;


    [SpacetimeDB.Reducer]
    public static void reducer_moveUpdate(ReducerContext ctx, MoveStateType moveType, PlayerMoveRequest moveRequest)
    {
        MoveUpdate commandInstance = new MoveUpdate(ctx, moveType, moveRequest);
        commandInstance.run();
    }

    public MoveUpdate(ReducerContext ctx, MoveStateType moveType, PlayerMoveRequest moveRequest) : base(ctx)
    {
        _lastMoveUpdate = _ctx.Db.player_move_updates.player.Find(_user.Id);
        _moveUpdate = moveRequest;
        _moveType = moveType;
    }

    //The way the system works:
    //Client reports what happened for them then the server validates.
    protected override void run()
    {
        // Get last known state
        var lastState = _ctx.Db.player_move_updates.player.Find(_user.Id);
        
        if (lastState == null)
        {
            // First move - just accept it
            _ctx.Db.player_move_updates.Insert(new Module.PlayerMoveUpdate
            {
                player = _user.Id,
                origin = _moveUpdate.origin,
                velocity = _moveUpdate.velocity,
                moveType = _moveType,
                timestamp = _moveUpdate.timestamp,
                lastValidPosition = _moveUpdate.origin,
                suspiciousActivityCount = 0
            });
            return;
        }
        
        // Convert to PlayerMoveUpdate for validation
        var lastUpdate = new Module.PlayerMoveUpdate
        {
            player = lastState.player,
            origin = lastState.origin,
            velocity = lastState.velocity,
            moveType = lastState.moveType,
            timestamp = lastState.timestamp
        };
        
        // VALIDATE
        var result = MoveValidator.Validate(_moveType, _moveUpdate, lastUpdate);
        
        if (result.IsValid)
        {
            // Accept movement
            _ctx.Db.player_move_updates.player.Update(new Module.PlayerMoveUpdate
            {
                player = _user.Id,
                origin = _moveUpdate.origin,
                velocity = _moveUpdate.velocity,
                moveType = _moveType,
                timestamp = _moveUpdate.timestamp,
                lastValidPosition = _moveUpdate.origin,  // Update last valid
                suspiciousActivityCount = 0  // Reset counter
            });
            
            Log.Info($"Player {_user.Id} moved to {_moveUpdate.origin}");
        }
        else
        {
            // REJECT - log and potentially ban
            lastState.suspiciousActivityCount++;
            
            Log.Warn($"Player {_user.Id} failed validation: {result.ErrorMessage}");
            
            if (lastState.suspiciousActivityCount > 10 && false) // Disable ban for now
            {
                // Ban player
                Log.Error($"Player {_user.Id} BANNED for repeated exploits");
                // Could add to ban table here
                return;
            }
            
            // Force correction - reset to last valid position
            _ctx.Db.player_move_updates.player.Update(new Module.PlayerMoveUpdate
            {
                player = _user.Id,
                origin = lastState.lastValidPosition,  // Reset position
                velocity = new DbVector3(0, 0, 0),     // Zero velocity
                moveType = MoveStateType.Idle,         // Force idle
                timestamp = _ctx.Timestamp.MicrosecondsSinceUnixEpoch,
                lastValidPosition = lastState.lastValidPosition,
                suspiciousActivityCount = lastState.suspiciousActivityCount
            });
        }
    } 
}