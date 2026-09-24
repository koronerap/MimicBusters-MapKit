using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>The language the kit's windows speak: Turkish or English, the author's choice, one setting
    /// for all of them, remembered per machine. The first time it follows the system language.</summary>
    public static class KitEditorLanguage
    {
        const string Key = "MimicBusters.MapKit.Turkish";

        public static bool Turkish
        {
            get => EditorPrefs.GetBool(Key, Application.systemLanguage == SystemLanguage.Turkish);
            set => EditorPrefs.SetBool(Key, value);
        }

        public static string T(string tr, string en) => Turkish ? tr : en;

        public static CultureInfo Culture => Turkish ? CultureInfo.GetCultureInfo("tr-TR") : CultureInfo.InvariantCulture;

        /// <summary>The TR/EN switch for a window's toolbar. True when it changed.</summary>
        public static bool Toolbar()
        {
            bool turkish = Turkish;
            int chosen = GUILayout.Toolbar(turkish ? 0 : 1, new[] { "TR", "EN" }, EditorStyles.toolbarButton, GUILayout.Width(64));
            if ((chosen == 0) == turkish)
                return false;
            Turkish = chosen == 0;
            return true;
        }
    }
}
