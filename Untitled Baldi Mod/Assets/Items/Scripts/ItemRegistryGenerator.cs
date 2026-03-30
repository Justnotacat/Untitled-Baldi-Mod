// Place this file in any folder named Editor/ inside your Assets folder.
// e.g. Assets/Editor/ItemRegistryGenerator.cs
//
// Usage: Unity menu bar -> Tools -> Generate Item Registry
// This will create all ItemDefinition assets and the ItemRegistry under Assets/Items/

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class ItemRegistryGenerator : Editor
{
    private const string OutputFolder = "Assets/Items";
    private const string DefsFolder   = "Assets/Items/Definitions";

    [MenuItem("Tools/Generate Item Registry")]
    public static void Generate()
    {
        // Make sure output folders exist
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets", "Items");
        if (!AssetDatabase.IsValidFolder(DefsFolder))
            AssetDatabase.CreateFolder(OutputFolder, "Definitions");

        // --- Define all items ---
        // Format: (id, displayName, pickupObjectName)
        var items = new (int id, string name, string pickup)[]
        {
            (1,  "Energy Flavored Zesty Bar",                  "Pickup_EnergyFlavoredZestyBar"),
            (2,  "Yellow Door Lock",                           "Pickup_YellowDoorLock"),
            (3,  "Principal's Keys",                           "Pickup_Key"),
            (4,  "BSODA",                                      "Pickup_BSODA"),
            (5,  "Quarter",                                    "Pickup_Quarter"),
            (6,  "Baldi Anti Hearing and Disorienting Tape",   "Pickup_Tape"),
            (7,  "Alarm Clock",                                "Pickup_AlarmClock"),
            (8,  "WD-NoSquee (Door Type)",                     "Pickup_WD-3D"),
            (9,  "Safety Scissors",                            "Pickup_SafetyScissors"),
            (10, "Big Ol' Boots",                              "Pickup_BigBoots"),
            (11, "Teleportation Teleporter",                   "Pickup_Teleporter"),
            (12, "Apple For Baldi",                            "Pickup_Apple"),
        };

        var definitions = new ItemDefinition[items.Length];

        for (int i = 0; i < items.Length; i++)
        {
            var (id, displayName, pickup) = items[i];
            string path = $"{DefsFolder}/Item_{id:D2}_{SanitizeFileName(displayName)}.asset";

            // Reuse existing asset if it already exists so we don't wipe icon assignments
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (existing != null)
            {
                // Only update fields that are safe to overwrite
                existing.itemID     = id;
                existing.itemName   = displayName;
                existing.pickupName = pickup;
                EditorUtility.SetDirty(existing);
                definitions[i] = existing;
                Debug.Log($"Updated existing: {path}");
            }
            else
            {
                var def = ScriptableObject.CreateInstance<ItemDefinition>();
                def.itemID     = id;
                def.itemName   = displayName;
                def.pickupName = pickup;
                AssetDatabase.CreateAsset(def, path);
                definitions[i] = def;
                Debug.Log($"Created: {path}");
            }
        }

        // --- Create or update the registry ---
        string registryPath = $"{OutputFolder}/ItemRegistry.asset";
        var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(registryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<ItemRegistry>();
            AssetDatabase.CreateAsset(registry, registryPath);
        }

        registry.items = definitions;
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Item Registry generated at {registryPath} with {definitions.Length} items.");
        EditorUtility.DisplayDialog(
            "Item Registry Generator",
            $"Done! Registry created at:\n{registryPath}\n\n" +
            $"{definitions.Length} item definitions created in:\n{DefsFolder}\n\n" +
            "Remember to assign icon textures to each ItemDefinition in the Inspector.",
            "OK"
        );
    }

    private static string SanitizeFileName(string name)
    {
        // Strip characters that are invalid in file names
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "");
        return name.Replace(" ", "_");
    }
}
#endif
