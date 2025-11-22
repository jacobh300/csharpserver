
using SpacetimeDB;


/// <summary>
/// Handles main game logic and state, mainly the tick loop.
/// </summary>
public class Game
{

    /// <summary>
    /// Main game tick function, called periodically to update game state.
    /// </summary>    
    public static void tick(ReducerContext ctx)
    {
        // Go through each player transform row and update positions based on velocity.
        foreach (var transform in ctx.Db.player_transform.Iter())
        {
            transform.position.x += transform.velocity.x;
            transform.position.y += transform.velocity.y;
            transform.position.z += transform.velocity.z;
            transform.timestamp = ctx.Timestamp;   
            ctx.Db.player_transform.player.Update(transform);
        }
    }
}