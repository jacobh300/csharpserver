using SpacetimeDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

// ============================================================================
// TEMPLATE-BASED STATE VALIDATION SYSTEM
// ============================================================================
// Each MoveStateType has a validator that defines:
//   1. Entry Requirements - Can we enter this state from the last state?
//   2. Movement Rules - What are the physics constraints while in this state?
//
// To add a new state:
//   1. Create a class inheriting from BaseMoveStateValidator
//   2. Define CanEnterState() and ValidateMovement() (and optionally ValidateBasic() if you need overridden basic validation)
//   3. Register it in MoveStateValidatorRegistry
// ============================================================================

/// <summary>
/// Base class for state-specific validation logic.
/// Each MoveStateType should have its own validator inheriting from this.
/// </summary>
public abstract class BaseMoveStateValidator
{
    /// <summary>
    /// Check if we can enter this state from the last state.
    /// </summary>
    /// <param name="fromState">The previous MoveStateType</param>
    /// <param name="moveRequest">The requested move data</param>
    /// <param name="last">The last validated move update</param>
    /// <returns>Validation result</returns>
    public abstract ValidationResult CanEnterState(MoveStateType fromState, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last);
    
    /// <summary>
    /// Validate movement physics while in this state.
    /// </summary>
    /// <param name="moveRequest">The requested move data</param>
    /// <param name="last">The last validated move update</param>
    /// <returns>Validation result</returns>
    public abstract ValidationResult ValidateMovement(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last);

    public virtual ValidationResult ValidateBasic(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last, Timestamp serverNow)
    {
        //Validate timestamp
        return ValidationHelpers.ValidateTimestamp(last.timestamp, moveRequest.timestamp, serverNow);
    }
}

/// <summary>
/// Main validator - delegates to state-specific validators.
/// </summary>
public static class MoveValidator
{
    public static ValidationResult Validate(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last, Timestamp serverNow)
    {
        // Get the validator for the requested state
        var validator = MoveStateValidatorRegistry.GetValidator(moveType);
        
        //Do basic validation that applies to all states (e.g. max speed limit)
        var basicCheck = validator.ValidateBasic(moveRequest, last, serverNow);
        if (!basicCheck.IsValid)
        {
            return basicCheck;
        }

        // Check if we can enter this state
        if (moveType != last.moveType)
        {
            var entryCheck = validator.CanEnterState(last.moveType, moveRequest, last);
            if (!entryCheck.IsValid)
                return entryCheck;
        }
        
        // Validate movement in current state
        var movementCheck = validator.ValidateMovement(moveRequest, last);
        if (!movementCheck.IsValid)
            return movementCheck;
        
        return new ValidationResult(true);
    }
}

/// <summary>
/// Registry mapping each MoveStateType to its validator.
/// </summary>
public static class MoveStateValidatorRegistry
{
    private static readonly Dictionary<MoveStateType, BaseMoveStateValidator> _validators = new()
    {
        [MoveStateType.Idle] = new IdleValidator(),
        [MoveStateType.Walk] = new WalkValidator(),
        [MoveStateType.Run] = new RunValidator(),
        [MoveStateType.Jump] = new JumpValidator(),
        [MoveStateType.Fall] = new FallValidator(),
    };
    
    public static BaseMoveStateValidator GetValidator(MoveStateType moveType)
    {
        return _validators[moveType];
    }
}

// ============================================================================
// SHARED CONFIGURATION
// ============================================================================

public static class MovementConstants
{
    // Basic validation
    public const float MAX_MOVE_DISTANCE = 50.0f; // move requests shouldn't be able to move more than this distance from last update in a single tick (prevents teleporting)

    // Grounded movement
    public const float MAX_WALK_SPEED = 6.0f;
    public const float MAX_RUN_SPEED = 12.0f;
    public const float MAX_GROUND_HEIGHT = 0.5f;
    
