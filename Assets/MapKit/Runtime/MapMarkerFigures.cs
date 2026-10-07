using UnityEngine;

namespace Mimicraft.Gameplay
{
    /// <summary>One box of a marker figure, in the frame of the thing it is drawn for: a player's root for the
    /// standing figure, the console's root for the console and the person seated at it.</summary>
    public readonly struct FigureBox
    {
        public readonly Vector3 Center;
        public readonly Quaternion Rotation;
        public readonly Vector3 Size;

        /// <summary>The box's place in its figure, worked out once: the figures are static, and a gizmo is drawn on
        /// every repaint of every Scene view.</summary>
        public readonly Matrix4x4 Local;

        public FigureBox(float cx, float cy, float cz, float rx, float ry, float rz, float rw, float sx, float sy, float sz)
        {
            Center = new Vector3(cx, cy, cz);

            // Normalised, because the baked numbers are rounded: a quaternion a ten-thousandth off unit length is
            // one Matrix4x4.TRS refuses, with an error per box per repaint.
            Rotation = new Quaternion(rx, ry, rz, rw).normalized;
            Size = new Vector3(sx, sy, sz);
            Local = Matrix4x4.TRS(Center, Rotation, Vector3.one);
        }
    }

    /// <summary>
    /// What a MapMarker looks like in the Scene view: the thing the game will put there, drawn as boxes. A player
    /// spawn is the default character as it stands in a round, the Gartic computer is the game's console with the
    /// modeller sitting at it.
    ///
    /// The boxes are MEASURED, not drawn by hand: MapMarkerFigureBaker (Tools ▸ Mimicraft ▸ MapKit ▸ İşaretçi
    /// figürlerini üret) builds the Player prefab's character and the console prefab, poses the character, and
    /// writes every box it finds into MapMarkerFigures.Baked.cs. The kit cannot read the game's prefabs - a map maker
    /// has neither - so the figures travel with the kit as numbers. Bake again when the character or the console
    /// changes.
    /// </summary>
    public static partial class MapMarkerFigures
    {
        /// <summary>Draws a figure in <paramref name="frame"/>: each box filled faintly and outlined. False when
        /// there is nothing baked to draw, so the caller can fall back to something simpler.</summary>
        public static bool Draw(FigureBox[] boxes, Matrix4x4 frame, Color color, float fillAlpha)
        {
            if (boxes == null || boxes.Length == 0)
                return false;

            Color fill = new Color(color.r, color.g, color.b, color.a * fillAlpha);
            foreach (FigureBox box in boxes)
            {
                Gizmos.matrix = frame * box.Local;
                if (fillAlpha > 0f)
                {
                    Gizmos.color = fill;
                    Gizmos.DrawCube(Vector3.zero, box.Size);
                }

                Gizmos.color = color;
                Gizmos.DrawWireCube(Vector3.zero, box.Size);
            }

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = color;
            return true;
        }
    }
}
