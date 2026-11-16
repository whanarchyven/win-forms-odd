using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace KioskStand
{
    /// <summary>Хранилище результатов: один JSON-файл рядом с exe.</summary>
    public class Storage
    {
        readonly string file;
        Dictionary<string, object> data;

        public Storage(string file)
        {
            this.file = file;
            Load();
        }

        public string FilePath { get { return file; } }

        void Load()
        {
            data = null;
            if (File.Exists(file))
            {
                var txt = File.ReadAllText(file, Encoding.UTF8);
                if (txt.Trim().Length > 0) data = Json.Parse(txt) as Dictionary<string, object>;
            }
            if (data == null) data = new Dictionary<string, object>();
            if (!data.ContainsKey("registrationOpen")) data["registrationOpen"] = true;
            if (!(data.ContainsKey("sessions") && data["sessions"] is List<object>)) data["sessions"] = new List<object>();
            if (!(data.ContainsKey("participants") && data["participants"] is List<object>)) data["participants"] = new List<object>();
        }

        public List<object> Sessions { get { return (List<object>)data["sessions"]; } }
        public List<object> Participants { get { return (List<object>)data["participants"]; } }

        public bool RegistrationOpen
        {
            get { return Json.Bool(data, "registrationOpen", true); }
        }

        public void SetRegistrationOpen(bool open)
        {
            data["registrationOpen"] = open;
            data[open ? "registrationOpenedAt" : "registrationClosedAt"] = Now();
            Save();
        }

        public string DrawAt { get { return Json.Str(data, "drawAt", null); } }

        public static string Now() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); }

        /// <summary>Атомарная запись: во временный файл, затем замена (с резервной копией).</summary>
        public void Save()
        {
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, Json.Write(data), new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(tmp, file, file + ".bak", true);
            else File.Move(tmp, file);
        }

        public static string NormalizePhone(string raw)
        {
            if (raw == null) return "";
            var sb = new StringBuilder();
            foreach (char ch in raw) if (char.IsDigit(ch)) sb.Append(ch);
            string d = sb.ToString();
            if (d.Length == 11 && d[0] == '8') d = "7" + d.Substring(1);
            if (d.Length == 10 && d[0] == '9') d = "7" + d;
            return d;
        }

        public bool PhoneExists(string normalized)
        {
            foreach (var p in Participants)
                if (Json.Str(p, "phoneNormalized", "") == normalized) return true;
            return false;
        }

        public void AddSession(Dictionary<string, object> session)
        {
            Sessions.Add(session);
            try { Save(); }
            catch { /* сессия без регистрации — не критично; сохранится вместе со следующей записью */ }
        }

        /// <summary>Добавляет участника. Бросает исключение при ошибке записи (запись откатывается).</summary>
        public void AddParticipant(Dictionary<string, object> p)
        {
            Participants.Add(p);
            try { Save(); }
            catch { Participants.Remove(p); throw; }
        }

        // ---------------- розыгрыш ----------------
        public List<Dictionary<string, object>> Draw(int count)
        {
            var all = new List<Dictionary<string, object>>();
            foreach (var o in Participants) { var p = (Dictionary<string, object>)o; p["winner"] = false; all.Add(p); }
            using (var rng = new RNGCryptoServiceProvider())
            {
                // тасование Фишера–Йетса с криптостойким ГСЧ
                for (int i = all.Count - 1; i > 0; i--)
                {
                    int j = RandomInt(rng, i + 1);
                    var t = all[i]; all[i] = all[j]; all[j] = t;
                }
            }
            var winners = all.GetRange(0, Math.Min(count, all.Count));
            foreach (var w in winners) w["winner"] = true;
            data["drawAt"] = Now();
            Save();
            return winners;
        }

        static int RandomInt(RandomNumberGenerator rng, int max)
        {
            var b = new byte[4];
            uint limit = uint.MaxValue - (uint.MaxValue % (uint)max);
            uint r;
            do { rng.GetBytes(b); r = BitConverter.ToUInt32(b, 0); } while (r >= limit);
            return (int)(r % (uint)max);
        }

        // ---------------- выгрузка в Excel ----------------
        public string ExportXlsx(string dir, AppConfig cfg)
        {
            string path = Path.Combine(dir, "export_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".xlsx");
            bool drawn = DrawAt != null;

            // Лист 1: участники
            var s1 = new List<List<string>>();
            var h1 = new List<string> { "№", "Дата регистрации", "ФИО", "Телефон", "Электронная почта", "Специализация",
                "Специализация (другое)", "Интерес к сотрудничеству", "Форматы сотрудничества", "Правильных ответов" };
            if (drawn) h1.Add("Победитель");
            s1.Add(h1);
            int n = 0;
            foreach (var p in Participants)
            {
                n++;
                var row = new List<string> {
                    n.ToString(), Json.Str(p, "registeredAt", ""), Json.Str(p, "fullName", ""), Json.Str(p, "phone", ""),
                    Json.Str(p, "email", ""), Json.Str(p, "specialization", ""), Json.Str(p, "specializationOther", ""),
                    Json.Str(p, "cooperation", ""), string.Join("; ", ToStrings(Json.Arr(p, "cooperationFormats"))),
                    Json.Str(p, "score", "") };
                if (drawn) row.Add(Json.Bool(p, "winner", false) ? "Победитель" : "");
                s1.Add(row);
            }

            // Лист 2: победители
            var s2 = new List<List<string>>();
            s2.Add(new List<string> { "№", "ФИО", "Телефон", "Электронная почта" });
            n = 0;
            foreach (var p in Participants)
                if (Json.Bool(p, "winner", false))
                    s2.Add(new List<string> { (++n).ToString(), Json.Str(p, "fullName", ""), Json.Str(p, "phone", ""), Json.Str(p, "email", "") });

            // Лист 3: ответы на задачи (все прохождения, включая незарегистрированных)
            var s3 = new List<List<string>>();
            var h3 = new List<string> { "ID прохождения", "Завершено", "ФИО", "Правильных ответов" };
            foreach (var t in cfg.Tasks)
                for (int qi = 0; qi < t.Questions.Count; qi++) h3.Add(t.Id + " / вопрос " + (qi + 1));
            s3.Add(h3);
            var nameBySession = new Dictionary<string, string>();
            foreach (var p in Participants) nameBySession[Json.Str(p, "sessionId", "")] = Json.Str(p, "fullName", "");
            foreach (var s in Sessions)
            {
                string sid = Json.Str(s, "id", "");
                string nm;
                var row = new List<string> { sid, Json.Str(s, "finishedAt", ""), nameBySession.TryGetValue(sid, out nm) ? nm : "",
                    Json.Str(s, "correct", "") + " из " + Json.Str(s, "total", "") };
                var answers = new Dictionary<string, string>();
                foreach (var ta in Json.Arr(s, "tasks"))
                    foreach (var a in Json.Arr(ta, "answers"))
                        answers[Json.Str(ta, "taskId", "") + "/" + Json.Str(a, "questionId", "")] =
                            Json.Str(a, "letters", "") + (Json.Bool(a, "isCorrect", false) ? " ✓" : " ✗");
                foreach (var t in cfg.Tasks)
                    foreach (var q in t.Questions) { string v; row.Add(answers.TryGetValue(t.Id + "/" + q.Id, out v) ? v : ""); }
                s3.Add(row);
            }

            XlsxWriter.Write(path, new[] { "Участники", "Победители", "Ответы" }, new[] { s1, s2, s3 });
            return path;
        }

        static List<string> ToStrings(List<object> l)
        {
            var r = new List<string>();
            foreach (var o in l) if (o != null) r.Add(o.ToString());
            return r;
        }
    }

    /// <summary>Минимальный писатель .xlsx (Office Open XML) без внешних библиотек.</summary>
    public static class XlsxWriter
    {
        public static void Write(string path, string[] names, List<List<string>>[] sheets)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var ct = new StringBuilder();
                ct.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
                ct.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                ct.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                ct.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                for (int i = 0; i < sheets.Length; i++)
                    ct.Append("<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                ct.Append("</Types>");
                Put(zip, "[Content_Types].xml", ct.ToString());

                Put(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

                var wb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < sheets.Length; i++)
                {
                    wb.Append("<sheet name=\"" + Esc(names[i]) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>");
                    rels.Append("<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>");
                }
                wb.Append("</sheets></workbook>");
                rels.Append("<Relationship Id=\"rId" + (sheets.Length + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                Put(zip, "xl/workbook.xml", wb.ToString());
                Put(zip, "xl/_rels/workbook.xml.rels", rels.ToString());

                Put(zip, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"3\"><xf/><xf fontId=\"1\" applyFont=\"1\"/><xf applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf></cellXfs></styleSheet>");

                for (int i = 0; i < sheets.Length; i++) Put(zip, "xl/worksheets/sheet" + (i + 1) + ".xml", Sheet(sheets[i]));
            }
        }

        static string Sheet(List<List<string>> rows)
        {
            int cols = 0;
            foreach (var r in rows) cols = Math.Max(cols, r.Count);
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            if (cols > 0)
            {
                sb.Append("<cols>");
                for (int c = 0; c < cols; c++)
                {
                    int w = 10;
                    foreach (var r in rows) if (c < r.Count && r[c] != null) w = Math.Max(w, Math.Min(60, r[c].Length + 2));
                    sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + w + "\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append("<row r=\"" + (r + 1) + "\">");
                for (int c = 0; c < rows[r].Count; c++)
                {
                    string v = rows[r][c] ?? "";
                    sb.Append("<c r=\"" + ColName(c) + (r + 1) + "\" t=\"inlineStr\" s=\"" + (r == 0 ? 1 : 0) + "\"><is><t xml:space=\"preserve\">" + Esc(v) + "</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        static string ColName(int c)
        {
            string s = "";
            c++;
            while (c > 0) { int m = (c - 1) % 26; s = (char)('A' + m) + s; c = (c - 1) / 26; }
            return s;
        }

        static string Esc(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                // удаляем недопустимые для XML управляющие символы
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
                sb.Append(ch);
            }
            return SecurityElement.Escape(sb.ToString());
        }

        static void Put(ZipArchive zip, string name, string content)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(content);
        }
    }
}
