
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
        Timestamp now = ctx.Timestamp;
        // For each player, get all their player inputs and update their positions accordingly.
        foreach (var transform in ctx.Db.player_transform.Iter())
        {

            //transform.velocity = new DbVector3(0,0,0);
            IEnumerable<Module.PlayerInputRow> inputRows = ctx.Db.player_input.playerIndex.Filter(transform.player);
            foreach (var inputRow in inputRows)
            {
                //Update player position based on input
                transform.position.x += inputRow.input.x * transform.moveSpeed;
                transform.position.z += inputRow.input.y * transform.moveSpeed;
                transform.velocity.y = inputRow.jump ? 5.0f : transform.velocity.y;



                //transform.velocity = new DbVector3(inputRow.input.x * transform.moveSpeed, 0, inputRow.input.y * transform.moveSpeed);
                transform.yaw = inputRow.yaw;
                transform.tick = gameTick.tick; 
                transform.sequence = inputRow.sequence;
                transform.timestamp = now;
                //Remove processed input
                ctx.Db.player_input.id.Delete(inputRow.id);
            }

            if(transform.velocity.y != 0)
            {
                //Apply gravity
                transform.velocity.y -= 0.98f;
                transform.position.y += transform.velocity.y;
                if(transform.position.y < 0)
                {
                    transform.position.y = 0;
                    transform.velocity.y = 0;
                }
            }   

            ctx.Db.player_transform.player.Update(transform);
        }

        // Update the game tick schedule
        gameTick.lastExecuted = now;
        gameTick.tick++;
        ctx.Db.game_tick_schedule.id.Update(gameTick);
    }

}