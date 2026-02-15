using SpacetimeDB;
[SpacetimeDB.Type]
public partial struct DbVector3
{
    public float x;
    public float y;
    public float z;

    public DbVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }


    public static DbVector3 operator +(DbVector3 a, DbVector3 b)
    {
        return new DbVector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static DbVector3 operator -(DbVector3 a, DbVector3 b)
    {
        return new DbVector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public static DbVector3 operator *(DbVector3 v, float scalar)
    {
        return new DbVector3(v.x * scalar, v.y * scalar, v.z * scalar);
    }

    public static DbVector3 operator *(float scalar, DbVector3 v)
    {
        return new DbVector3(v.x * scalar, v.y * scalar, v.z * scalar);
    }

    public static DbVector3 operator /(DbVector3 v, float scalar)
    {
        return new DbVector3(v.x / scalar, v.y / scalar, v.z / scalar);
    }

    public float magnitude => (float)System.Math.Sqrt(x * x + y * y + z * z);

    public static DbVector3 normalize(DbVector3 v)
    {
        float length = (float)System.Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        if (length > 0)
        {
            return new DbVector3(v.x / length, v.y / length, v.z / length);
        }
        return new DbVector3(0, 0, 0);
    }

    public static float Distance(DbVector3 a, DbVector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        float dz = a.z - b.z;
        return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }


}
[SpacetimeDB.Type]
public partial struct DbVector2
{
    public float x;
    public float y;

    public DbVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public static DbVector2 normalize(DbVector2 v)
    {
        float length = (float)System.Math.Sqrt(v.x * v.x + v.y * v.y);
        if (length > 0)
        {
            return new DbVector2(v.x / length, v.y / length);
        }
        return new DbVector2(0, 0);
    }

}

[SpacetimeDB.Type]
public partial struct PlayerMoveRequest
{
    public DbVector3 origin;
    public DbVector3 destination;
    public DbVector3 velocity;
    public long timestamp;
    public float duration;
}

/// <summary>
/// Enum representing different types of player movement states.  
/// </summary>  
/// 
[SpacetimeDB.Type]
public enum MoveStateType
{
    Idle,
    Walk,
    Run,
    Jump,
    Fall
}

/// <summary>
/// Result of a validation check.
/// </summary>
public class ValidationResult
{
    public bool IsValid;
    public string ErrorMessage;

    public ValidationResult(bool isValid, string errorMessage = "")
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
}