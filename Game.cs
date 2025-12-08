
using System.Numerics;
using SpacetimeDB;


/// <summary>
/// Handles main game logic and state, mainly the tick loop.
/// </summary>
public class Game
{
    const float MOVE_SPEED = 1.0f;
    const float TICK_RATE = 1.0f /30.0f;
    /// <summary>
    /// Main game tick function, called periodically to update game state.
    /// </summary>    
    public static void tick(ReducerContext ctx, Module.GameTickSchedule gameTick)
    {
        //Update the scheduled tick time
        Timestamp lastTick = gameTick.last_tick;
        gameTick.last_tick = ctx.Timestamp;
        ctx.Db.game_tick_schedule.id.Update(gameTick);

        // Go through each player transform row and update positions based on velocity.
        foreach (var transform in ctx.Db.player_transform.Iter())
        {
            Module.PlayerInputRow? inputRow = ctx.Db.player_input.player.Find(transform.player);
            if (inputRow != null)
            {
                transform.position.x += inputRow.input.x * MOVE_SPEED;
                transform.position.y += 0 * MOVE_SPEED;
                transform.position.z += inputRow.input.y * MOVE_SPEED;
            }

            transform.timestamp = ctx.Timestamp;   
            ctx.Db.player_transform.player.Update(transform);
        }

        foreach( var entity in ctx.Db.entity_transform.Iter())
        {
            Module.EntityTransformRow eTransform = entity;
            long differenceInMicroSeconds = ctx.Timestamp.MicrosecondsSinceUnixEpoch - lastTick.MicrosecondsSinceUnixEpoch;
            Vector3 oldPosition = new Vector3(eTransform.position.x, eTransform.position.y, eTransform.position.z);
            
            
            double deltaTime = differenceInMicroSeconds / 1_000_000.0; // Convert microseconds to seconds
            Log.Info($"Entity ID: {eTransform.id} Delta Time: {deltaTime}");

            //Multiply speed by delta time to make movement framerate independent
            eTransform.position.x += eTransform.velocity.x * MOVE_SPEED * (float)deltaTime;
            eTransform.position.y += eTransform.velocity.y * MOVE_SPEED * (float)deltaTime;
            eTransform.position.z += eTransform.velocity.z * MOVE_SPEED * (float)deltaTime;
            Vector3 newPosition = new Vector3(eTransform.position.x, eTransform.position.y, eTransform.position.z);
            Vector3 delta = newPosition - oldPosition;
            float distance = delta.Length();
            float alpha = distance / (float)deltaTime;
            Log.Info($"Entity ID: {eTransform.id} Moved Distance: {distance} at Speed: {alpha} units/second");


            ctx.Db.entity_transform.id.Update(eTransform);
        }
    }
}