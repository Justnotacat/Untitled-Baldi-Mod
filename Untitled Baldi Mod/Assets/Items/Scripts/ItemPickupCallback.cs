using UnityEngine;

/// <summary>
/// Attach a subclass of this to a pickup GameObject to run custom logic when it is picked up.
/// Only called when addToInventory is false on the ItemDefinition, OR in addition to inventory
/// collection if you want side-effects on any pickup.
///
/// Example:
///
///     public class NotebookPickup : ItemPickupCallback
///     {
///         public override void OnPickup(GameControllerScript gc)
///         {
///             gc.CollectNotebook();
///         }
///     }
/// </summary>
public abstract class ItemPickupCallback : MonoBehaviour
{
    /// <summary>Called by PickupScript when the player picks up this item.</summary>
    public abstract void OnPickup(GameControllerScript gc);
}
