using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// A small JSON reader and writer for the content files and saves (the simulation can't use
    /// UnityEngine.JsonUtility). Objects become Dictionary&lt;string, object&gt; (keys in file
    /// order are kept in <see cref="JsonObject.Keys"/>), arrays List&lt;object&gt;, numbers double.
    /// Errors name the line and column.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text, string where = "json")
        {
            var p = new Parser(text ?? "", where);
            p.SkipWs();
            var v = p.Value();
            p.SkipWs();
            if (!p.End) throw p.Error("unexpected text after the value");
            return v;
        }

        /// <summary>Like Parse, but returns null and an error message instead of throwing.</summary>
        public static object TryParse(string text, string where, out string error)
        {
            try
            {
                error = null;
                return Parse(text, where);
            }
            catch (FormatException e)
            {
                error = e.Message;
                return null;
            }
        }

        sealed class Parser
        {
            readonly string _s;
            readonly string _where;
            int _i;

            public Parser(string s, string where)
            {
                _s = s;
                _where = where;
            }

            public bool End => _i >= _s.Length;

            public FormatException Error(string msg)
            {
                int line = 1, col = 1;
                for (int k = 0; k < _i && k < _s.Length; k++)
                {
                    if (_s[k] == '\n') { line++; col = 1; }
                    else col++;
                }
                return new FormatException($"{_where}:{line}:{col}: {msg}");
            }

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿') _i++;
                    else break;
                }
            }

            public object Value()
            {
                if (End) throw Error("expected a value");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return Num();
                        throw Error($"unexpected '{c}'");
                }
            }

            void Word(string w)
            {
                if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) throw Error($"expected '{w}'");
                _i += w.Length;
            }

            JsonObject Obj()
            {
                var o = new JsonObject();
                _i++;
                SkipWs();
                if (!End && _s[_i] == '}') { _i++; return o; }
                while (true)
                {
                    SkipWs();
                    if (End || _s[_i] != '"') throw Error("expected a \"key\"");
                    string k = Str();
                    SkipWs();
                    if (End || _s[_i] != ':') throw Error("expected ':' after a key");
                    _i++;
                    SkipWs();
                    if (o.ContainsKey(k)) throw Error($"duplicate key \"{k}\"");
                    o.Add(k, Value());
                    SkipWs();
                    if (End) throw Error("unclosed '{'");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return o; }
                    throw Error("expected ',' or '}' (a trailing comma or a missing comma?)");
                }
            }

            List<object> Arr()
            {
                var a = new List<object>();
                _i++;
                SkipWs();
                if (!End && _s[_i] == ']') { _i++; return a; }
                while (true)
                {
                    SkipWs();
                    a.Add(Value());
                    SkipWs();
                    if (End) throw Error("unclosed '['");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return a; }
                    throw Error("expected ',' or ']'");
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                _i++;
                while (true)
                {
                    if (End) throw Error("unclosed string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\n') throw Error("newline inside a string");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("bad escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error($"bad escape '\\{e}'");
                    }
                }
            }

            double Num()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (!End && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == 'e' || _s[_i] == 'E' || _s[_i] == '+' || _s[_i] == '-')) _i++;
                string t = _s.Substring(start, _i - start);
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) throw Error($"bad number '{t}'");
                return d;
            }
        }

        // ---------------------------------------------------------------- writing

        /// <summary>Writes a value as indented JSON (2 spaces). Dictionaries keep insertion order when they're JsonObjects.</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v, int indent)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: WriteString(sb, s); break;
                case double d: sb.Append(Num(d)); break;
                case float f: sb.Append(Num(f)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case JsonObject o: WriteObject(sb, o.Keys, k => o[k], indent); break;
                case IDictionary<string, object> dict: WriteObject(sb, dict.Keys, k => dict[k], indent); break;
                case System.Collections.IEnumerable list:
                {
                    var items = new List<object>();
                    foreach (var x in list) items.Add(x);
                    bool simple = items.TrueForAll(x => !(x is IDictionary<string, object>) && !(x is System.Collections.IList));
                    sb.Append('[');
                    for (int k = 0; k < items.Count; k++)
                    {
                        if (k > 0) sb.Append(simple ? ", " : ",");
                        if (!simple) { sb.Append('\n'); sb.Append(' ', (indent + 1) * 2); }
                        WriteValue(sb, items[k], indent + 1);
                    }
                    if (!simple && items.Count > 0) { sb.Append('\n'); sb.Append(' ', indent * 2); }
                    sb.Append(']');
                    break;
                }
                default: WriteString(sb, v.ToString()); break;
            }
        }

        static void WriteObject(StringBuilder sb, IEnumerable<string> keys, Func<string, object> get, int indent)
        {
            sb.Append('{');
            bool first = true;
            foreach (var k in keys)
            {
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append(' ', (indent + 1) * 2);
                WriteString(sb, k);
                sb.Append(": ");
                WriteValue(sb, get(k), indent + 1);
            }
            if (!first) { sb.Append('\n'); sb.Append(' ', indent * 2); }
            sb.Append('}');
        }

        static string Num(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "0";
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------------------------------------------------------- reading helpers

        public static double Num(object o, double fallback = 0) => o is double d ? d : fallback;
        public static string Str(object o, string fallback = "") => o is string s ? s : fallback;
        public static JsonObject Obj(object o) => o as JsonObject;
        public static List<object> Arr(object o) => o as List<object>;
    }

    /// <summary>A JSON object that remembers its key order (so saved files read naturally).</summary>
    public sealed class JsonObject : Dictionary<string, object>
    {
        readonly List<string> _order = new List<string>();

        public new IEnumerable<string> Keys => _order;

        public new void Add(string key, object value)
        {
            base.Add(key, value);
            _order.Add(key);
        }

        public new object this[string key]
        {
            get => base[key];
            set
            {
                if (!ContainsKey(key)) _order.Add(key);
                base[key] = value;
            }
        }

        public new bool Remove(string key)
        {
            _order.Remove(key);
            return base.Remove(key);
        }

        public object Get(string key) => TryGetValue(key, out var v) ? v : null;
        public double Num(string key, double fallback = 0) => TryGetValue(key, out var v) && v is double d ? d : fallback;
        public int Int(string key, int fallback = 0) => TryGetValue(key, out var v) && v is double d ? (int)d : fallback;
        public string Str(string key, string fallback = "") => TryGetValue(key, out var v) && v is string s ? s : fallback;
        public bool Bool(string key, bool fallback = false) => TryGetValue(key, out var v) && v is bool b ? b : fallback;
        public JsonObject Obj(string key) => TryGetValue(key, out var v) ? v as JsonObject : null;
        public List<object> Arr(string key) => TryGetValue(key, out var v) ? v as List<object> : null;
    }
}
