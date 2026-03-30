using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PincushionRenderPass : ScriptableRenderPass
{
    Material material;
    RenderTargetIdentifier source;
    RenderTargetHandle tempTex;

    public PincushionRenderPass(Material mat, RenderPassEvent evt)
    {
        material = mat;
        renderPassEvent = evt;
        tempTex.Init("_TempPincushionTex");
    }

    public void Setup(RenderTargetIdentifier src)
    {
        source = src;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (material == null) return;

        CommandBuffer cmd = CommandBufferPool.Get("PincushionDistortion");

        RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        cmd.GetTemporaryRT(tempTex.id, desc);
        cmd.Blit(source, tempTex.Identifier(), material);
        cmd.Blit(tempTex.Identifier(), source);
        cmd.ReleaseTemporaryRT(tempTex.id);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(tempTex.id);
    }
}