using System.Numerics;
using Basics.Game.texture;
using Basics.Game.Utilities;
using Basics.Graphics;
using Basics.Window;
using Silk.NET.OpenGL;
using Shader = Basics.Graphics.Shader;

namespace Basics.Game.Graphics;

/// <summary>
/// Verwaltet Shader-Programme undTexturen für das Rendern.
/// Lädt Shader-Dateien, aktiviert das Shader-Programm, setzt Uniforms für View- und Projection-Matrix
/// und bindet Texturen für das Rendering.
/// </summary>
public class ShaderManager : IDisposable
{
    
    private readonly Shader _shader;
    private GL _gl; 
    
    /// <summary>
    /// Initialisiert eine neue Instanz des <see cref="ShaderManager"/>-Klassens.
    /// Lädt und kompiliert die angegebenen Vertex- und Fragment-Shader-Dateien.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="vertexShaderFile">Der Dateiname der Vertex-Shader-Datei.</param>
    /// <param name="fragmentShaderFile">Der Dateiname der Fragment-Shader-Datei.</param>
    public ShaderManager(GL gl, string vertexShaderFile, string fragmentShaderFile)
    {
        _gl = gl;

        string vertPath = "Game/Graphics/Shader/" + vertexShaderFile;
        string fragPath = "Game/Graphics/Shader/" + fragmentShaderFile;
        
        _shader = new Shader(gl, vertPath, fragPath);
    }
    
    /// <summary>
    /// Aktiviert das Shader-Programm, setzt die View- und Projection-Matrix als Uniforms
    /// und gibt einen Frustum für das Culling zurück.
    /// </summary>
    /// <param name="gl">Die OpenGL-Instanz.</param>
    /// <param name="camera">Die Kamera, deren View-Matrix verwendet werden soll.</param>
    /// <returns>Ein Frustum-Objekt für das View-Frustum-Culling.</returns>
    public unsafe Frustum Use(GL gl, Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        
        gl.CullFace(GLEnum.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);
        
        _shader.Use();
        
        var size = WindowSetup.Window.FramebufferSize;
        camera.AspectRatio = (float)size.X / size.Y; 
        
        var view = camera.GetViewMatrix();
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), camera.AspectRatio, camera.nearPlane, camera.farPlane);
        Frustum frustum = camera.CreateFrustum(view, projection);
        
        if (EngineStates.Game.DebugCamera != null)
        {
            EngineStates.Game.DebugCamera.AspectRatio = (float)size.X / size.Y;
            var debugView = EngineStates.Game.DebugCamera.GetViewMatrix();
            var debugProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), EngineStates.Game.DebugCamera.AspectRatio, EngineStates.Game.DebugCamera.nearPlane, EngineStates.Game.DebugCamera.farPlane);
            _shader.SetUniform("uView", debugView);
            _shader.SetUniform("uProjection", debugProjection);
        }
        else
        {
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);
        }

        _shader.SetUniform("uTexture", 0);

        return frustum;
    }
    
    /// <summary>
    /// Setzt die Model-Matrix als Uniform im Shader.
    /// </summary>
    /// <param name="model">Die Model-Matrix, die gesetzt werden soll.</param>
    public void SetModelMatrix(Matrix4x4 model)
    {
        _shader.SetUniform("uModel", model);
    }
    
    /// <summary>
    /// Bindet die angegebene Textur an den Shader-Texture-Slot 0.
    /// </summary>
    /// <param name="texture">Die Textur, die gebunden werden soll.</param>
    public void BindTexture(TextureArray texture)
    {
        texture.Bind(TextureUnit.Texture0);
    }

    /// <summary>
    /// Gibt das Shader-Programm frei.
    /// </summary>
    public void Dispose()
    {
        _shader.Dispose();
    }
}