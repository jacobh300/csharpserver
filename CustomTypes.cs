using SpacetimeDB;
[SpacetimeDB.Type]
public partial struct DbVector3
{
    public float x;
    public float y;
    public float z;

    public DbVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }


    public static DbVector3 operator +(DbVector3 a, DbVector3 b)
    {
        return new DbVector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static DbVector3 operator *(DbVector3 v, float scalar)
    {
        return new DbVector3(v.x * scalar, v.y * scalar, v.z * scalar);
    }

    public static DbVector3 operator *(float scalar, DbVector3 v)
    {
        return new DbVector3(v.x * scalar, v.y * scalar, v.z * scalar);
    }

    public float magnitude => (float)System.Math.Sqrt(x * x + y * y + z * z);

    public static DbVector3 normalize(DbVector3 v)
    {
        float length = (float)System.Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        if (length > 0)
        {
            return new DbVector3(v.x / length, v.y / length, v.z / length);
        }
        return new DbVector3(0, 0, 0);
    }

    public static float Distance(DbVector3 a, DbVector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        float dz = a.z - b.z;
        return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }


}
[SpacetimeDB.Type]
public partial struct DbVector2
{
    public float x;
    public float y;

    public DbVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public static DbVector2 normalize(DbVector2 v)
    {
        float length = (float)System.Math.Sqrt(v.x * v.x + v.y * v.y);
        if (length > 0)
        {
            return new DbVector2(v.x / length, v.y / length);
        }
        return new DbVector2(0, 0);
    }

}

[SpacetimeDB.Type]
public partial struct PlayerMoveRequest
{
    public DbVector3 origin;
    public DbVector3 destination;
    public DbVector3 velocity;
    public long timestamp;
    public float duration;
}

/// <summary>
/// Enum representing different types of player movement states.  
/// </summary>  
/// 
[SpacetimeDB.Type]
public enum MoveStateType
{
    Idle,
    Walk,
    Run,
    Jump,
    Fall
}

public class ValidationResult
{
    public bool IsValid;
    public string ErrorMessage;

    public ValidationResult(bool isValid, string errorMessage = "")
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
}

public static class StateTransitionRules
{
    // Simple allowed transitions map
    private static readonly HashSet<(MoveStateType, MoveStateType)> _allowed = new()
    {
        // Can always stay in same state
        // Grounded states
        (MoveStateType.Idle, MoveStateType.Walk),
        (MoveStateType.Idle, MoveStateType.Run),
        (MoveStateType.Idle, MoveStateType.Jump),
        (MoveStateType.Idle, MoveStateType.Fall),
        
        (MoveStateType.Walk, MoveStateType.Idle),
        (MoveStateType.Walk, MoveStateType.Run),
        (MoveStateType.Walk, MoveStateType.Jump),
        (MoveStateType.Walk, MoveStateType.Fall),
        
        (MoveStateType.Run, MoveStateType.Idle),
        (MoveStateType.Run, MoveStateType.Walk),
        (MoveStateType.Run, MoveStateType.Jump),
        (MoveStateType.Run, MoveStateType.Fall),
        
        // Airborne states
        (MoveStateType.Jump, MoveStateType.Fall),
        (MoveStateType.Jump, MoveStateType.Idle),
        (MoveStateType.Jump, MoveStateType.Walk),
        (MoveStateType.Jump, MoveStateType.Run),
        
        (MoveStateType.Fall, MoveStateType.Idle),
        (MoveStateType.Fall, MoveStateType.Walk),
        (MoveStateType.Fall, MoveStateType.Run),
    };
    
    public static ValidationResult Validate(MoveStateType moveType, Module.PlayerMoveUpdate last)
    {
        // Same state is always OK
        if (moveType == last.moveType)
            return new ValidationResult(true);
        
        // Check if transition is allowed
        if (!_allowed.Contains((last.moveType, moveType)))
        {
            return new ValidationResult(false, 
                $"Invalid state transition: {last.moveType} → {moveType}");
        }
        
        return new ValidationResult(true);
    }
}

public static class MoveValidator
{
    public static ValidationResult Validate(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last)
    {
        // Layer 1: Fast sanity checks (catches 95% of exploits)
        var sanityResult = SanityChecks.Validate(moveType, moveRequest, last);
        if (!sanityResult.IsValid)
            return sanityResult;
        
        // Layer 2: State transition rules
        var stateResult = StateTransitionRules.Validate(moveType, last);
        if (!stateResult.IsValid)
            return stateResult;
        
        // Layer 3: Light physics plausibility
        var physicsResult = PlausibilityChecks.Validate(moveType, moveRequest, last);
        if (!physicsResult.IsValid)
            return physicsResult;
        
        // All checks passed!
        return new ValidationResult(true);
    }
}

public static class SanityChecks
{
    // Max speeds per state (generous to avoid false positives)
    private static readonly Dictionary<MoveStateType, float> MaxSpeeds = new()
    {
        [MoveStateType.Idle] = 2.0f,      // Basically stationary
        [MoveStateType.Walk] = 6.0f,      // Walk speed
        [MoveStateType.Run] = 12.0f,      // Sprint speed
        [MoveStateType.Jump] = 15.0f,     // Jumping arc
        [MoveStateType.Fall] = 30.0f,     // Terminal velocity + margin
    };
    
