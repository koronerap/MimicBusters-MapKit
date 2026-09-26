using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Greybox building blocks for the kit's examples: boxes with a one-metre grid on every face, in a few
    /// flat colours that say what a thing is (grey for the world, orange for what you press, yellow for
    /// what moves, red for what hurts, blue for teleports, purple for what appears and disappears).
    ///
    /// Each box is its own mesh with its texture coordinates in world metres, so the grid runs on across
    /// neighbouring boxes whatever their size, the way a level designer's blockout looks. Only the URP Lit
    /// shader and one grid texture: nothing a map may not carry. The meshes are kept in one asset beside
    /// the scene, and each gets lightmap coordinates of its own.
    /// </summary>
    internal sealed class KitGreybox
    {
        [System.Flags]
        public enum Faces
        {
            None = 0,
            Right = 1,   // +X
            Left = 2,    // -X
            Top = 4,     // +Y
            Bottom = 8,  // -Y
            Front = 16,  // +Z
            Back = 32,   // -Z
            All = 63,

            /// <summary>Every face but the bottom when the box stands on the ground, every face otherwise.
            /// </summary>
            Auto = 64,
        }

        public static readonly Color Floor = new Color(0.62f, 0.63f, 0.66f);
        public static readonly Color Wall = new Color(0.8f, 0.8f, 0.78f);
        public static readonly Color Dark = new Color(0.34f, 0.35f, 0.38f);
        public static readonly Color Prop = new Color(0.58f, 0.47f, 0.36f);
        public static readonly Color Button = new Color(1f, 0.42f, 0.08f);
        public static readonly Color Mover = new Color(0.98f, 0.78f, 0.18f);
        public static readonly Color Hazard = new Color(0.85f, 0.16f, 0.12f);
        public static readonly Color Teleport = new Color(0.18f, 0.62f, 0.95f);
        public static readonly Color Toggle = new Color(0.62f, 0.36f, 0.9f);
        public static readonly Color Reward = new Color(1f, 0.84f, 0.35f);

        readonly string folder;
        readonly Shader lit;
        readonly Texture2D grid;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly List<Mesh> meshes = new List<Mesh>();

        public KitGreybox(string mapFolder, Shader litShader)
        {
            folder = mapFolder;
            lit = litShader;
            grid = GridTexture(mapFolder);
        }

        /// <summary>The material for a colour, made the first time it is asked for.</summary>
        public Material Material(Color color, string name)
        {
            if (materials.TryGetValue(color, out Material material))
                return material;

            material = new Material(lit) { name = name };
            material.SetTexture("_BaseMap", grid);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            Directory.CreateDirectory(folder + "/Materials");
            AssetDatabase.CreateAsset(material, $"{folder}/Materials/{name}.mat");
            materials[color] = material;
            return material;
        }

        /// <summary>A box between two corners in world space, parented without moving. Static unless
        /// asked otherwise: what moves, and what appears and disappears, must not be (its baked light would
        /// stay behind).</summary>
        public GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Material material, bool isStatic = true,
            Faces faces = Faces.Auto)
        {
            Vector3 lo = Vector3.Min(min, max), hi = Vector3.Max(min, max);
            Vector3 center = (lo + hi) * 0.5f, size = hi - lo;

            if (faces == Faces.Auto)
                faces = Mathf.Abs(lo.y) < 0.001f && isStatic ? Faces.All & ~Faces.Bottom : Faces.All;

            var go = new GameObject(name);
            go.transform.position = center;
            if (parent != null)
                go.transform.SetParent(parent, true);

            if (faces != Faces.None && material != null)
            {
                Mesh mesh = BuildMesh(name, center, size, faces);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            // The whole box, whichever faces are drawn. Added after the mesh, a collider fits itself to it,
            // and a box drawn with one face only would get a collider centred on that face: a quarter of a
            // metre off, in front of a wall, above a floor.
            var collider = go.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = size;

            if (isStatic)
                KitNewMap.MakeStatic(go);
            return go;
        }

        /// <summary>Writes every mesh made so far into one asset beside the scene. Before the scene is
        /// saved: a scene that referred to meshes that are not assets would carry copies of them.</summary>
        public void SaveMeshes()
        {
            if (meshes.Count == 0)
                return;

            string path = $"{folder}/Greybox Meshes.asset";
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++)
                AssetDatabase.AddObjectToAsset(meshes[i], meshes[0]);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ the mesh

        static readonly (Faces face, Vector3 normal, Vector3 up)[] Sides =
        {
            (Faces.Right, Vector3.right, Vector3.up),
            (Faces.Left, Vector3.left, Vector3.up),
            (Faces.Top, Vector3.up, Vector3.forward),
            (Faces.Bottom, Vector3.down, Vector3.forward),
            (Faces.Front, Vector3.forward, Vector3.up),
            (Faces.Back, Vector3.back, Vector3.up),
        };

        Mesh BuildMesh(string name, Vector3 center, Vector3 size, Faces faces)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 half = size * 0.5f;

            foreach ((Faces face, Vector3 normal, Vector3 up) in Sides)
            {
                if ((faces & face) == 0)
                    continue;

                // Seen from outside, "across" runs to the right of "up": the corners below go clockwise, which
                // is the side Unity draws.
                Vector3 across = Vector3.Cross(normal, up);
                float n = Mathf.Abs(Vector3.Dot(half, normal));
                float a = Mathf.Abs(Vector3.Dot(half, across));
                float b = Mathf.Abs(Vector3.Dot(half, up));

                int first = vertices.Count;
                foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) })
                {
                    Vector3 local = normal * n + across * (corner.x * a) + up * (corner.y * b);
                    Vector3 world = center + local;
                    vertices.Add(local);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(world, across), Vector3.Dot(world, up)));
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            meshes.Add(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ the grid

        /// <summary>One metre of grid: a light field, a dark line at the metre and fainter ones every quarter.
        /// White enough that a material's colour tints it.</summary>
        static Texture2D GridTexture(string mapFolder)
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = 0.86f;
                    if (Line(x, 128, 2) || Line(y, 128, 2))
                        value = 0.72f;
                    if (Line(x, size, 5) || Line(y, size, 5))
                        value = 0.5f;
                    byte v = (byte)Mathf.RoundToInt(value * 255f);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            string path = $"{mapFolder}/Greybox Grid.png";
            Directory.CreateDirectory(mapFolder);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.maxTextureSize = size;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Whether a pixel is on a line repeating every period pixels, the line width pixels wide
        /// and centred on the repeat (half of it on each side of the seam).</summary>
        static bool Line(int pixel, int period, int width)
        {
            int m = pixel % period;
            return m < width / 2 + width % 2 || m >= period - width / 2;
        }
    }
}
