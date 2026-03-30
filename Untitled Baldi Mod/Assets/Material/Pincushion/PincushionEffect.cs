using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PincushionEffect : MonoBehaviour
{
    public Material pincushionMaterial;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (pincushionMaterial != null)
            Graphics.Blit(src, dest, pincushionMaterial);
        else
            Graphics.Blit(src, dest);
    }
}