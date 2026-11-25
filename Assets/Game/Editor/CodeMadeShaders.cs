using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Keeps in a built player the shaders and shader variants that the game's code asks for by name (work order 032,
    /// 2026-11-20). A player is built with only the shaders, and of each only the keyword combinations, that some
    /// material in a scene or an asset of the build uses; the materials the code makes at run time
    /// (<see cref="PropFactory"/>, <see cref="CryptKit.MakeMaterial"/>,
    /// <see cref="CryptKit.MakeFloorMaterial"/>, the enemies' models' in <see cref="EnemyLook"/>, the tinted boxes of
    /// <see cref="GeneratedLevel"/> and the enemies' capsules of <see cref="EnemyFactory"/>) are not among them. So one
    /// small material per shader and keyword set the code uses is kept in a Resources folder, which goes into every
    /// build whole: <see cref="Folder"/>. Nothing loads them; they are there for their shader variants.
    ///
    /// What the code makes, read from it: URP Simple Lit (<see cref="PropFactory.ShaderName"/>) with no keyword (a prop,
    /// a kit piece or an enemy's mesh without an emission map, every floor) and with _EMISSION (one with an emission
    /// map); URP Lit with no keyword (the boxes' and capsules' fallback when the scene has no material to copy, and
    /// every Simple Lit material's fallback when Simple Lit is missing). The code's _SPECULARHIGHLIGHTS_OFF is not a
    /// keyword of Simple Lit and is dropped by Unity; its _SPECULAR_COLOR is turned off, as it is here. The copies of the
    /// scene's own materials need nothing: their shaders and keywords are in the build already.
    ///
    /// Made through Unity (run in batch mode: <see cref="Ensure"/>), each written only when it is missing or different.
    /// </summary>
    public static class CodeMadeShaders
    {
        public const string Folder = "Assets/Game/Resources/CodeMadeShaders";

        public sealed class Kept
        {
            public string File;
            public string Shader;
            public string[] Keywords;
            public bool Emission;
        }

        public static readonly Kept[] All =
        {
            new Kept { File = "SimpleLit", Shader = PropFactory.ShaderName, Keywords = new string[0] },
            new Kept { File = "SimpleLit_Emission", Shader = PropFactory.ShaderName, Keywords = new[] { "_EMISSION" }, Emission = true },
            new Kept { File = "Lit", Shader = "Universal Render Pipeline/Lit", Keywords = new string[0] },
            // Work order 115: her model's toon look, the project's own shader (Assets/Game/Shaders/ToonHer.shader), found by name by
            // ToonLook; it has no keyword of its own (its variants are URP's lights and shadows, which every build keeps). Work order
            // 119 declares Simple Lit's _EMISSION in it, only so that an enemy's material keeps that keyword while it wears this
            // shader: its code does not branch on it, so the variant without it, kept here, draws an enemy's glow the same.
            new Kept { File = "ToonHer", Shader = ToonLook.ShaderName, Keywords = new string[0] },
        };

        [MenuItem("Roguelike/Build/Keep the code's shaders in the build")]
        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources")) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Resources", "CodeMadeShaders");
            foreach (Kept k in All)
            {
                string path = Folder + "/" + k.File + ".mat";
                Shader shader = Shader.Find(k.Shader);
                if (shader == null) throw new System.InvalidOperationException("Shader not found: " + k.Shader);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool make = m == null;
                if (make) m = new Material(shader);
                if (m.shader != shader) m.shader = shader;
                m.name = k.File;
                // Its properties set so that URP's check of a material asset (BaseShaderGUI.SetMaterialKeywords, run when it
                // is imported) leaves exactly the keywords the code's materials have: Simple Lit's _SpecularHighlights 1 is
                // "no specular" (no _SPECULAR_COLOR), Lit's 1 is highlights on (no _SPECULARHIGHLIGHTS_OFF); _EMISSION only
                // with an emissive colour and an emissive GI flag.
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", k.Emission ? Color.cyan : Color.black);
                m.globalIlluminationFlags = k.Emission ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                var want = new HashSet<string>(k.Keywords);
                foreach (string kw in m.shaderKeywords)
                    if (!want.Contains(kw)) m.DisableKeyword(kw);
                foreach (string kw in k.Keywords)
                    if (!m.IsKeywordEnabled(kw)) m.EnableKeyword(kw);
                // Saved every time (unchanged, the file stays as it is): a property set above may have changed.
                if (make) AssetDatabase.CreateAsset(m, path);
                else EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            foreach (Kept k in All)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + k.File + ".mat");
                Debug.Log("Code-made shaders kept: " + k.File + " = " + (m != null ? m.shader.name + " [" + string.Join(" ", m.shaderKeywords) + "]" : "MISSING"));
            }
            if (!AllThere()) throw new System.InvalidOperationException("The code's shaders are not kept as they must be: see the lines above.");
        }

        /// <summary>True when every kept material is there with its shader and exactly its keywords.</summary>
        public static bool AllThere()
        {
            foreach (Kept k in All)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + k.File + ".mat");
                if (m == null || m.shader == null || m.shader.name != k.Shader) return false;
                var have = new HashSet<string>(m.shaderKeywords);
                if (!have.SetEquals(k.Keywords)) return false;
            }
            return true;
        }

        /// <summary>The files, for the report.</summary>
        public static IEnumerable<string> Paths()
        {
            foreach (Kept k in All) yield return Path.Combine(Folder, k.File + ".mat");
        }
    }
}
