using SpacetimeDB;
// Transitions are centralized
public static class MoveStateTransitions
{
    private static readonly HashSet<(MoveStateType, MoveStateType)> _allowedTransitions = new()
    {
        // Ground movement
        (MoveStateType.Idle, MoveStateType.Walk),
        (MoveStateType.Walk, MoveStateType.Idle),
        (MoveStateType.Walk, MoveStateType.Run),
        (MoveStateType.Run, MoveStateType.Walk),
        
        // Jumping
        (MoveStateType.Idle, MoveStateType.Jump),
        (MoveStateType.Walk, MoveStateType.Jump),
        (MoveStateType.Run, MoveStateType.Jump),
        
        // Falling
        (MoveStateType.Jump, MoveStateType.Fall),
        (MoveStateType.Walk, MoveStateType.Fall),  // Walked off edge
        (MoveStateType.Run, MoveStateType.Fall),   // Ran off edge
        
        // Landing
        (MoveStateType.Jump, MoveStateType.Idle),
        (MoveStateType.Jump, MoveStateType.Walk),
        (MoveStateType.Jump, MoveStateType.Run),
        (MoveStateType.Fall, MoveStateType.Idle),
        (MoveStateType.Fall, MoveStateType.Walk),
        (MoveStateType.Fall, MoveStateType.Run),
    };
    
    public static ValidationResult IsTransitionAllowed(MoveStateType from, MoveStateType to)
    {
        if(from == to || _allowedTransitions.Contains((from, to)))
        {
            return new ValidationResult(true);
        }

        return new ValidationResult(false, $"Transition from {from} to {to} is not allowed.");
    }

    public static ValidationResult Validate(Module.PlayerMoveUpdate moveUpdate, Module.PlayerMoveUpdate lastMoveUpdate, Timestamp now)
    {
       // Add any additional validation logic here, e.g. check if jump height is reasonable, etc.
       if(moveUpdate.moveType == MoveStateType.Jump) return ValidateJump(moveUpdate, lastMoveUpdate, now);

       return new ValidationResult(true);

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="moveUpdate"></param>
    /// <param name="lastMoveUpdate"></param>
    /// <returns></returns>
    public static ValidationResult ValidateJump(Module.PlayerMoveUpdate moveUpdate, Module.PlayerMoveUpdate lastMoveUpdate, Timestamp now)
    {
        if (moveUpdate.velocity.y <= 0)
        {
            return new ValidationResult(false, "velocity.y must be positive for a jump");
        } 

        // Validate velocity is reasonable (global check)
        const float MAX_UPWARD_VELOCITY = 15.0f;  // Sanity check
        if (moveUpdate.velocity.y > MAX_UPWARD_VELOCITY)
        {
            return new ValidationResult(false, $"Jump velocity too high: {moveUpdate.velocity.y}");
        }

        // KEY FIX: Validate POSITION follows ballistic trajectory from ORIGIN
        if (lastMoveUpdate.moveType == MoveStateType.Jump)
        {
            float dt = (moveUpdate.timestamp - lastMoveUpdate.timestamp) / 1_000_000.0f;
            
            // Compute expected position using ballistic physics from LAST VALIDATED position
            DbVector3 expectedPos = lastMoveUpdate.origin  // Use origin, not destination!
                + lastMoveUpdate.velocity * dt
                + 0.5f * new DbVector3(0, -9.81f, 0) * dt * dt;
            
            float positionError = DbVector3.Distance(expectedPos, moveUpdate.origin);
            
            Log.Info($"Validating jump position: expected={expectedPos}, actual={moveUpdate.origin}, error={positionError}");
            
            if (positionError > 0.5f)  // 50cm tolerance
            {
                return new ValidationResult(false, 
                    $"Jump position doesn't match trajectory. Expected {expectedPos}, got {moveUpdate.origin}");
            }
            
            // Also validate velocity is in reasonable range (looser check)
            float expectedVelY = lastMoveUpdate.velocity.y + (-9.81f * dt);
            float velError = Math.Abs(moveUpdate.velocity.y - expectedVelY);
            
            if (velError > 2.0f)  // Looser tolerance than before
            {
                return new ValidationResult(false, 
                    $"Jump velocity unreasonable. Expected ~{expectedVelY}, got {moveUpdate.velocity.y}");
            }
        }

        return new ValidationResult(true);
    }
}


public static class MoveStateValidator
{
    /// <summary>
    /// Simple check to see if transition from currentState to newState is valid based on predefined allowed transitions.
    /// </summary>
    /// <returns>False if the transition is not allowed, true otherwise.</returns>
    public static ValidationResult ValidateMoveStateTransition(Module.PlayerMoveUpdate moveUpdate, Module.PlayerMoveUpdate lastMoveUpdate, Timestamp now)
    {
        ValidationResult result = MoveStateTransitions.IsTransitionAllowed(lastMoveUpdate.moveType, moveUpdate.moveType);
        if(result.IsValid == false)
        {
            return result;
        }
        return MoveStateTransitions.Validate( moveUpdate, lastMoveUpdate, now);
    }
}