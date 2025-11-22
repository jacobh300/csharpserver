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