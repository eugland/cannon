using UnityEngine;

namespace Cannon.Game
{
    /// <summary>
    /// Creates colored materials at runtime by cloning base material assets kept in a
    /// Resources folder. Because those .mat assets reference the URP shaders, the build
    /// ships the shaders (with only the needed variants) — avoiding both the "null shader"
    /// crash in the player and the variant explosion caused by Always-Included shaders.
    /// </summary>
    public static class MaterialFactory
    {
        private static Material _litBase;
        private static Material _unlitBase;

        public static Material Lit(Color color) => Colored(GetLit(), color, lit: true);
        public static Material Unlit(Color color) => Colored(GetUnlit(), color, lit: false);

        private static Material GetLit()
        {
            if (_litBase == null) _litBase = Resources.Load<Material>("LitWhite");
            return _litBase;
        }

        private static Material GetUnlit()
        {
            if (_unlitBase == null) _unlitBase = Resources.Load<Material>("UnlitWhite");
            return _unlitBase;
        }

        private static Material Colored(Material baseMat, Color color, bool lit)
        {
            Material m = baseMat != null ? new Material(baseMat) : new Material(Fallback(lit));
            m.color = color;
            return m;
        }

        private static Shader Fallback(bool lit)
        {
            return Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Hidden/InternalErrorShader");
        }
    }
}
