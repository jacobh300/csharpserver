using SpacetimeDB;

public class ItemService
{
    private ReducerContext _ctx;

    public ItemService(ReducerContext ctx)
    {
        _ctx = ctx;
    }



    public Module.ItemRow? GiveItemToUser(UserService user, ItemTypeIds itemId, int quantity)
    {
        var itemType = GetItemTypeById(itemId);
        if (itemType == null)
        {
            return null; // Item type does not exist
        }

        return addOrUpdateItem(user, itemId, quantity);
    }

    private Module.ItemType? GetItemTypeById(ItemTypeIds itemId)
    {
        return _ctx.Db.item_types.id.Find((int)itemId);
    }


    private Module.ItemRow addOrUpdateItem(UserService user, ItemTypeIds itemId, int quantity)
    {
        // Use ItemRows property to get current items
        int id = (int)itemId;
        user.ItemRows.TryGetValue(id, out var existingItem);

        if (existingItem == null)
        {

            Module.ItemRow itemAdded = new Module.ItemRow
            {
                owner = _ctx.Sender,
                item_type_id = id,
                quantity = quantity
            };

            _ctx.Db.item.Insert(itemAdded);
            return itemAdded;

        }
        else
        {
            existingItem.quantity += quantity;

            _ctx.Db.item.id.Update(existingItem);
            return existingItem;
        }
    }

    public enum ItemTypeIds
    {
        Coin = 1,
        Copper = 2,
        Iron = 3,
        Gold = 4,
        Diamond = 5
    }
}