    // Airborne movement
    public const float JUMP_IMPULSE = 5.0f;
    public const float JUMP_IMPULSE_TOLERANCE = 0.5f;
    public const float JUMP_HORIZONTAL_BOOST = 1.0f;
    public const float MAX_AIRBORNE_HORIZONTAL_SPEED = 13.0f;
    public const float TERMINAL_VELOCITY = 25.0f;
    public const float AIRBORNE_HORIZONTAL_GAIN_TOLERANCE = 0.1f;
    
    // Landing
    public const float LANDING_MAX_HEIGHT = 0.2f;
    public const float LANDING_MAX_VERTICAL_SPEED = 1.0f;
    
    // Physics
    public const float GRAVITY = -9.81f;
    public const float RUN_SPEED_VELOCITY = 12.0f;
}

public static class ValidationHelpers
{
    public static float GetHorizontalSpeed(DbVector3 velocity)
    {
        return new DbVector3(velocity.x, 0, velocity.z).magnitude;
    }
    
    public static bool IsGrounded(MoveStateType state)
    {
        return state == MoveStateType.Idle || state == MoveStateType.Walk || state == MoveStateType.Run;
    }
    
    public static bool IsAirborne(MoveStateType state)
    {
        return state == MoveStateType.Jump || state == MoveStateType.Fall;
    }

    public static float GetHorizontalDistance(DbVector3 a, DbVector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return (float)System.Math.Sqrt(dx * dx + dz * dz);
    }
    public static ValidationResult ValidateTimestamp(long lastTimestamp, long currentTimestamp, Timestamp serverNow)
    {
        int MAX_OFFSET_IN_PAST = 5000;
        int MAX_OFFSET_IN_FUTURE = 1000;

        if (currentTimestamp > serverNow.MicrosecondsSinceUnixEpoch + MAX_OFFSET_IN_FUTURE * 1000)
        {
            Log.Exception($"Timestamp validation failed: currentTimestamp {currentTimestamp} is too far in the future compared to server time {serverNow.MicrosecondsSinceUnixEpoch}");
            return new ValidationResult(false, "Timestamp is too far in the future");
        }

        if (currentTimestamp < serverNow.MicrosecondsSinceUnixEpoch - MAX_OFFSET_IN_PAST * 1000)
        {
            Log.Exception($"Timestamp validation failed: currentTimestamp {currentTimestamp} is too far in the past compared to server time {serverNow.MicrosecondsSinceUnixEpoch}");
            return new ValidationResult(false, "Timestamp is too far in the past");
        }

        if (currentTimestamp <= lastTimestamp)
        {
            Log.Exception($"Timestamp validation failed: currentTimestamp {currentTimestamp} <= lastTimestamp {lastTimestamp}");
            return new ValidationResult(false, "Timestamp is not greater than last timestamp", true);
        }
        return new ValidationResult(true);
    }

    public static ValidationResult ValidateHorizontalMove(Module.PlayerMoveUpdate last, PlayerMoveRequest moveRequest, float maxSpeedForMovement, float tolerance = 0.05f)
    {
        float timeSinceLast = (moveRequest.timestamp - last.timestamp) / 1000.0f / 1000.0f; // convert µs to seconds
        float expectedTime = ValidationHelpers.GetHorizontalDistance(last.origin, moveRequest.origin) / maxSpeedForMovement;
        // Tolerance will allow the player to move 0.05 seconds
        if (timeSinceLast < expectedTime - tolerance) // allow small tolerance for network jitter
        {
            return new ValidationResult(false,
                $"Moved too quick, time since last {timeSinceLast:F2}s, expected minimum {expectedTime:F2} time for move.");
        }
        return new ValidationResult(true);
    }
}

// ============================================================================
// STATE VALIDATORS
// ============================================================================

