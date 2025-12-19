
using System.Diagnostics;
using System.Numerics;
using SpacetimeDB;


/// <summary>
/// Handles main game logic and state, mainly the tick loop.
/// </summary>
public class Game
{
    const float MOVE_SPEED = 4.0f;
    private static bool _debugLog = false;
    /// <summary>
    /// Main game tick function, called periodically to update game state.
    /// </summary>    
    public static void tick(ReducerContext ctx, Module.GameTickSchedule gameTick)
    {
        // For each player, get all their player inputs and update their positions accordingly.
        foreach (var transform in ctx.Db.player_transform.Iter())
        {
            IEnumerable<Module.PlayerInputRow> inputRows = ctx.Db.player_input.playerIndex.Filter(transform.player);
            foreach (var inputRow in inputRows)
            {
                //Update player position based on input
                transform.position.x += inputRow.input.x * MOVE_SPEED;
                transform.position.y += 0 * MOVE_SPEED;
                transform.position.z += inputRow.input.y * MOVE_SPEED;
                transform.tick = gameTick.tick; 
                transform.sequence = inputRow.sequence;
                ctx.Db.player_transform.player.Update(transform);
                //Remove processed input
                ctx.Db.player_input.id.Delete(inputRow.id);
            }
        }
    }

    private static float getSpeed(Vector3 oldPosition, Vector3 newPosition, double deltaTime)
    {
        Vector3 delta = newPosition - oldPosition;
        float distance = delta.Length();
        return distance / (float)deltaTime;
    }



    /** OLD CODE
        public static void tick(ReducerContext ctx, Module.GameTickSchedule gameTick)
    {
        // Go through each player transform row and update positions based on velocity.
        foreach (var transform in ctx.Db.player_transform.Iter())
        {
            Module.PlayerInputRow? inputRow = ctx.Db.player_input.player.Find(transform.player);
            //Normalize input to prevent faster diagonal movement

            if (inputRow != null && (inputRow.input.x != 0 || inputRow.input.y != 0))
            {
                transform.position.x += inputRow.input.x * MOVE_SPEED;
                transform.position.y += 0 * MOVE_SPEED;
                transform.position.z += inputRow.input.y * MOVE_SPEED;
                transform.tick = gameTick.tick; 
                ctx.Db.player_transform.player.Update(transform);

                inputRow.input = new DbVector2 { x = 0, y = 0 };
                ctx.Db.player_input.player.Update(inputRow);      
            }


        }

        foreach(var entity in ctx.Db.entity_transform.Iter())
        {
            //long differenceInMicroSeconds = ctx.Timestamp.MicrosecondsSinceUnixEpoch - lastTick.MicrosecondsSinceUnixEpoch;
           // double deltaTime = differenceInMicroSeconds / 1_000_000.0; // Convert microseconds to seconds

            Module.EntityTransformRow eTransform = entity;

            Vector3 oldPosition = new Vector3(eTransform.position.x, eTransform.position.y, eTransform.position.z);

            //Log.Info($"Entity ID: {eTransform.id} Delta Time: {deltaTime}");

            //Multiply speed by delta time to make movement framerate independent
            eTransform.position.x += eTransform.velocity.x * MOVE_SPEED;
            eTransform.position.y += eTransform.velocity.y * MOVE_SPEED;
            eTransform.position.z += eTransform.velocity.z * MOVE_SPEED;

            if(_debugLog == true)
            {
                Vector3 newPosition = new Vector3(eTransform.position.x, eTransform.position.y, eTransform.position.z);
                //float speed = getSpeed(oldPosition, newPosition, deltaTime);
                //Log.Info($"Entity ID: {eTransform.id} Delta Time: {deltaTime} Speed: {speed} units/second");
            }


            ctx.Db.entity_transform.id.Update(eTransform);
        }
        
        //Check if the current time is greater than a second since last tick
        if(ctx.Timestamp.MicrosecondsSinceUnixEpoch - gameTick.lastExecuted.MicrosecondsSinceUnixEpoch >= 1_000_000)
        {
            //Update last executed time
            gameTick.lastExecuted = ctx.Timestamp;
            gameTick.lastSecondTick = gameTick.tick;
            Log.Info("Amount of ticks in the last second: " + (gameTick.tick - gameTick.lastTick)); 
        }

        //Update the scheduled tick time
        gameTick.tick += 1;
        ctx.Db.game_tick_schedule.id.Update(gameTick);
    }
    */
}