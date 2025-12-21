
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
            IEnumerable<Module.PlayerInputRow> inputRows = ctx.Db.player_input.playerIndex.Filter(transform.player);
            foreach (var inputRow in inputRows)
            {
                //Update player position based on input
                transform.position.x += inputRow.input.x * transform.moveSpeed;
                transform.position.y += 0;
                transform.position.z += inputRow.input.y * transform.moveSpeed;
                transform.tick = gameTick.tick; 
                transform.sequence = inputRow.sequence;
                transform.timestamp = now;
                ctx.Db.player_transform.player.Update(transform);
                //Remove processed input
                ctx.Db.player_input.id.Delete(inputRow.id);
            }
        }
    }

}