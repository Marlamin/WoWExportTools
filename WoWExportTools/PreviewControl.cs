using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using WoWExportTools.Loaders;
using System.Drawing;
using OpenTK.Input;
using static WoWExportTools.Structs;
using CASCLib;
using WoWExportTools.Objects;

namespace WoWExportTools
{
    public class PreviewControl
    {
        public GLControl renderCanvas;

        private NewCamera ActiveCamera;

        public Container3D activeObject = null;
        public bool IsPreviewEnabled = false;

        private int adtShaderProgram;
        private int wmoShaderProgram;
        private int m2ShaderProgram;
        private int bakeShaderProgram;
        private int bakeFullMinimapShaderProgram;

        public PreviewControl(GLControl renderCanvas)
        {
            this.renderCanvas = renderCanvas;
            this.renderCanvas.Paint += RenderCanvas_Paint;
            this.renderCanvas.Load += RenderCanvas_Load;
            this.renderCanvas.Resize += RenderCanvas_Resize;

            ActiveCamera = new NewCamera(renderCanvas.Width, renderCanvas.Height, new Vector3(0, 0, -1), new Vector3(-11, 0, 0));
        }

        public void SetCamera(float x, float y, float z, float rot)
        {
            ActiveCamera.Pos = new Vector3(x, y, z);
            ActiveCamera.rotationAngle = rot;
        }

        private void RenderCanvas_Resize(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            if (renderCanvas.Width > 0 && renderCanvas.Height > 0)
                ActiveCamera.viewportSize(renderCanvas.Width, renderCanvas.Height);
        }

        public void BakeTexture(MapTile mapTile, string outName, bool minimap = false)
        {
            new Renderer.RenderMinimap().Generate(mapTile, outName, minimap ? bakeFullMinimapShaderProgram : bakeShaderProgram);
        }

        public void LoadModel(string fileName)
        {
            GL.ActiveTexture(TextureUnit.Texture0);

            try
            {
                if (fileName.EndsWith(".m2"))
                {
                    var m2 = M2Loader.LoadM2(fileName, m2ShaderProgram);
                    activeObject = new M2Container(m2, fileName);
                    ActiveCamera.Pos = new Vector3((m2.boundingBox.max.Z) + 11.0f, 0.0f, 4.0f);
                }
                else if (fileName.EndsWith(".wmo"))
                {
                    activeObject = new WMOContainer(WMOLoader.LoadWMO(fileName, wmoShaderProgram), fileName);
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine("Error occured when loading model " + fileName + ": " + e.StackTrace);
            }
           
            ActiveCamera.ResetCamera();
        }

        public void WindowsFormsHost_Initialized(object sender, EventArgs e)
        {
            renderCanvas.MakeCurrent();
        }

        private void Update()
        {
            if (!renderCanvas.Focused) return;

            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();

            ActiveCamera.processKeyboardInput(keyboardState);

            return;
        }

        private void RenderCanvas_Load(object sender, EventArgs e)
        {
            GL.Enable(EnableCap.DepthTest);

            adtShaderProgram = Shader.CompileShader("adt");
            wmoShaderProgram = Shader.CompileShader("wmo");
            m2ShaderProgram = Shader.CompileShader("m2");
            bakeShaderProgram = Shader.CompileShader("baketexture");
            bakeFullMinimapShaderProgram = Shader.CompileShader("bakeFullMinimap");

            GL.ClearColor(Color.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        private void RenderCanvas_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (!IsPreviewEnabled)
                return;

            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            GL.Enable(EnableCap.Texture2D);

            if (activeObject is M2Container activeM2)
            {
                var m2 = activeM2.DoodadBatch;

                GL.UseProgram(m2ShaderProgram);

                ActiveCamera.setupGLRenderMatrix(m2ShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(m2ShaderProgram, "alphaRef");
                GL.BindVertexArray(m2.vao);

                for (var i = 0; i < m2.submeshes.Length; i++)
                {
                    var submesh = m2.submeshes[i];
                    if (!activeM2.EnabledGeosets[i])
                        continue;

                    switch (submesh.blendType)
                    {
                        case 0:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                        case 1:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, 0.90393700787f);
                            break;
                        case 2:
                            GL.Enable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                            break;
                        default:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                    }

                    GL.BindTexture(TextureTarget.Texture2D, submesh.material);
                    GL.DrawElements(PrimitiveType.Triangles, (int)submesh.numFaces, DrawElementsType.UnsignedInt, (int)submesh.firstFace * 4);
                }
            }
            else if (activeObject is WMOContainer activeWMO)
            {
                var wmo = activeWMO.WorldModel;

                GL.UseProgram(wmoShaderProgram);

                ActiveCamera.setupGLRenderMatrix(wmoShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(wmoShaderProgram, "alphaRef");

                for (var j = 0; j < wmo.wmoRenderBatch.Length; j++)
                {
                    GL.BindVertexArray(wmo.groupBatches[wmo.wmoRenderBatch[j].groupID].vao);

                    switch (wmo.wmoRenderBatch[j].blendType)
                    {
                        case 0:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                        case 1:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, 0.90393700787f);
                            break;
                        case 2:
                            GL.Enable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                            break;
                        default:
                            GL.Disable(EnableCap.Blend);
                            GL.Uniform1(alphaRefLoc, -1.0f);
                            break;
                    }

                    GL.BindTexture(TextureTarget.Texture2D, wmo.wmoRenderBatch[j].materialID[0]);
                    GL.DrawElements(PrimitiveType.Triangles, (int)wmo.wmoRenderBatch[j].numFaces, DrawElementsType.UnsignedInt, (int)wmo.wmoRenderBatch[j].firstFace * 4);
                }
            }

            var error = GL.GetError().ToString();
            if (error != "NoError")
                Console.WriteLine(error);

            GL.BindVertexArray(0);
            renderCanvas.SwapBuffers();
        }

        public void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            Update();
            renderCanvas.Invalidate();
        }
    }
}
