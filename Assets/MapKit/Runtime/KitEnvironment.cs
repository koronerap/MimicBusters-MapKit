using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// A scene's environment lighting - sky, ambient, fog, default reflection - captured as values so it
    /// can be applied to a scene that is not the one it was baked in.
    ///
    /// It exists because a kit map is never the active scene. RenderSettings are read from the active
    /// scene alone, and making the map's scene active is not an option: NGO spawns runtime NetworkObjects
    /// (players) into the active scene, and a scene NGO did not load is one it cannot synchronise
    /// (ExternalMapSupportPlan.md §6.3). So the packer captures the map scene's settings while the author
    /// has it open, and the game applies them to whichever scene is active.
    ///
    /// Captured whole rather than as opt-in overrides (the way MapEnvironment does it for prefab maps):
    /// these values were baked together, and applying some of them would light the map with a mixture it
    /// was never baked against.
    /// </summary>
    [Serializable]
    public sealed class KitEnvironment
    {
        public bool captured;

        public Material skybox;
        public AmbientMode ambientMode = AmbientMode.Skybox;
        public Color ambientSkyColor;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;
        public Color ambientLight;
        public float ambientIntensity = 1f;

        /// <summary>The ambient probe as baked, 3 colour channels x 9 coefficients. What lights anything
        /// the APV does not cover; a skybox alone is not enough because the runtime recomputation
        /// (DynamicGI.UpdateEnvironment) would give a different answer than the bake did.</summary>
        public float[] ambientProbe = new float[27];

        public bool fog;
        public Color fogColor;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public float fogDensity;
        public float fogStartDistance;
        public float fogEndDistance;

        public DefaultReflectionMode reflectionMode;
        public Texture reflectionTexture;
        public float reflectionIntensity = 1f;
        public int reflectionBounces = 1;

        public Color subtractiveShadowColor;

        /// <summary>Directional or not is global state, not per scene: a directional atlas read in
        /// non-directional mode loses its normal response, the reverse reads garbage.</summary>
        public LightmapsMode lightmapsMode = LightmapsMode.CombinedDirectional;

        /// <summary>The active scene's environment, right now.</summary>
        public static KitEnvironment Capture()
        {
            var e = new KitEnvironment
            {
                captured = true,
                skybox = RenderSettings.skybox,
                ambientMode = RenderSettings.ambientMode,
                ambientSkyColor = RenderSettings.ambientSkyColor,
                ambientEquatorColor = RenderSettings.ambientEquatorColor,
                ambientGroundColor = RenderSettings.ambientGroundColor,
                ambientLight = RenderSettings.ambientLight,
                ambientIntensity = RenderSettings.ambientIntensity,
                fog = RenderSettings.fog,
                fogColor = RenderSettings.fogColor,
                fogMode = RenderSettings.fogMode,
                fogDensity = RenderSettings.fogDensity,
                fogStartDistance = RenderSettings.fogStartDistance,
                fogEndDistance = RenderSettings.fogEndDistance,
                reflectionMode = RenderSettings.defaultReflectionMode,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                reflectionBounces = RenderSettings.reflectionBounces,
                subtractiveShadowColor = RenderSettings.subtractiveShadowColor,
                lightmapsMode = LightmapSettings.lightmapsMode,
            };

            SphericalHarmonicsL2 sh = RenderSettings.ambientProbe;
            for (int channel = 0; channel < 3; channel++)
            {
                for (int i = 0; i < 9; i++)
                    e.ambientProbe[channel * 9 + i] = sh[channel, i];
            }

            // Custom mode names its own cubemap; Skybox mode means the one baked for this scene.
            e.reflectionTexture = RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom
                ? RenderSettings.customReflectionTexture
                : ReflectionProbe.defaultTexture;

            return e;
        }

        public void Apply()
        {
            if (!captured)
                return;

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientIntensity = ambientIntensity;

            // After the mode and colours, and with no UpdateEnvironment call: that would recompute the
            // probe from the skybox and overwrite the baked one set here.
            if (ambientProbe != null && ambientProbe.Length == 27)
            {
                var sh = new SphericalHarmonicsL2();
                for (int channel = 0; channel < 3; channel++)
                {
                    for (int i = 0; i < 9; i++)
                        sh[channel, i] = ambientProbe[channel * 9 + i];
                }
                RenderSettings.ambientProbe = sh;
            }

            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;

            if (reflectionTexture != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflectionTexture;
            }
            else if (skybox == null && reflectionMode == DefaultReflectionMode.Skybox)
            {
                // No sky to reflect: the map was baked with a black default reflection. Left in Skybox mode
                // it would reflect the ACTIVE scene's sky instead - measured on the HQ spike, 2-10% brighter
                // than the same lightmaps loaded the normal way.
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = BlackCube;
            }
            else
            {
                RenderSettings.defaultReflectionMode = reflectionMode;
            }

            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.reflectionBounces = Mathf.Clamp(reflectionBounces, 1, 5);
            RenderSettings.subtractiveShadowColor = subtractiveShadowColor;
            LightmapSettings.lightmapsMode = lightmapsMode;
        }

        static Cubemap blackCube;

        static Cubemap BlackCube
        {
            get
            {
                if (blackCube != null)
                    return blackCube;

                blackCube = new Cubemap(1, TextureFormat.RGBA32, false) { name = "KitBlackReflection" };
                for (int face = 0; face < 6; face++)
                    blackCube.SetPixel((CubemapFace)face, 0, 0, Color.black);
                blackCube.Apply(false, true);
                return blackCube;
            }
        }

        public string Describe() =>
            !captured ? "not captured"
            : $"sky {(skybox != null ? skybox.name : "none")}, ambient {ambientMode} x{ambientIntensity:0.##}, "
              + $"fog {(fog ? fogMode.ToString() : "off")}, reflection {(reflectionTexture != null ? reflectionTexture.name : reflectionMode.ToString())}, "
              + $"lightmaps {lightmapsMode}";
    }
}
