using System.Numerics;

namespace Basics.Game.Graphics;

/// <summary>
/// Stellt Transformationsdaten für ein Objekt bereit: Position, Rotation und Skalierung.
/// Berechnet automatisch die zugehörige Model-Matrix aus den Transformationsparametern.
/// </summary>
public class Transform
{
    /// <summary>
    /// Die Position des Objekts im Weltkoordinatensystem.
    /// </summary>
    public Vector3 Position { get; set; } = new Vector3(0, 0, 0);

    /// <summary>
    /// Der Skalierungsfaktor des Objekts.
    /// </summary>
    public float Scale { get; set; } = 1f;

    /// <summary>
    /// Die Rotation des Objekts als Quaternion.
    /// </summary>
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    
    /// <summary>
    /// Berechnet die Model-Matrix aus Position, Rotation und Skalierung.
    /// Die Reihenfolge der Transformationen ist: Rotation, dann Skalierung, dann Translation.
    /// </summary>
    public Matrix4x4 ModelMatrix => Matrix4x4.Identity * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateTranslation(Position);
}