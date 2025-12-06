
using SpacetimeDB;


/// <summary>
/// Handles main game logic and state, mainly the tick loop.
/// </summary>
public class Game
{
    const float MOVE_SPEED = 1.0f;
    /// <summary>
    /// Main game tick function, called periodically to update game state.
    /// </summary>    
    public static void tick(ReducerContext ctx)
    {
        //Log delta time sinfce last tick
        

        

        // Go through each player transform row and update positions based on velocity.
        foreach (var transform in ctx.Db.player_transform.Iter())
        {
            transform.position.x += transform.velocity.x * MOVE_SPEED;
            transform.position.y += transform.velocity.y * MOVE_SPEED;
            transform.position.z += transform.velocity.z * MOVE_SPEED;
            transform.timestamp = ctx.Timestamp;   
            ctx.Db.player_transform.player.Update(transform);
        }
    }
}