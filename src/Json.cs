using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KioskStand
{
    /// <summary>
    /// Минимальный JSON-парсер/писатель без внешних зависимостей.
    /// Объекты -> Dictionary&lt;string, object&gt;, массивы -> List&lt;object&gt;,
    /// числа -> double, строки -> string, true/false -> bool, null -> null.
    /// Поддерживает комментарии // и /* */ и висячие запятые (удобно для ручного редактирования конфига).
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            object v = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw p.Error("Лишние символы после конца JSON");
            return v;
        }

        class Parser
        {
            readonly string s; int i;
            public Parser(string s) { this.s = s; i = 0; if (s.Length > 0 && s[0] == '﻿') i = 1; }
            public bool End { get { return i >= s.Length; } }

            public Exception Error(string msg)
            {
                int line = 1, col = 1;
                for (int k = 0; k < i && k < s.Length; k++) { if (s[k] == '\n') { line++; col = 1; } else col++; }
                return new FormatException(msg + " (строка " + line + ", позиция " + col + ")");
            }

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (char.IsWhiteSpace(c)) { i++; continue; }
                    if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
                    if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                    {
                        int e = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                        if (e < 0) throw Error("Незакрытый комментарий");
                        i = e + 2; continue;
                    }
                    break;
                }
            }

            public object ReadValue()
            {
                if (End) throw Error("Неожиданный конец файла");
                char c = s[i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == '-' || char.IsDigit(c)) return ReadNumber();
                if (Match("true")) return true;
                if (Match("false")) return false;
                if (Match("null")) return null;
                throw Error("Неожиданный символ '" + c + "'");
            }

            bool Match(string w)
            {
                if (string.CompareOrdinal(s, i, w, 0, w.Length) == 0) { i += w.Length; return true; }
                return false;
            }

            Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++; SkipWs();
                while (true)
                {
                    if (End) throw Error("Незакрытый объект");
                    if (s[i] == '}') { i++; return d; }
                    if (s[i] != '"') throw Error("Ожидалось имя свойства в кавычках");
                    string key = ReadString();
                    SkipWs();
                    if (End || s[i] != ':') throw Error("Ожидалось ':' после \"" + key + "\"");
                    i++; SkipWs();
                    d[key] = ReadValue();
                    SkipWs();
                    if (End) throw Error("Незакрытый объект");
                    if (s[i] == ',') { i++; SkipWs(); continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Error("Ожидалась ',' или '}'");
                }
            }

            List<object> ReadArray()
            {
                var l = new List<object>();
                i++; SkipWs();
                while (true)
                {
                    if (End) throw Error("Незакрытый массив");
                    if (s[i] == ']') { i++; return l; }
                    l.Add(ReadValue());
                    SkipWs();
                    if (End) throw Error("Незакрытый массив");
                    if (s[i] == ',') { i++; SkipWs(); continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw Error("Ожидалась ',' или ']'");
                }
            }

            string ReadString()
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (End) throw Error("Незакрытая строка");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (End) throw Error("Незакрытая строка");
                        char e = s[i++];
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
                                if (i + 4 > s.Length) throw Error("Неверная \\u-последовательность");
                                sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                                i += 4; break;
                            default: throw Error("Неверная escape-последовательность \\" + e);
                        }
                    }
                    else sb.Append(c);
                }
            }

            object ReadNumber()
            {
                int st = i;
                if (s[i] == '-') i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
                double d;
                if (!double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    throw Error("Неверное число");
                return d;
            }
        }

        // ---------- запись ----------
        public static string Write(object v)
        {
            var sb = new StringBuilder();
            WriteValue(sb, v, 0);
            return sb.ToString();
        }

        static void Indent(StringBuilder sb, int n) { sb.Append('\n'); sb.Append(' ', n * 2); }

        static void WriteValue(StringBuilder sb, object v, int lvl)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is DateTime) { WriteString(sb, ((DateTime)v).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)); return; }
            if (v is int || v is long || v is double || v is float || v is decimal)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return;
            }
            var dict = v as IDictionary;
            if (dict != null)
            {
                if (dict.Count == 0) { sb.Append("{}"); return; }
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Indent(sb, lvl + 1);
                    WriteString(sb, kv.Key.ToString());
                    sb.Append(": ");
                    WriteValue(sb, kv.Value, lvl + 1);
                }
                Indent(sb, lvl); sb.Append('}');
                return;
            }
            var list = v as IEnumerable;
            if (list != null)
            {
                var items = new List<object>();
                foreach (var o in list) items.Add(o);
                if (items.Count == 0) { sb.Append("[]"); return; }
                bool simple = true;
                foreach (var o in items) if (o is IDictionary || (o is IEnumerable && !(o is string))) simple = false;
                sb.Append('[');
                for (int k = 0; k < items.Count; k++)
                {
                    if (k > 0) sb.Append(simple ? ", " : ",");
                    if (!simple) Indent(sb, lvl + 1);
                    WriteValue(sb, items[k], lvl + 1);
                }
                if (!simple) Indent(sb, lvl);
                sb.Append(']');
                return;
            }
            WriteString(sb, v.ToString());
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

        // ---------- удобные аксессоры ----------
        public static Dictionary<string, object> Obj(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            if (d == null) return new Dictionary<string, object>();
            object v;
            if (d.TryGetValue(key, out v) && v is Dictionary<string, object>) return (Dictionary<string, object>)v;
            return new Dictionary<string, object>();
        }

        public static List<object> Arr(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is List<object>) return (List<object>)v;
            return new List<object>();
        }

        public static string Str(object o, string key, string def)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                if (v is double) return ((double)v).ToString(CultureInfo.InvariantCulture);
                return v.ToString();
            }
            return def;
        }

        public static double Num(object o, string key, double def)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                if (v is double) return (double)v;
                double r;
                if (double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            }
            return def;
        }

        public static bool Bool(object o, string key, bool def)
        {
            var d = o as Dictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return def;
        }

        public static bool Has(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            return d != null && d.ContainsKey(key) && d[key] != null;
        }
    }
}
