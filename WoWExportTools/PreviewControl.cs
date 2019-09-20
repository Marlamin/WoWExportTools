using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using WoWExportTools.Loaders;
using System.Drawing;
using OpenTK.Input;
using System.Collections.Generic;
using static WoWExportTools.Structs;
using CASCLib;

namespace WoWExportTools
{
    public class PreviewControl
    {
        public GLControl renderCanvas;

        public bool IsModelActive = false;
        public string ModelType = "none";

        private NewCamera ActiveCamera;

        public string SelectedFileName;

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

        public Renderer.Structs.DoodadBatch LoadM2(string fileName)
        {
            return M2Loader.LoadM2(fileName, m2ShaderProgram);
        }

        public void LoadModel(string fileName)
        {
            IsModelActive = false;
            GL.ActiveTexture(TextureUnit.Texture0);

            SelectedFileName = fileName;
            try
            {
                if (fileName.EndsWith(".m2"))
                {
                    var doodadBatch = LoadM2(fileName);

                    ActiveCamera.Pos = new Vector3((doodadBatch.boundingBox.max.Z) + 11.0f, 0.0f, 4.0f);
                    ModelType = "m2";

                    IsModelActive = true;
                }
                else if (fileName.EndsWith(".wmo"))
                {
                    var worldModel = WMOLoader.LoadWMO(fileName, wmoShaderProgram);

                    ModelType = "wmo";
                    IsModelActive = true;
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

            if (!IsModelActive) return;

            GL.Viewport(0, 0, renderCanvas.Width, renderCanvas.Height);
            GL.Enable(EnableCap.Texture2D);

            if (ModelType == "m2")
            {
                var doodadBatch = M2Loader.LoadM2(SelectedFileName, m2ShaderProgram);
                GL.UseProgram(m2ShaderProgram);

                ActiveCamera.setupGLRenderMatrix(m2ShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(m2ShaderProgram, "alphaRef");
                GL.BindVertexArray(doodadBatch.vao);

                for (var i = 0; i < doodadBatch.submeshes.Length; i++)
                {
                    var submesh = doodadBatch.submeshes[i];
                    if (!submesh.enabled)
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
            else if (ModelType == "wmo")
            {
                var worldModel = WMOLoader.LoadWMO(SelectedFileName, wmoShaderProgram);

                GL.UseProgram(wmoShaderProgram);

                ActiveCamera.setupGLRenderMatrix(wmoShaderProgram);
                ActiveCamera.flyMode = false;

                var alphaRefLoc = GL.GetUniformLocation(wmoShaderProgram, "alphaRef");

                for (var j = 0; j < worldModel.wmoRenderBatch.Length; j++)
                {
                    GL.BindVertexArray(worldModel.groupBatches[worldModel.wmoRenderBatch[j].groupID].vao);

                    switch(worldModel.wmoRenderBatch[j].blendType)
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

                    GL.BindTexture(TextureTarget.Texture2D, worldModel.wmoRenderBatch[j].materialID[0]);
                    GL.DrawElements(PrimitiveType.Triangles, (int)worldModel.wmoRenderBatch[j].numFaces, DrawElementsType.UnsignedInt, (int)worldModel.wmoRenderBatch[j].firstFace * 4);
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
