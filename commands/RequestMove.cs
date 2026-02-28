using System.Numerics;
using System.Reflection.Metadata;
using SpacetimeDB;

public partial class RequestMove : ReducerCommand
{
    // Used when validating move requests.
    private const float MAX_MOVE_SPEED = 1.0f; // units per second
    private const float MAX_JUMP_HEIGHT = 5.0f; // max jump height in units
    private Timestamp _timestamp;
    private DbVector3 _requestedOrigin;
    private DbVector3 _requestedDestination;
    private float _duration; 
    private bool _relativeMove;


    /// <summary>
    /// The idea is that the client will periodically send position updates to the server,
    /// and the server will use these updates to validate the client's position, movement, and how long the movement took.
    /// We will validate the timestamp to make sure the client is not sending old or future timestamps?
    /// We will validate the position to make sure the client is not teleporting around the map or moving faster than they should be.
    /// We will validate the duration to make sure the client is not sending move requests that are too fast or too slow.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="timestamp"></param>
    /// <param name="newPosition"></param>
    [SpacetimeDB.Reducer]
    public static void reducer_sendPositionUpdate(ReducerContext ctx, Timestamp timestamp, DbVector3 newPosition, float duration)
    {

    }



    [SpacetimeDB.Reducer]
    public static void reducer_RequestMove(ReducerContext ctx, Timestamp timestamp, DbVector3 requestedOrigin, DbVector3 requestedDestination, float duration)
    {
        RequestMove commandInstance = new RequestMove(ctx, timestamp, requestedOrigin, requestedDestination, duration);
        commandInstance.run();
    }

    [SpacetimeDB.Reducer]
    public static void reducer_SimpleRequestMove(ReducerContext ctx)
    {
        //Create standard move request for testing
        Timestamp timestamp = ctx.Timestamp;
        DbVector3 requestedOrigin = new DbVector3(1, 0, 0);
        DbVector3 requestedDestination = new DbVector3(1, 0, 0);
        float duration = 1.0f;

        RequestMove commandInstance = new RequestMove(ctx, timestamp, requestedOrigin, requestedDestination, duration, true);
        commandInstance.run();
    }

    [SpacetimeDB.Reducer]
    public static void reducer_TimestampValidationCheck(ReducerContext ctx)
    {
        Timestamp timestamp = ctx.Timestamp;
        for(int i = 0; i < 5; i++)
        {
            //Create standard move request for testing
            timestamp += TimeSpan.FromSeconds(i);
            DbVector3 requestedOrigin = new DbVector3(1, 0, 0);
            DbVector3 requestedDestination = new DbVector3(2, 0, 0);
            float duration = 1.0f;

            RequestMove commandInstance = new RequestMove(ctx, timestamp, requestedOrigin, requestedDestination, duration, true);
            commandInstance.run();
        }
    }

    [SpacetimeDB.Reducer]
    public static void reducer_timeCompressedMoveCheck(ReducerContext ctx)
    {
        Timestamp timestamp = ctx.Timestamp;
        for(int i = 0; i < 5; i++)
        {
            //Create standard move request for testing
            timestamp += TimeSpan.FromMilliseconds(1);
            DbVector3 requestedOrigin = new DbVector3(1, 0, 0);
            DbVector3 requestedDestination = new DbVector3(2, 0, 0);
            float duration = 1.0f;

            RequestMove commandInstance = new RequestMove(ctx, timestamp, requestedOrigin, requestedDestination, duration, true);
            commandInstance.run();
        }
    }

    public RequestMove(ReducerContext ctx, Timestamp timestamp, DbVector3 requestedOrigin, DbVector3 requestedDestination, float duration, bool relativeMove = false) : base(ctx)
    {
        _timestamp = timestamp;
        _requestedOrigin = requestedOrigin;
        _requestedDestination = requestedDestination;
        _duration = duration;
        _relativeMove = relativeMove;


        Module.PlayerTransformRow? playerTransform = _ctx.Db.PlayerTransformRow.player.Find(_user.Id);
        if(playerTransform != null )
        {
            if( _relativeMove)
            {
                _requestedDestination = playerTransform.position + requestedDestination;
                _requestedOrigin += playerTransform.position;
            }
        }  

    }

    private bool noMovement(Vector3 from, Vector3 to)
    {
        return Vector3.Distance(from, to) < 0.01f;
    }


