using UnityEngine;

public class PickupScript : MonoBehaviour
{
    public GameControllerScript gc;
    public Transform player;
    public ItemRegistry itemRegistry;

    private void Update()
    {
        if (!Singleton<InputManager>.Instance.GetActionKeyDown(InputAction.Interact)
            || Time.timeScale == 0f) return;

        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // The raycast may hit a child object (e.g. ItemSprite), so walk up to the root
        // of the pickup by checking the hit object and all its parents for a matching definition.
        Transform check = hit.transform;
        ItemDefinition def = null;
        while (check != null)
        {
            def = itemRegistry.GetByPickupName(check.name);
            if (def != null) break;
            check = check.parent;
        }

        if (def == null) return;
        if (Vector3.Distance(player.position, check.position) >= 10f) return;

        // Hide the pickup object unless explicitly told to keep it
        if (!def.stayOnPickup)
            check.gameObject.SetActive(false);

        // Add to inventory if flagged
        if (def.addToInventory)
            gc.CollectItem(def.itemID);

        // Run any custom pickup logic attached to the pickup GameObject
        var callback = check.GetComponent<ItemPickupCallback>();
        if (callback != null)
            callback.OnPickup(gc);
    }
}