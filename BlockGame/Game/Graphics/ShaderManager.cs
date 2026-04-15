using System.Numerics;
using Basics.Game.texture;
using Basics.Game.Utilities;
using Basics.Graphics;
using Basics.Window;
using Silk.NET.OpenGL;
using Shader = Basics.Graphics.Shader;

namespace Basics.Game.Graphics;

/**
 * Die Klasse verwaltet die Shaderdateien und die Texturen
 * Shader werden von der Shader Klasse kompiliert, dann hier benutzt.
 * Mit dem Camera Objekt wird die richtige Sicht gesetzt
 */
public class ShaderManager : IDisposable
{
    
    // Die Tutorial-Shader und Texture Klassen
    private readonly Shader _shader;
    private GL _gl; 
    
    
    public ShaderManager(GL gl, string vertexShaderFile, string fragmentShaderFile)
    {
        _gl = gl;
        

        // 1. Shader laden
        // Pfad anpassen, da deine Dateien im Graphics/Shader Ordner liegen könnten
        string vertPath = "Game/Graphics/Shader/" + vertexShaderFile;
        string fragPath = "Game/Graphics/Shader/" + fragmentShaderFile;
        
        _shader = new Shader(gl, vertPath, fragPath);
    }
    
    //Shader "Aktivieren" und die Uniforms setzen
    public unsafe Frustum Use(GL gl, Camera camera)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        
        gl.CullFace(GLEnum.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);
        
        // Shader aktivieren
        _shader.Use();
        
        var size = WindowSetup.Window.FramebufferSize;
        camera.AspectRatio = (float)size.X / size.Y; 
        
        // Culling aus der Sicht der Hauptkamera behalten
        var view = camera.GetViewMatrix(); // die debug camera wird jetzt auch als parameter übergeben es wird immer die gleiche matrix erstellt
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), camera.AspectRatio, camera.nearPlane, camera.farPlane);
        Frustum frustum = camera.CreateFrustum(view, projection);
        
        //Wenn eine Debugkamera existiert, soll aus ihrer Sicht gerendert
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

        // Dem Shader sagen, dass die Textur auf Slot 0 liegt
        _shader.SetUniform("uTexture", 0);

        //Frustum an den Renderer zurückgeben, damit er die Chunks cullen kann
        return frustum;
    }
    
    //Um ein Objekt and die richtige Stelle zu setzten.
    public void SetModelMatrix(Matrix4x4 model)
    {
        _shader.SetUniform("uModel", model);
    }
    
    public void BindTexture(TextureArray texture)
    {
        texture.Bind(TextureUnit.Texture0);
    }

    public void Dispose()
    {
        _shader.Dispose();
    }
}