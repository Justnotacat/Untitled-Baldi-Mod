using UnityEngine;

[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Items/Item Registry")]
public class ItemRegistry : ScriptableObject
{
    public ItemDefinition[] items;

    /// <summary>Returns the ItemDefinition matching the given ID, or null if not found.</summary>
    public ItemDefinition GetByID(int id)
    {
        foreach (var item in items)
            if (item != null && item.itemID == id)
                return item;
        return null;
    }

    /// <summary>Returns the ItemDefinition whose pickupName matches the given GameObject name, or null.</summary>
    public ItemDefinition GetByPickupName(string pickupName)
    {
        foreach (var item in items)
            if (item != null && item.pickupName == pickupName)
                return item;
        return null;
    }
}
