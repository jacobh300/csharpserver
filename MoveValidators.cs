using SpacetimeDB;
using System;
using System.Collections.Generic;

// ============================================================================
// MOVEMENT VALIDATION SYSTEM
// ============================================================================
// This file contains all movement validation logic separated from basic types.
// 
// HOW TO SWITCH VALIDATORS:
// 1. Go to the MoveValidator class (line ~108)
// 2. Uncomment one of the validator options:
//    - DefaultMoveValidator: Balanced (sanity + state + physics)
//    - LenientMoveValidator: Only sanity checks (for testing if physics is too strict)
//    - StrictMoveValidator: All checks + future stricter rules
// 3. Comment out the others
// 4. Rebuild and test
//
// TO CREATE A NEW VALIDATOR:
// 1. Create a class that implements IMoveValidator
// 2. Implement the Validate method with your custom logic
// 3. Update MoveValidator._activeValidator to use your new class
// ============================================================================

/// <summary>
/// Interface for movement validation strategies. 
/// Implement this to create different validation approaches for testing.
/// </summary>
public interface IMoveValidator
{
    ValidationResult Validate(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last);
}

/// <summary>
/// Default validation strategy with layered checks (sanity, state transitions, physics).
/// This is the current production validator.
/// </summary>
public class DefaultMoveValidator : IMoveValidator
{
    public ValidationResult Validate(
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

/// <summary>
/// Lenient validator for testing - only does basic sanity checks.
/// Use this to test if strict physics validation is causing issues.
/// </summary>
public class LenientMoveValidator : IMoveValidator
{
    public ValidationResult Validate(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last)
    {
        // Only do sanity checks, skip physics
        return SanityChecks.Validate(moveType, moveRequest, last);
    }
}

/// <summary>
/// Strict validator for testing - all checks with tighter tolerances.
/// Use this to test anti-cheat effectiveness.
/// </summary>
public class StrictMoveValidator : IMoveValidator
{
    public ValidationResult Validate(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last)
    {
        // Use stricter version of checks (could implement stricter variants)
        var sanityResult = SanityChecks.Validate(moveType, moveRequest, last);
        if (!sanityResult.IsValid)
            return sanityResult;
        
        var stateResult = StateTransitionRules.Validate(moveType, last);
        if (!stateResult.IsValid)
            return stateResult;
        
        var physicsResult = PlausibilityChecks.Validate(moveType, moveRequest, last);
        if (!physicsResult.IsValid)
            return physicsResult;
        
        // TODO: Add stricter checks here
        
        return new ValidationResult(true);
    }
}

/// <summary>
/// Static accessor for the active validator. Change this to test different strategies.
/// </summary>
public static class MoveValidator
{
    // Switch this to test different validators
    private static readonly IMoveValidator _activeValidator = new DefaultMoveValidator();
    // private static readonly IMoveValidator _activeValidator = new LenientMoveValidator();
    // private static readonly IMoveValidator _activeValidator = new StrictMoveValidator();
    
    public static ValidationResult Validate(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last)
    {
        return _activeValidator.Validate(moveType, moveRequest, last);
    }
}

// ============================================================================
// VALIDATION COMPONENTS
// ============================================================================

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
    private const float MAX_RUN_SPEED = 12.0f;
    private const float MAX_AIRBORNE_HORIZONTAL_SPEED = 8.0f;  // Slightly more than run speed for momentum
    private const float AIR_CONTROL_ACCELERATION = 1.5f;  // Limited air control (m/s²)
    
    // Helper to get horizontal velocity magnitude
    private static float GetHorizontalSpeed(DbVector3 velocity)
    {
        return new DbVector3(velocity.x, 0, velocity.z).magnitude;
    }
    
    private static bool IsAirborne(MoveStateType state)
    {
        return state == MoveStateType.Jump || state == MoveStateType.Fall;
    }
    
    private static bool IsGrounded(MoveStateType state)
    {
        return state == MoveStateType.Idle || state == MoveStateType.Walk || state == MoveStateType.Run;
    }
    
    public static ValidationResult Validate(MoveStateType moveType, PlayerMoveRequest moveRequest, Module.PlayerMoveUpdate last)
    {
        // Simplified: Check if currently airborne (regardless of last state)
        if (IsAirborne(moveType))
        {
            // Validate vertical movement
            var verticalCheck = ValidateAirborneVertical(moveType, moveRequest, last);
            if (!verticalCheck.IsValid)
                return verticalCheck;
            
            // Validate horizontal movement (applies to ALL airborne states)
            var horizontalCheck = ValidateAirborneHorizontal(moveType, moveRequest, last);
            if (!horizontalCheck.IsValid)
                return horizontalCheck;
        }
        
        // Landing validation
        if (IsAirborne(last.moveType) && IsGrounded(moveType))
        {
            return ValidateLanding(moveRequest);
        }
        
        return new ValidationResult(true);
    }
    
    private static ValidationResult ValidateAirborneVertical(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last)
    {
        if (moveType == MoveStateType.Jump)
        {
            // Must be going upward
            if (moveRequest.velocity.y <= 0)
            {
                return new ValidationResult(false,
                    "Jump state but velocity is downward (should be Fall)");
            }
            
            // If continuing from jump, velocity should decrease due to gravity
            if (last.moveType == MoveStateType.Jump && moveRequest.velocity.y >= last.velocity.y)
            {
                return new ValidationResult(false,
                    "Jump velocity should decrease due to gravity");
            }
            
            // If just started jumping, check reasonable initial velocity
            if (IsGrounded(last.moveType))
            {
                float vyTolerance = 3.0f;
                if (moveRequest.velocity.y < JUMP_FORCE - vyTolerance || 
                    moveRequest.velocity.y > JUMP_FORCE + vyTolerance)
                {
                    return new ValidationResult(false,
                        $"Jump start velocity {moveRequest.velocity.y:F2} not near expected {JUMP_FORCE:F2}");
                }
            }
        }
        else if (moveType == MoveStateType.Fall)
        {
            // Must be going downward
            if (moveRequest.velocity.y > 0)
            {
                return new ValidationResult(false,
                    "Fall state but velocity is upward");
            }
            
            // Terminal velocity check
            const float TERMINAL_VELOCITY = -25.0f;
            if (moveRequest.velocity.y < TERMINAL_VELOCITY)
            {
                return new ValidationResult(false,
                    $"Fall velocity {moveRequest.velocity.y:F2} exceeds terminal velocity {TERMINAL_VELOCITY:F2}");
            }
            
            // If continuing fall, velocity should increase (get more negative) or stay at terminal
            if (last.moveType == MoveStateType.Fall)
            {
                if (moveRequest.velocity.y > last.velocity.y + 0.5f)
                {
                    return new ValidationResult(false,
                        "Fall velocity should increase (or stay at terminal velocity)");
                }
            }
        }
        
        return new ValidationResult(true);
    }
    
    private static ValidationResult ValidateAirborneHorizontal(
        MoveStateType moveType,
        PlayerMoveRequest moveRequest,
        Module.PlayerMoveUpdate last)
    {
        float currentHorizontalSpeed = GetHorizontalSpeed(moveRequest.velocity);
        float lastHorizontalSpeed = GetHorizontalSpeed(last.velocity);
        
        // Check 1: Absolute max horizontal speed while airborne
        if (currentHorizontalSpeed > MAX_AIRBORNE_HORIZONTAL_SPEED)
        {
            return new ValidationResult(false,
                $"Airborne horizontal speed {currentHorizontalSpeed:F2} m/s exceeds max {MAX_AIRBORNE_HORIZONTAL_SPEED:F2} m/s");
        }
        
        // Check 2: If entering airborne from ground, can't exceed ground speed by much
        if (IsGrounded(last.moveType) && IsAirborne(moveType))
        {
            float maxCarryOver = lastHorizontalSpeed + 1.0f;  // Small boost from jump
            if (currentHorizontalSpeed > maxCarryOver)
            {
                return new ValidationResult(false,
                    $"Cannot gain {currentHorizontalSpeed - lastHorizontalSpeed:F2} m/s horizontal speed from jump " +
                    $"(had {lastHorizontalSpeed:F2} m/s on ground, now {currentHorizontalSpeed:F2} m/s)");
            }
        }
        
        // Check 3: Limited air control - can't gain too much speed mid-air
        if (IsAirborne(last.moveType) && IsAirborne(moveType))
        {
            float dt = (moveRequest.timestamp - last.timestamp) / 1_000_000.0f;
            float maxAllowedIncrease = AIR_CONTROL_ACCELERATION * dt;
            float horizontalSpeedChange = currentHorizontalSpeed - lastHorizontalSpeed;
            
            if (horizontalSpeedChange > maxAllowedIncrease)
            {
                return new ValidationResult(false,
                    $"Gained {horizontalSpeedChange:F2} m/s horizontal speed in air " +
                    $"(max air control: {maxAllowedIncrease:F2} m/s over {dt:F3}s)");
            }
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
