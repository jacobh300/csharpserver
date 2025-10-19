using SpacetimeDB;


public abstract class ReducerCommand
{
    protected ReducerContext _ctx;

    public ReducerCommand(ReducerContext ctx)
    {
        _ctx = ctx;

    }

    protected void respond(string data)
    {
        ReducerResponse response = new ReducerResponse(_ctx, data);
        response.send();
    }

    /// <summary>
    /// Gives an item to the user who sent the command.
    /// Handles both adding new items and updating existing ones.
    /// </summary>
    /// <param name="itemName">Item name</param>
    /// <param name="quantity">Item Quantity</param>
    /// <returns> The updated ItemRow </returns>
    protected Module.ItemRow giveItem(string itemName, int quantity)
    {
        Module.ItemRow? itemAdded = null;
        foreach (var item in _ctx.Db.item.Iter())
        {
            if (item.owner == _ctx.Sender && item.name == itemName)
            {
                item.quantity += quantity;
                _ctx.Db.item.id.Update(item);
                itemAdded = item;
            }
        }

        if (itemAdded == null)
        {
            return _ctx.Db.item.Insert(
                new Module.ItemRow
                {
                    owner = _ctx.Sender,
                    name = itemName,
                    quantity = quantity
                }
            );
        }
        return itemAdded;
    }

    protected abstract void run();
}