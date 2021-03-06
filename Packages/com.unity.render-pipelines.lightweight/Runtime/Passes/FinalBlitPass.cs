namespace UnityEngine.Rendering.LWRP
{
    /// <summary>
    /// Copy the given color target to the current camera target
    ///
    /// You can use this pass to copy the result of rendering to
    /// the camera target. The pass takes the screen viewport into
    /// consideration.
    /// </summary>
    internal class FinalBlitPass : ScriptableRenderPass
    {
        const string m_ProfilerTag = "Final Blit Pass";
        RenderTargetHandle m_Source;
        Material m_BlitMaterial;
        TextureDimension m_TargetDimension;
        bool m_ClearBlitTarget;
        Rect m_PixelRect;
        
        public FinalBlitPass(RenderPassEvent evt, Material blitMaterial)
        {
            m_BlitMaterial = blitMaterial;
            renderPassEvent = evt;
        }

        /// <summary>
        /// Configure the pass
        /// </summary>
        /// <param name="baseDescriptor"></param>
        /// <param name="colorHandle"></param>
        /// <param name="clearBlitTarget"></param>
        /// <param name="pixelRect"></param>
        public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle colorHandle, bool clearBlitTarget = false, Rect pixelRect = new Rect())
        {
            m_Source = colorHandle;
            m_TargetDimension = baseDescriptor.dimension;
            m_ClearBlitTarget = clearBlitTarget;
            m_PixelRect = pixelRect;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_BlitMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", m_BlitMaterial, GetType().Name);
                return;
            }

            bool requiresSRGBConvertion = Display.main.requiresSrgbBlitToBackbuffer;
            bool killAlpha = renderingData.killAlphaInFinalBlit;

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

            if (requiresSRGBConvertion)
                cmd.EnableShaderKeyword(ShaderKeywordStrings.LinearToSRGBConversion);
            else
                cmd.DisableShaderKeyword(ShaderKeywordStrings.LinearToSRGBConversion);

            if (killAlpha)
                cmd.EnableShaderKeyword(ShaderKeywordStrings.KillAlpha);
            else
                cmd.DisableShaderKeyword(ShaderKeywordStrings.KillAlpha);

            if (renderingData.cameraData.isStereoEnabled || renderingData.cameraData.isSceneViewCamera)
            {
                cmd.Blit(m_Source.Identifier(), BuiltinRenderTextureType.CameraTarget);
            }
            else
            {
                cmd.SetGlobalTexture("_BlitTex", m_Source.Identifier());

                // TODO: Final blit pass should always blit to backbuffer. The first time we do we don't need to Load contents to tile.
                // We need to keep in the pipeline of first render pass to each render target to propertly set load/store actions.
                // meanwhile we set to load so split screen case works.
                SetRenderTarget(
                    cmd,
                    BuiltinRenderTextureType.CameraTarget,
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    m_ClearBlitTarget ? ClearFlag.Color : ClearFlag.None,
                    Color.black,
                    m_TargetDimension);

                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.SetViewport(m_PixelRect != Rect.zero ? m_PixelRect : renderingData.cameraData.camera.pixelRect);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_BlitMaterial);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
