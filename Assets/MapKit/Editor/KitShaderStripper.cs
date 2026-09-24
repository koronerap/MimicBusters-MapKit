using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// No compiled shader variants in a kit map's bundles.
    ///
    /// The game never draws with a kit bundle's shaders: KitShaders (in the game's KitMapLoader) replaces
    /// every material's shader with the game's own of the same name as the scene loads, before its first
    /// frame. Only the names are used. Compiling the variants anyway cost URP Lit's 2144 of them per
    /// platform, over half an hour for the first Windows pack (measured in the kit's self test), for code
    /// that is thrown away on arrival.
    ///
    /// Only while KitPack is building. The game's own player builds, which run this same editor assembly,
    /// strip nothing here.
    /// </summary>
    sealed class KitShaderStripper : IPreprocessShaders
    {
        // Last, after URP's own stripping: whatever it leaves goes too.
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (KitPack.Building)
                data.Clear();
        }
    }
}