    public static ValidationResult Validate(MoveStateType moveType, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Check 1: Time delta is reasonable
        float dt = (moveRequest.timestamp - last.timestamp) / 1_000_000.0f;
        if (dt < 0.001f || dt > 1.0f)
        {
            return new ValidationResult(false, 
                $"Invalid time delta: {dt}s (must be 0.001-1.0)");
        }
        
        // Check 2: Didn't teleport
        float distance = DbVector3.Distance(moveRequest.origin, last.origin);
        float maxDistance = 50.0f * dt;  // Even at max speed, can't go > 50 m/s
        
        if (distance > maxDistance)
        {
            return new ValidationResult(false, 
                $"Teleport detected: moved {distance:F2}m in {dt:F3}s " +
                $"(max: {maxDistance:F2}m)");
        }
        
        // Check 3: Speed is reasonable for current state
        float speed = distance / dt;
        float maxSpeed = MaxSpeeds.GetValueOrDefault(moveType, 20.0f);
        
        if (speed > maxSpeed * 1.2f)  // 20% tolerance
        {
            return new ValidationResult(false, 
                $"Speed too high: {speed:F2} m/s for state {moveType} " +
                $"(max: {maxSpeed:F2} m/s)");
        }
        
        // Check 4: Velocity magnitude is reasonable
        float velMagnitude = moveRequest.velocity.magnitude;
        if (velMagnitude > 50.0f)  // Global max
        {
            return new ValidationResult(false, 
                $"Velocity magnitude too high: {velMagnitude:F2} m/s");
        }
        
        // Check 5: Not flying (unless jumping/falling)
        if (moveType == MoveStateType.Walk || 
            moveType == MoveStateType.Run ||
            moveType == MoveStateType.Idle)
        {
            if (moveRequest.origin.y > 0.5f)  // Allow small elevation for terrain
            {
                return new ValidationResult(false, 
                    $"Grounded state but Y={moveRequest.origin.y:F2} (flying?)");
            }
            
            if (Math.Abs(moveRequest.velocity.y) > 0.5f)
            {
                return new ValidationResult(false, 
                    $"Grounded state but vy={moveRequest.velocity.y:F2}");
            }
        }
        
        return new ValidationResult(true);
    }
}

public static class PlausibilityChecks
{
    private const float GRAVITY = -9.81f;
    private const float JUMP_FORCE = 5.0f;
    
    public static ValidationResult Validate(MoveStateType moveType, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Only validate transitions that need physics checks
        return (last.moveType, moveType) switch
        {
            // Jumping from ground
            (MoveStateType.Idle or MoveStateType.Walk or MoveStateType.Run, 
             MoveStateType.Jump) => ValidateJumpStart(moveRequest),
            
            // Continuing jump
            (MoveStateType.Jump, MoveStateType.Jump) => ValidateJumpContinue(moveRequest, last),
            
            // Falling
            (MoveStateType.Fall, MoveStateType.Fall) => ValidateFalling(moveRequest, last),
            
            // Landing
            (MoveStateType.Jump or MoveStateType.Fall, 
             MoveStateType.Idle or MoveStateType.Walk or MoveStateType.Run) 
                => ValidateLanding(moveRequest),
            
            // Everything else is fine
            _ => new ValidationResult(true)
        };
    }
    
    private static ValidationResult ValidateJumpStart(PlayerMoveRequest moveRequest)
    {
        // Just check initial velocity is reasonable
        float vyTolerance = 3.0f;  // Very generous
        
        if (moveRequest.velocity.y < JUMP_FORCE - vyTolerance || 
            moveRequest.velocity.y > JUMP_FORCE + vyTolerance)
        {
            return new ValidationResult(false, 
                $"Jump start velocity {moveRequest.velocity.y:F2} not near expected {JUMP_FORCE:F2}");
        }
        
        return new ValidationResult(true);
    }
    
    private static ValidationResult ValidateJumpContinue(
        PlayerMoveRequest moveRequest, 
        Module.PlayerMoveUpdate last)
    {
        // Check velocity is going down (gravity applied)
        if (moveRequest.velocity.y >= last.velocity.y)
        {
            return new ValidationResult(false, 
                "Jump velocity should decrease due to gravity");
        }
        
        // Check it's still going up (otherwise should be Fall state)
        if (moveRequest.velocity.y <= 0)
        {
            return new ValidationResult(false, 
                "Jump state but velocity is downward (should be Fall)");
        }
        
        return new ValidationResult(true);
    }
    
    private static ValidationResult ValidateFalling(
        PlayerMoveRequest moveRequest, 
        Module.PlayerMoveUpdate last)
    {
        // Check velocity is downward
        if (moveRequest.velocity.y > 0)
        {
            return new ValidationResult(false, 
                "Fall state but velocity is upward");
        }
        
        // Check terminal velocity not exceeded
        const float TERMINAL_VELOCITY = -25.0f;
        if (moveRequest.velocity.y < TERMINAL_VELOCITY)
        {
            return new ValidationResult(false, 
                $"Fall velocity {moveRequest.velocity.y:F2} exceeds terminal velocity {TERMINAL_VELOCITY:F2}");
        }
        
        // Check velocity is getting more negative (or at terminal)
        if (moveRequest.velocity.y > last.velocity.y + 0.5f)  // Allow some slowing (air resistance)
        {
            return new ValidationResult(false, 
                "Fall velocity should increase (or stay at terminal velocity)");
        }
        
        return new ValidationResult(true);
    }
    
    private static ValidationResult ValidateLanding(PlayerMoveRequest moveRequest)
    {
        // Check player is at ground level
        if (moveRequest.origin.y > 0.2f)
        {
            return new ValidationResult(false, 
                $"Landing but Y={moveRequest.origin.y:F2} (not on ground)");
        }
        
        // Check vertical velocity is ~0
        if (Math.Abs(moveRequest.velocity.y) > 1.0f)
        {
            return new ValidationResult(false, 
                $"Landing but vy={moveRequest.velocity.y:F2} (should be ~0)");
        }
        
        return new ValidationResult(true);
    }
}