public class IdleValidator : BaseMoveStateValidator
{
    public override ValidationResult CanEnterState(MoveStateType fromState, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Can idle from any grounded state, or from landing
        if (ValidationHelpers.IsGrounded(fromState) || ValidationHelpers.IsAirborne(fromState))
        {
            // If coming from airborne, validate landing
            if (ValidationHelpers.IsAirborne(fromState))
            {
                if (moveRequest.origin.y > MovementConstants.LANDING_MAX_HEIGHT)
                {
                    return new ValidationResult(false, 
                        $"Cannot land into Idle: Y={moveRequest.origin.y:F2} (max: {MovementConstants.LANDING_MAX_HEIGHT:F2})");
                }
                
                if (Math.Abs(moveRequest.velocity.y) > MovementConstants.LANDING_MAX_VERTICAL_SPEED)
                {
                    return new ValidationResult(false,
                        $"Cannot land into Idle: vy={moveRequest.velocity.y:F2} too high");
                }
            }
            
            return new ValidationResult(true);
        }
        
        return new ValidationResult(false, $"Invalid transition: {fromState} → Idle");
    }
    
    public override ValidationResult ValidateMovement(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Should be on ground
        if (moveRequest.origin.y > MovementConstants.MAX_GROUND_HEIGHT)
        {
            return new ValidationResult(false, $"Idle but Y={moveRequest.origin.y:F2} (not grounded)");
        }
        
        // Should have minimal speed
        float horizontalSpeed = ValidationHelpers.GetHorizontalSpeed(moveRequest.velocity);
        if (horizontalSpeed > 2.0f)
        {
            return new ValidationResult(false, $"Idle but moving at {horizontalSpeed:F2} m/s");
        }
        
        return new ValidationResult(true);
    }
}

public class WalkValidator : BaseMoveStateValidator
{
    public override ValidationResult CanEnterState(MoveStateType fromState, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Can walk from any grounded state, or from landing
        if (ValidationHelpers.IsGrounded(fromState) || ValidationHelpers.IsAirborne(fromState))
        {
            // If coming from airborne, validate landing
            if (ValidationHelpers.IsAirborne(fromState))
            {
                if (moveRequest.origin.y > MovementConstants.LANDING_MAX_HEIGHT)
                {
                    return new ValidationResult(false,
                        $"Cannot land into Walk: Y={moveRequest.origin.y:F2} (max: {MovementConstants.LANDING_MAX_HEIGHT:F2})");
                }
            }
            
            return new ValidationResult(true);
        }
        
        return new ValidationResult(false, $"Invalid transition: {fromState} → Walk");
    }
    
    public override ValidationResult ValidateMovement(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Should be on ground
        if (moveRequest.origin.y > MovementConstants.MAX_GROUND_HEIGHT)
        {
            return new ValidationResult(false, $"Walk but Y={moveRequest.origin.y:F2} (not grounded)");
        }
        
        // Speed should be within walk limits
        float horizontalSpeed = ValidationHelpers.GetHorizontalSpeed(moveRequest.velocity);
        if (horizontalSpeed > MovementConstants.MAX_WALK_SPEED)
        {
            return new ValidationResult(false,
                $"Walk speed {horizontalSpeed:F2} m/s exceeds max {MovementConstants.MAX_WALK_SPEED:F2} m/s");
        }
        
        return new ValidationResult(true);
    }
}

public class RunValidator : BaseMoveStateValidator
{
    public override ValidationResult CanEnterState(MoveStateType fromState, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Can run from any grounded state, or from landing
        if (ValidationHelpers.IsGrounded(fromState) || ValidationHelpers.IsAirborne(fromState))
        {
            // If coming from airborne, validate landing
            if (ValidationHelpers.IsAirborne(fromState))
            {
                if (moveRequest.origin.y > MovementConstants.LANDING_MAX_HEIGHT)
                {
                    return new ValidationResult(false,
                        $"Cannot land into Run: Y={moveRequest.origin.y:F2} (max: {MovementConstants.LANDING_MAX_HEIGHT:F2})");
                }
            }
            
            return ValidationHelpers.ValidateHorizontalMove(last, moveRequest, MovementConstants.RUN_SPEED_VELOCITY);
        }
        
        return new ValidationResult(false, $"Invalid transition: {fromState} → Run");
    }
    
