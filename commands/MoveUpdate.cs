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

    protected override void run()
    {
        if (_lastMoveUpdate == null)
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
            player = _lastMoveUpdate.player,
            origin = _lastMoveUpdate.origin,
            velocity = _lastMoveUpdate.velocity,
            moveType = _lastMoveUpdate.moveType,
            timestamp = _lastMoveUpdate.timestamp
        };

        // Simple check to see if the player is standing still
        if (_moveType == lastUpdate.moveType && _moveUpdate.origin.Equals(lastUpdate.origin))
        {
            // No movement - just update timestamp
            _ctx.Db.player_move_updates.player.Update(new Module.PlayerMoveUpdate
            {
                player = _user.Id,
                origin = _lastMoveUpdate.origin,
                velocity = new DbVector3(0, 0, 0),
                moveType = _moveType,
                yaw = _moveUpdate.yaw,
                timestamp = _ctx.Timestamp.MicrosecondsSinceUnixEpoch,
                lastValidPosition = _lastMoveUpdate.lastValidPosition,
                suspiciousActivityCount = _lastMoveUpdate.suspiciousActivityCount
            });
            return;
        }
        
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
                yaw = _moveUpdate.yaw,
                timestamp = _moveUpdate.timestamp,
                lastValidPosition = _moveUpdate.origin,  // Update last valid
                suspiciousActivityCount = 0  // Reset counter
            });
        }
        else
        {
            // REJECT - log and potentially ban
            _lastMoveUpdate.suspiciousActivityCount++;
            
            Log.Warn($"Player {_user.Id} failed validation: {result.ErrorMessage}");
            
            if (_lastMoveUpdate.suspiciousActivityCount > 10 && false) // Disable ban for now
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
                origin = _lastMoveUpdate.lastValidPosition,  // Reset position
                velocity = new DbVector3(0, 0, 0),     // Zero velocity
                moveType = MoveStateType.Idle,         // Force idle
                yaw = _lastMoveUpdate.yaw,             // Reset yaw
                timestamp = _ctx.Timestamp.MicrosecondsSinceUnixEpoch,
                lastValidPosition = _lastMoveUpdate.lastValidPosition,
                suspiciousActivityCount = _lastMoveUpdate.suspiciousActivityCount
            });
        }
    } 
}