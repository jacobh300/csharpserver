using SpacetimeDB;

public class CompanionService
{
    private ReducerContext _ctx;
    public CompanionService(ReducerContext ctx)
    {
        _ctx = ctx;
    }


    public Module.CompanionRow? MoveCompanion(int companionId, DbVector2 newLocation)
    {
        //Fetch the companion
        Module.CompanionRow? companionRow = _ctx.Db.companion.id.Find(companionId);
        if(companionRow == null)
        {
            return null;
        }

        //Update the location
        companionRow.position = newLocation;
        _ctx.Db.companion.id.Update(companionRow);
        return companionRow;
    }

    public Module.CompanionRow? CreateRandomCompanionForUser(UserContext user)
    {
        //Create a new companion with random attributes
        Module.CompanionRow newCompanion = new Module.CompanionRow
        {
            owner = user.Id,
            name = "Companion_" + Guid.NewGuid().ToString().Substring(0, 8),
            position = new DbVector2(0, 0) // Default starting position
        };

        _ctx.Db.companion.Insert(newCompanion);
        return newCompanion;
    }

}