    public override ValidationResult ValidateMovement(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Should be on ground
        if (moveRequest.origin.y > MovementConstants.MAX_GROUND_HEIGHT)
        {
            return new ValidationResult(false, $"Run but Y={moveRequest.origin.y:F2} (not grounded)");
        }

        return ValidationHelpers.ValidateHorizontalMove(last, moveRequest, MovementConstants.RUN_SPEED_VELOCITY);
    }
}

public class JumpValidator : BaseMoveStateValidator
{
    public override ValidationResult CanEnterState(MoveStateType fromState, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Can jump from grounded states or continue from Fall (changing direction upward)
        if (ValidationHelpers.IsGrounded(fromState))
        {
            // Validate jump impulse
            float vy = moveRequest.velocity.y;
            if (vy < MovementConstants.JUMP_IMPULSE - MovementConstants.JUMP_IMPULSE_TOLERANCE ||
                vy > MovementConstants.JUMP_IMPULSE + MovementConstants.JUMP_IMPULSE_TOLERANCE)
            {
                return new ValidationResult(false,
                    $"Jump impulse {vy:F2} not near expected {MovementConstants.JUMP_IMPULSE:F2}");
            }
            
            // Validate horizontal carry-over
            float currentHorizontal = ValidationHelpers.GetHorizontalSpeed(moveRequest.velocity);
            float lastHorizontal = ValidationHelpers.GetHorizontalSpeed(last.velocity);
            float maxAllowed = lastHorizontal + MovementConstants.JUMP_HORIZONTAL_BOOST;
            
            if (currentHorizontal > maxAllowed && false) // disable horizontal gain check for jump since some games allow it
            {
                return new ValidationResult(false,
                    $"Jump gained too much horizontal speed: {currentHorizontal:F2} m/s (max: {maxAllowed:F2} m/s)");
            }

            // We need to do a similar check for position to prevent jump teleporting - we can calculate the expected position based on the jump impulse and time since last update and ensure the new position is within a reasonable distance of that
            float timeSinceLast = (moveRequest.timestamp - last.timestamp) / 1000.0f / 1000.0f; // convert µs to seconds
            float expectedY = last.origin.y + MovementConstants.JUMP_IMPULSE * timeSinceLast + 0.5f * MovementConstants.GRAVITY * timeSinceLast * timeSinceLast;
            if (Math.Abs(moveRequest.origin.y - expectedY) > 0.5f) // allow some tolerance for player movement and network jitter
            {                return new ValidationResult(false,
                    $"Jump position Y={moveRequest.origin.y:F2} too far from expected Y={expectedY:F2} based on jump physics");
            }
            
            return new ValidationResult(true);
        }
        else if (fromState == MoveStateType.Fall)
        {
            // Can transition Fall → Jump if going upward (shouldn't normally happen but allow it)
            return new ValidationResult(false, "Jump from Fall not allowed in current implementation");
        }
        
        return new ValidationResult(false, $"Invalid transition: {fromState} → Jump");
    }
    
    public override ValidationResult ValidateMovement(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Must be going upward
        if (moveRequest.velocity.y <= 0)
        {
            return new ValidationResult(false, "Jump state but velocity is downward");
        }
        
        // Must be off ground
        if (moveRequest.origin.y == 0.0f)
        {
            return new ValidationResult(false, $"Jump but Y={moveRequest.origin.y:F2} (still on ground)");
        }
        
        // Horizontal speed limit
        float horizontalSpeed = ValidationHelpers.GetHorizontalSpeed(moveRequest.velocity);
        if (horizontalSpeed > MovementConstants.MAX_AIRBORNE_HORIZONTAL_SPEED)
        {
            return new ValidationResult(false,
                $"Jump horizontal speed {horizontalSpeed:F2} m/s exceeds max {MovementConstants.MAX_AIRBORNE_HORIZONTAL_SPEED:F2} m/s");
        }
        
        // Can't gain horizontal speed in air
        if (last.moveType == MoveStateType.Jump || last.moveType == MoveStateType.Fall)
        {
            float lastHorizontal = ValidationHelpers.GetHorizontalSpeed(last.velocity);
            float gainedSpeed = horizontalSpeed - lastHorizontal;
            
            if (gainedSpeed > MovementConstants.AIRBORNE_HORIZONTAL_GAIN_TOLERANCE && false) // disable horizontal gain check for jump since some games allow it
            {
                return new ValidationResult(false,
                    $"Jump gained {gainedSpeed:F2} m/s horizontal speed (can't accelerate in air)");
            }
        }
        
        return new ValidationResult(true);
    }
}