    private bool validateTimestamp(Timestamp lastTimestamp, Timestamp currentTimestamp)
    {
        int MAX_OFFSET_IN_PAST = 5000;
        int MAX_OFFSET_IN_FUTURE = 1000;

        Timestamp serverNow = _ctx.Timestamp;

        if (currentTimestamp.MicrosecondsSinceUnixEpoch > serverNow.MicrosecondsSinceUnixEpoch + MAX_OFFSET_IN_FUTURE * 1000)
        {
            Log.Exception($"Timestamp validation failed: currentTimestamp {currentTimestamp.MicrosecondsSinceUnixEpoch} is too far in the future compared to server time {serverNow.MicrosecondsSinceUnixEpoch}");
            return false;
        }

        if (currentTimestamp.MicrosecondsSinceUnixEpoch < serverNow.MicrosecondsSinceUnixEpoch - MAX_OFFSET_IN_PAST * 1000)
        {
            Log.Exception($"Timestamp validation failed: currentTimestamp {currentTimestamp.MicrosecondsSinceUnixEpoch} is too far in the past compared to server time {serverNow.MicrosecondsSinceUnixEpoch}");
            return false;
        }

        if (currentTimestamp.MicrosecondsSinceUnixEpoch <= lastTimestamp.MicrosecondsSinceUnixEpoch)
        {
            Log.Exception($"Timestamp validation failed: currentTimestamp {currentTimestamp.MicrosecondsSinceUnixEpoch} <= lastTimestamp {lastTimestamp.MicrosecondsSinceUnixEpoch}");
            return false;
        }
        return true;
    }


    private bool validateMoveBasic(Vector3 from, Vector3 to, float duration)
    {
        float MAX_DURATION = 100.0f;
        float distance = Vector3.Distance(from, to);
        float speed = distance / duration;

        if(duration > MAX_DURATION || duration < 0.0f)
        {
            return false;
        }

        if (speed > MAX_MOVE_SPEED)
        {
            return false;
        }

        return true;    
    }

    /// <summary>
    /// Validates the move based on distance and time constraints.
    /// Example for this check would be if a client is sending alot of move requests
    /// that would require them to move faster than the maximum allowed speed.
    /// </summary>
    /// <param name="previousOrigin"></param>
    /// <param name="newOrigin"></param>
    /// <param name="lastTimestamp"></param>
    /// <param name="currentTimestamp"></param>
    /// <returns></returns>
    private bool validateMoveOrigin(Vector3 previousOrigin, Vector3 newOrigin, Timestamp lastTimestamp, Timestamp currentTimestamp)
    {
        const float DURATION_LENIENCY_FLAT_VALUE = 0.05f;
        const float DURATION_LENIENCY_MULTIPLIER = 0.9f;

        float estimatedDuration = getTravelTime(previousOrigin, newOrigin);
        float timestampDiff = (currentTimestamp.MicrosecondsSinceUnixEpoch - lastTimestamp.MicrosecondsSinceUnixEpoch) / 1_000_000.0f; // convert to seconds
        if (timestampDiff < (estimatedDuration * DURATION_LENIENCY_MULTIPLIER) - DURATION_LENIENCY_FLAT_VALUE)
        {
            Log.Debug($"ValidateMoveOrigin: validation failed: timestampDiff {timestampDiff} < estimatedDuration {estimatedDuration} with leniency.");
            return false;
        }

        return true;
    }

    private float getTravelTime(Vector3 from, Vector3 to)
    {
        float distance = Vector3.Distance(from, to);
        return distance / MAX_MOVE_SPEED;
    }

    protected override void run()
    {
        Module.PlayerTransformRow? playerTransform = _ctx.Db.PlayerTransformRow.player.Find(_user.Id);
        if (playerTransform == null)
        {
            Log.Exception("Player transform not found for player: " + _user.Id);
            return;
        }
        Timestamp lastTimestamp = playerTransform.timestamp; 

        if (!validateTimestamp(lastTimestamp, _timestamp))
        {
            Log.Exception($"Player {_user.Id} move request denied. Invalid timestamp from {lastTimestamp} to {_timestamp}.");
            return;
        }

        // Validate move of the origin.
        Vector3 lastOrigin = new Vector3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z);
        Vector3 newOrigin = new Vector3(_requestedOrigin.x, _requestedOrigin.y, _requestedOrigin.z);
        if(!validateMoveOrigin(lastOrigin, newOrigin, lastTimestamp, _timestamp))
        {
            Log.Exception($"Player {_user.Id} move request denied. Invalid origin from {lastOrigin} to {_requestedOrigin}.");
            return;
        }

        // Do checks using the new origin.
        Vector3 to = new Vector3(_requestedDestination.x, _requestedDestination.y, _requestedDestination.z);
        if (!validateMoveBasic(newOrigin, to, _duration))
        {
            Log.Exception($"Player {_user.Id} move request denied. Move too fast from {newOrigin} to {to} in {_duration} seconds.");
            return;
        }

    

        // We are not moving the player to the destination directly, but updating their position to the new origin.
        // This pattern serves to validate that the player is where they say they are before the next move
        // And gives us a vector to determine velocity and direction for future moves.
        Log.Info("valid move request from player " + _user.Id + " old origin: " + lastOrigin + " new origin: " + newOrigin + " destination: " + to + " duration: " + _duration);
        playerTransform.position = new DbVector3(newOrigin.X, newOrigin.Y, newOrigin.Z);
        playerTransform.timestamp = _timestamp;
        _ctx.Db.PlayerTransformRow.player.Update(playerTransform); 
    }

}