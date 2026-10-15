using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Languages (Game.Core Loc, Content/strings.json, docs/recipes/add-language.md): the English
    /// text is the key and the fallback, the chosen language is saved, and every text the game
    /// shows has an entry in every shipped language: the UXML texts, every Loc.T("...") literal
    /// in the code, world names and subtitles, level titles and boss names.
    /// </summary>
    public class LocTests
    {
        const string TwoLanguages = "{\"languages\": {\"en\": \"English\", \"xx\": \"Xish\"}, \"texts\": {\"xx\": {\"Play\": \"Plxx\", \"Gem {0}/{1}\": \"{0} of {1} xx\"}}}";

        [TearDown]
        public void ShippedTablesBack()
        {
            Loc.Load(Kit.Read(Loc.Path), null);
            Loc.Use(Loc.English);
        }

        [Test]
        public void EnglishIsTheKeyAndTheFallback()
        {
            var errors = new List<string>();
            Loc.Load(TwoLanguages, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual("Play", Loc.T("Play"), "English is the key itself");
            Assert.AreEqual("Gem 2/3", Loc.T("Gem {0}/{1}", 2, 3));
            Assert.AreEqual("xx", Loc.Use("xx"));
            Assert.AreEqual("Plxx", Loc.T("Play"));
            Assert.AreEqual("2 of 3 xx", Loc.T("Gem {0}/{1}", 2, 3));
            Assert.AreEqual("Settings", Loc.T("Settings"), "a text the table lacks is shown in English");
            Assert.AreEqual("Xish", Loc.NameOf("xx"));
            Assert.AreEqual("en", Loc.Next("xx"), "Settings cycles back to the first language");
        }

        [Test]
        public void UnknownLanguagesFallBackToEnglish()
        {
            Loc.Load(TwoLanguages, null);
            Assert.AreEqual(Loc.English, Loc.Use("zz"));
            Assert.AreEqual(Loc.English, Loc.Use(""));
            Assert.AreEqual(Loc.English, Loc.Use(null));
            Loc.Use("xx");
            Loc.Load(null, null);
            Assert.AreEqual(Loc.English, Loc.Language, "a language that is no longer shipped falls back to English");
            Assert.AreEqual(1, Loc.Languages.Count);
        }

        [Test]
        public void ProblemsInStringsJsonAreReported()
        {
            var errors = new List<string>();
            Loc.Load("{\"languages\": {\"en\": \"English\"}, \"texts\": {\"xx\": {\"Play\": \"P\"}}}", errors);
            Assert.That(errors, Has.Some.Contains("\"xx\""), "a table for a language that isn't listed");
            errors.Clear();
            Loc.Load("{\"texts\": {}}", errors);
            Assert.That(errors, Has.Some.Contains("\"languages\""));
            errors.Clear();
            Loc.Load("{ not json", errors);
            Assert.IsNotEmpty(errors);
            Assert.AreEqual("English", Loc.NameOf(Loc.English), "English is always there");
        }

        [Test]
        public void TheLanguageIsSavedWithTheSettings()
        {
            var p = new Progress();
            Assert.AreEqual(Loc.English, p.Settings.Language);
            p.Settings.Language = "xx";
            var back = Progress.FromJson(p.ToJson(), out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual("xx", back.Settings.Language);
            var old = Progress.FromJson("{\"version\": 1, \"settings\": {\"musicVolume\": 0.5}}", out _);
            Assert.AreEqual(Loc.English, old.Settings.Language, "a save from before languages starts in English");
        }

        /// <summary>Every text the game shows in English, with where it comes from.</summary>
        public static Dictionary<string, string> GameTexts()
        {
            var texts = new Dictionary<string, string>();
            void Add(string text, string where)
            {
                if (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text, "[A-Za-z]")) return;
                if (!texts.ContainsKey(text)) texts[text] = where;
            }
            string root = Kit.Root;
            foreach (var f in Directory.GetFiles(Path.Combine(root, ProjectPaths.UI), "*.uxml"))
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "\\btext=\"([^\"]*)\""))
                    Add(UnXml(m.Groups[1].Value), Path.GetFileName(f));
            foreach (var f in Directory.GetFiles(Path.Combine(root, ProjectPaths.Game, "Game"), "*.cs", SearchOption.AllDirectories))
                foreach (Match m in Regex.Matches(Regex.Replace(File.ReadAllText(f), "^\\s*//.*$", "", RegexOptions.Multiline), "Loc\\.T\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                    Add(Regex.Unescape(m.Groups[1].Value), Path.GetFileName(f));
            foreach (var w in Kit.Content.Worlds)
            {
                Add(w.Name, "worlds.json");
                Add(w.Subtitle, "worlds.json");
            }
            foreach (var l in Kit.Content.Levels)
            {
                Add(l.Title, $"level_{l.Number:00}.txt");
                foreach (var b in new World(l, Kit.Config(), Kit.Enemies).Bosses) Add(b.Name, $"level_{l.Number:00}.txt (boss)");
            }
            return texts;
        }

        static string UnXml(string s) => s.Replace("&#10;", "\n").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");

        /// <summary>The texts <paramref name="code"/> has no entry for, as "text (where)" lines.</summary>
        public static List<string> Missing(string code, Dictionary<string, string> texts)
        {
            var missing = new List<string>();
            foreach (var t in texts)
                if (!Loc.Has(code, t.Key)) missing.Add($"\"{t.Key.Replace("\n", "\\n")}\" ({t.Value})");
            return missing;
        }

        [Test]
        public void TheTextListFindsEveryKindOfText()
        {
            var texts = GameTexts();
            Assert.Greater(texts.Count, 40, "the texts are found (UXML, Loc.T, content)");
            foreach (var t in new[] { "Settings", "Checkpoint", "Level clear!", Kit.Content.Levels[0].Title, Kit.Content.Worlds[0].Name })
                Assert.IsTrue(texts.ContainsKey(t), $"\"{t}\" should be among the game's texts");
            Assert.IsTrue(texts.ContainsKey("Keys: arrows or WASD move, {0} jumps, {1} dashes, {2} pauses.\nGamepad: stick moves, A jumps, X dashes, Start pauses."), "escaped literals are read as the game shows them");
            Loc.Load(TwoLanguages, null);
            var missing = Missing("xx", new Dictionary<string, string> { ["Play"] = "Title.uxml", ["Settings"] = "Title.uxml" });
            CollectionAssert.AreEqual(new[] { "\"Settings\" (Title.uxml)" }, missing);
        }

        [Test]
        public void EveryTextHasAnEntryInEveryShippedLanguage()
        {
            var texts = GameTexts();
            var problems = new List<string>();
            foreach (var (code, name) in Loc.Languages)
            {
                if (code == Loc.English) continue;
                var missing = Missing(code, texts);
                if (missing.Count > 0) problems.Add($"{name} ({code}) has no entry for {missing.Count} text(s) in Content/strings.json:\n  {string.Join("\n  ", missing)}");
                foreach (var key in Loc.Keys(code))
                {
                    var want = Placeholders(key);
                    var got = Placeholders(Loc.In(code, key));
                    if (want != got) problems.Add($"{name} ({code}): \"{key}\" has the values {want} but its translation has {got}");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        static string Placeholders(string s)
        {
            var found = new SortedSet<string>();
            foreach (Match m in Regex.Matches(s, "\\{\\d+\\}")) found.Add(m.Value);
            return found.Count == 0 ? "(none)" : string.Join(" ", found);
        }
    }
}
