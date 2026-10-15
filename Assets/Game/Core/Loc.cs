using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>
    /// The game's texts in the player's language (docs/recipes/add-language.md). The English text
    /// is the key: <c>Loc.T("Checkpoint")</c> is "Checkpoint" in English and the entry for
    /// "Checkpoint" in another language's table. Texts with values use {0}, {1}:
    /// <c>Loc.T("Gem {0}/{1}", found, 3)</c>. A text a table doesn't have is shown in English, so a
    /// missing translation never breaks the game (LocTests lists them).
    ///
    /// The tables are Content/strings.json: "languages" lists each language's code and its own
    /// name (the Settings button shows it; English first), and "texts" holds one table per
    /// language other than English. The template ships English only, so Settings has no Language
    /// row until a second language is added.
    ///
    /// What goes through it: every text in the UXML files (Ui.Load), every literal the code shows
    /// (Loc.T("...")), and the content's names (world names and subtitles, level titles, boss
    /// names) where they are shown. The credits page reads Content/credits_&lt;code&gt;.txt when it
    /// exists. The player's choice is saved in progress.json (Progress.SettingsData.Language).
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Path = "Content/strings";

        /// <summary>The shipped languages, code and the language's own name, in the order Settings cycles through them.</summary>
        public static readonly List<(string Code, string Name)> Languages = new List<(string, string)> { (English, "English") };

        /// <summary>The language in use. Set it with <see cref="Use"/>.</summary>
        public static string Language { get; private set; } = English;

        static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Reads strings.json (null or empty: English only). Problems go to <paramref name="errors"/>.
        /// Keeps the language in use when it's still there, English otherwise.
        /// </summary>
        public static void Load(string json, List<string> errors, string where = "strings.json")
        {
            Languages.Clear();
            Tables.Clear();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var root = Json.TryParse(json, where, out string err) as JsonObject;
                if (err != null || root == null) errors?.Add(err ?? $"{where}: expected an object");
                else
                {
                    var langs = root.Obj("languages");
                    if (langs == null) errors?.Add($"{where}: \"languages\" is missing (an object of code: name, English first)");
                    else
                        foreach (var code in langs.Keys)
                        {
                            string name = langs.Str(code);
                            if (code.Length == 0 || name.Length == 0) errors?.Add($"{where}: language \"{code}\" needs a code and a name");
                            else Languages.Add((code, name));
                        }
                    var texts = root.Obj("texts");
                    if (texts != null)
                        foreach (var code in texts.Keys)
                        {
                            if (!IsKnown(code))
                            {
                                errors?.Add($"{where}: \"texts\" has a table for \"{code}\", which \"languages\" doesn't list");
                                continue;
                            }
                            var block = texts.Obj(code);
                            if (block == null)
                            {
                                errors?.Add($"{where}: texts.{code} must be an object of English text: translation");
                                continue;
                            }
                            var table = new Dictionary<string, string>();
                            foreach (var key in block.Keys)
                                if (block.Get(key) is string s && s.Length > 0) table[key] = s;
                                else errors?.Add($"{where}: texts.{code}[\"{key}\"] must be a non-empty string");
                            Tables[code] = table;
                        }
                }
            }
            if (!IsKnown(English)) Languages.Insert(0, (English, "English"));
            Use(Language);
        }

        public static bool IsKnown(string code) => Languages.Exists(l => l.Code == code);

        /// <summary>Switches to <paramref name="code"/>; an unknown or empty code gives English. Returns the language now in use.</summary>
        public static string Use(string code)
        {
            Language = !string.IsNullOrEmpty(code) && IsKnown(code) ? code : English;
            return Language;
        }

        /// <summary>The language after <paramref name="code"/> in <see cref="Languages"/> (the Settings button), wrapping around.</summary>
        public static string Next(string code)
        {
            int i = Languages.FindIndex(l => l.Code == code);
            return Languages[(i + 1) % Languages.Count].Code;
        }

        /// <summary>A language's own name ("English"), or the code when it isn't shipped.</summary>
        public static string NameOf(string code)
        {
            foreach (var l in Languages) if (l.Code == code) return l.Name;
            return code;
        }

        /// <summary>True when <paramref name="code"/> has its own text for <paramref name="key"/> (always true for English).</summary>
        public static bool Has(string code, string key) => code == English || (Tables.TryGetValue(code, out var t) && t.ContainsKey(key));

        /// <summary>The English texts a language's table has (the keys), for the checks.</summary>
        public static IEnumerable<string> Keys(string code) => Tables.TryGetValue(code, out var t) ? t.Keys : (IEnumerable<string>)new string[0];

        /// <summary>The text in the language in use, with {0}, {1} filled in.</summary>
        public static string T(string english, params object[] args) => In(Language, english, args);

        /// <summary>The text in <paramref name="code"/>, falling back to the English.</summary>
        public static string In(string code, string english, params object[] args)
        {
            if (english == null) return "";
            string text = english;
            if (code != English && Tables.TryGetValue(code, out var t) && t.TryGetValue(english, out var tr)) text = tr;
            if (args == null || args.Length == 0) return text;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch (System.FormatException)
            {
                return text; // a translation with a broken {0}: show it unfilled rather than fail (LocTests catches it)
            }
        }
    }
}
