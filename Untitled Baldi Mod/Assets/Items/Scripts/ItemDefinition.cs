using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public int itemID;
    public string itemName = "Unknown Item";
    public string pickupName; // matched against the pickup GameObject's name in the scene

    [Header("Inventory")]
    [Tooltip("If true, picking this up adds it to an item slot. If false, it is consumed immediately on pickup via the onPickup callback.")]
    public bool addToInventory = true;

    [Tooltip("If true, the pickup GameObject will NOT be hidden when picked up. Leave false unless you have a specific reason to keep it visible.")]
    public bool stayOnPickup = false;

    [Header("Visuals")]
    public Texture icon;

    [Header("Audio")]
    public AudioClip useSound; // optional per-item sound on use
}