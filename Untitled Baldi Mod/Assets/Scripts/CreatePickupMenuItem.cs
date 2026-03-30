// Place this in Assets/Editor/CreatePickupMenuItem.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class CreatePickupMenuItem
{
    [MenuItem("GameObject/Create Pickup", false, 0)]
    static void CreatePickup(MenuCommand menuCommand)
    {
        // ── Root: Pickup_ ─────────────────────────────────────────────────────
        GameObject root = new GameObject("Pickup_");

        // Cylinder mesh filter + renderer
        MeshFilter mf = root.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        MeshRenderer mr = root.AddComponent<MeshRenderer>();
        mr.enabled = false; // invisible by default, just like existing pickups

        // Capsule collider
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.center    = new Vector3(0f, 1f, 0f);
        col.radius    = 1.5f;
        col.height    = 2f;
        col.direction = 1; // Y-Axis

        // ── Child: ItemSprite ─────────────────────────────────────────────────
        GameObject sprite = new GameObject("ItemSprite");
        sprite.transform.SetParent(root.transform, false);

        sprite.AddComponent<SpriteRenderer>(); // empty, assign sprite in Inspector
        sprite.AddComponent<Billboard>();
        sprite.AddComponent<PickupAnimationScript>();

        // ── Finalize ──────────────────────────────────────────────────────────
        // Parent to whatever is selected in the hierarchy (or scene root if nothing)
        GameObject context = menuCommand.context as GameObject;
        if (context != null)
            root.transform.SetParent(context.transform, false);

        GameObjectUtility.SetParentAndAlign(root, context);
        Undo.RegisterCreatedObjectUndo(root, "Create Pickup");
        Selection.activeGameObject = root;
    }
}
#endif