public class FallValidator : BaseMoveStateValidator
{
    public override ValidationResult CanEnterState(MoveStateType fromState, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Can fall from any airborne state or from grounded states (walked off edge)
        if (ValidationHelpers.IsAirborne(fromState) || ValidationHelpers.IsGrounded(fromState))
        {
            // If coming from grounded, validate we're actually leaving the ground
            if (ValidationHelpers.IsGrounded(fromState))
            {
                // Should be above ground level
                if (moveRequest.origin.y < 0.1f)
                {
                    return new ValidationResult(false,
                        $"Cannot fall from ground: Y={moveRequest.origin.y:F2} (still grounded)");
                }
                
                // Horizontal speed should be similar to what we had on ground
                float currentHorizontal = ValidationHelpers.GetHorizontalSpeed(moveRequest.velocity);
                float lastHorizontal = ValidationHelpers.GetHorizontalSpeed(last.velocity);
                float maxAllowed = lastHorizontal + MovementConstants.JUMP_HORIZONTAL_BOOST;
                
                if (currentHorizontal > maxAllowed && false) // disable horizontal gain check for now
                {
                    return new ValidationResult(false,
                        $"Fall gained too much horizontal speed: {currentHorizontal:F2} m/s (max: {maxAllowed:F2} m/s)");
                }
            }
            
            return new ValidationResult(true);
        }
        
        return new ValidationResult(false, $"Invalid transition: {fromState} → Fall");
    }
    
    public override ValidationResult ValidateMovement(PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Must be going downward
        if (moveRequest.velocity.y > 0)
        {
            return new ValidationResult(false, "Fall state but velocity is upward");
        }
        
        // Terminal velocity check
        if (Math.Abs(moveRequest.velocity.y) > MovementConstants.TERMINAL_VELOCITY)
        {
            return new ValidationResult(false,
                $"Fall velocity {moveRequest.velocity.y:F2} m/s exceeds terminal velocity {MovementConstants.TERMINAL_VELOCITY:F2} m/s");
        }
        
        // Horizontal speed limit
        float horizontalSpeed = ValidationHelpers.GetHorizontalSpeed(moveRequest.velocity);
        if (horizontalSpeed > MovementConstants.MAX_AIRBORNE_HORIZONTAL_SPEED)
        {
            return new ValidationResult(false,
                $"Fall horizontal speed {horizontalSpeed:F2} m/s exceeds max {MovementConstants.MAX_AIRBORNE_HORIZONTAL_SPEED:F2} m/s");
        }
        
        // Can't gain horizontal speed in air
        if (last.moveType == MoveStateType.Jump || last.moveType == MoveStateType.Fall)
        {
            float lastHorizontal = ValidationHelpers.GetHorizontalSpeed(last.velocity);
            float gainedSpeed = horizontalSpeed - lastHorizontal;
            
            if (gainedSpeed > MovementConstants.AIRBORNE_HORIZONTAL_GAIN_TOLERANCE && false) // disable horizontal gain check for fall since some games allow it
            {
                return new ValidationResult(false,
                    $"Fall gained {gainedSpeed:F2} m/s horizontal speed (can't accelerate in air)");
            }
        }
        
        return new ValidationResult(true);
    }
}

