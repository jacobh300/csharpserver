
using System.Diagnostics;
using System.Numerics;
using SpacetimeDB;

/// <summary>
/// Handles main game logic and state, mainly the tick loop.
/// </summary>
public class Game
{
    /// <summary>
    /// Main game tick function, called periodically to update game state.
    /// </summary>    
    public static void tick(ReducerContext ctx, Module.GameTickSchedule gameTick)
    {
        Timestamp now = ctx.Timestamp;
        float realDelta = (now.MicrosecondsSinceUnixEpoch - gameTick.lastExecuted.MicrosecondsSinceUnixEpoch) / 1_000_000.0f;
        gameTick.lastExecuted = now; 

        gameTick.tick++;
        ctx.Db.game_tick_schedule.id.Update(gameTick);
    }
}