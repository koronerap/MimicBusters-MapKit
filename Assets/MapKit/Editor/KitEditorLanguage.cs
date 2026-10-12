using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The language the kit's windows speak: one setting for all of them, remembered per machine, chosen in Map Kit >
    /// Settings or on the welcome window (KitSettingsWindow, KitWelcomeWindow). The first time it follows the
    /// computer's language when the kit has it, English otherwise.
    ///
    /// The words are written where they are used, as Turkish/English pairs - T("tr", "en") - the two languages the kit
    /// was written in. Every other language comes from MapKitStrings.csv in the Localization folder beside this file,
    /// keyed by the ENGLISH text: a row per string, a column per language. A string with no row, or an empty cell, shows
    /// in English, so a sentence added to the code is never blank, only untranslated until the table catches up
    /// (Tools/MapKit/Strings/kit_strings.py lists what is missing and builds the table).
    ///
    /// Strings with values in them are formats - F("... {0} ...", "... {0} ...", value) - because the key has to be
    /// the same text every time for the table to find it; an interpolated string would be a new key per value.
    ///
    /// No Arabic: the kit's windows are IMGUI, which neither joins Arabic letters nor lays text right to left, so it
    /// would show as loose letters backwards.
    /// </summary>
    public static class KitEditorLanguage
    {
        const string Key = "MimicBusters.MapKit.Language";
        const string OldTurkishKey = "MimicBusters.MapKit.Turkish";
        const string TableFile = "MapKitStrings.csv";

        /// <summary>The kit's languages: code, the language's own name, the culture its numbers are written in.</summary>
        public static readonly (string Code, string Name, string Culture)[] Languages =
        {
            ("en", "English", "en-US"),
            ("tr", "Türkçe", "tr-TR"),
            ("de", "Deutsch", "de-DE"),
            ("es", "Español", "es-ES"),
            ("fr", "Français", "fr-FR"),
            ("it", "Italiano", "it-IT"),
            ("pt-BR", "Português (Brasil)", "pt-BR"),
            ("ru", "Русский", "ru-RU"),
            ("uk", "Українська", "uk-UA"),
            ("el", "Ελληνικά", "el-GR"),
            ("ja", "日本語", "ja-JP"),
            ("ko", "한국어", "ko-KR"),
            ("zh-Hans", "简体中文", "zh-CN"),
            ("zh-Hant", "繁體中文", "zh-TW"),
        };

        /// <summary>The language changed. Every view is repainted already; this is for a window that caches text.</summary>
        public static event Action Changed;

        static string current;
        static CultureInfo culture;
        static string cultureFor;

        /// <summary>The language in force, as its code ("tr", "pt-BR").</summary>
        public static string Language
        {
            get => current ??= Saved();
            set
            {
                if (IndexOf(value) < 0 || value == Language)
                    return;

                current = value;
                EditorPrefs.SetString(Key, value);
                Changed?.Invoke();
                InternalEditorUtility.RepaintAllViews();
            }
        }

        public static bool Turkish => Language == "tr";

        public static int IndexOf(string code)
        {
            for (int i = 0; i < Languages.Length; i++)
            {
                if (Languages[i].Code == code)
                    return i;
            }

            return -1;
        }

        /// <summary>What was chosen on this machine; the old TR/EN switch's answer before there was a choice; the
        /// computer's language the first time.</summary>
        static string Saved()
        {
            string saved = EditorPrefs.GetString(Key, "");
            if (IndexOf(saved) >= 0)
                return saved;

            if (EditorPrefs.HasKey(OldTurkishKey))
                return EditorPrefs.GetBool(OldTurkishKey) ? "tr" : "en";

            return Application.systemLanguage switch
            {
                SystemLanguage.Turkish => "tr",
                SystemLanguage.German => "de",
                SystemLanguage.Spanish => "es",
                SystemLanguage.French => "fr",
                SystemLanguage.Italian => "it",
                SystemLanguage.Portuguese => "pt-BR",
                SystemLanguage.Russian => "ru",
                SystemLanguage.Ukrainian => "uk",
                SystemLanguage.Greek => "el",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Chinese => "zh-Hans",
                SystemLanguage.ChineseSimplified => "zh-Hans",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                _ => "en",
            };
        }

        /// <summary>The text in the language in force: one of the two written here, or the table's.</summary>
        public static string T(string tr, string en)
        {
            string language = Language;
            if (language == "tr")
                return tr;
            if (language == "en")
                return en;

            return Lookup(en, language) ?? en;
        }

        /// <summary>A format in the language in force, filled in. A translation whose placeholders do not fit is
        /// shown in English rather than thrown out of a window's OnGUI.</summary>
        public static string F(string tr, string en, params object[] args)
        {
            try
            {
                return string.Format(Culture, T(tr, en), args);
            }
            catch (FormatException)
            {
                return string.Format(Culture, en, args);
            }
        }

        /// <summary>How numbers and dates are written in the language in force.</summary>
        public static CultureInfo Culture
        {
            get
            {
                string language = Language;
                if (culture != null && cultureFor == language)
                    return culture;

                cultureFor = language;
                try
                {
                    culture = CultureInfo.GetCultureInfo(Languages[Mathf.Max(0, IndexOf(language))].Culture);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.InvariantCulture;
                }

                return culture;
            }
        }

        /// <summary>The kit's settings button, in each window's toolbar where the TR/EN switch used to be: opens
        /// Map Kit > Settings. Always false - the language is changed in that window, which repaints every view.</summary>
        public static bool Toolbar()
        {
            var content = new GUIContent(EditorGUIUtility.IconContent("_Popup").image,
                T("Kit ayarları: dil, oyunun yeri", "Kit settings: language, where the game is"));
            if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(28)))
                KitSettingsWindow.Open();
            return false;
        }

        // ------------------------------------------------------------------ the table

        // Language code -> English text -> the text in that language.
        static Dictionary<string, Dictionary<string, string>> table;

        static string Lookup(string en, string language)
        {
            if (string.IsNullOrEmpty(en))
                return null;

            if (table == null)
                table = Read();

            return table.TryGetValue(language, out Dictionary<string, string> texts)
                   && texts.TryGetValue(en, out string text) && !string.IsNullOrEmpty(text)
                ? text
                : null;
        }

        /// <summary>Forgets the table, so it is read again the next time a string is asked for. After the file
        /// changes (KitStringsPostprocessor).</summary>
        public static void Reload()
        {
            table = null;
            InternalEditorUtility.RepaintAllViews();
        }

        static Dictionary<string, Dictionary<string, string>> Read()
        {
            var read = new Dictionary<string, Dictionary<string, string>>();
            string path = TablePath();
            if (path == null)
                return read;

            List<List<string>> rows;
            try
            {
                rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Map Kit] {TableFile} could not be read, the kit stays in English: {e.Message}");
                return read;
            }

            if (rows.Count == 0)
                return read;

            List<string> header = rows[0];
            for (int column = 1; column < header.Count; column++)
                read[header[column].Trim()] = new Dictionary<string, string>();

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                if (row.Count == 0 || string.IsNullOrEmpty(row[0]))
                    continue;

                for (int column = 1; column < header.Count && column < row.Count; column++)
                {
                    if (!string.IsNullOrEmpty(row[column]))
                        read[header[column].Trim()][row[0]] = row[column];
                }
            }

            return read;
        }

        /// <summary>The table, wherever the kit's folder is in this project.</summary>
        static string TablePath()
        {
            foreach (string guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(TableFile)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == TableFile)
                    return path;
            }

            return null;
        }

        /// <summary>RFC 4180: commas between cells, a cell in quotes may hold commas, line breaks and doubled quotes.
        /// Line breaks inside a cell come back as \n whatever the file was saved with.</summary>
        static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else if (c != '\r')
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    case '﻿':
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }

    /// <summary>Reads the strings table again when it is imported: a translation fixed in the file shows at once.</summary>
    sealed class KitStringsPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (path.EndsWith("MapKitStrings.csv", StringComparison.Ordinal))
                {
                    KitEditorLanguage.Reload();
                    return;
                }
            }
        }
    }
}
