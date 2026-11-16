using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KioskStand
{
    public class HeaderImage { public string Path; public double X, Y, Width, Height; }
    public class CaseSection { public string Title, Text; }
    public class QuestionCfg
    {
        public string Id, Text, Hint, Explanation;
        public bool Multiple;
        public List<string> Options = new List<string>();
        public List<int> Correct = new List<int>();
    }
    public class TaskCfg
    {
        public string Id, Title, Subtitle;
        public List<CaseSection> Sections = new List<CaseSection>();
        public List<string> Images = new List<string>();
        public List<QuestionCfg> Questions = new List<QuestionCfg>();
    }
    public class LinkItem { public string Label, Url, QrImage; }

    public class AppConfig
    {
        public string BaseDir;

        // окно / поведение
        public bool Fullscreen = true, Topmost = false, HideCursor = false;
        public double DesignWidth = 2560, DesignHeight = 1440;
        public string Background = "assets/background.png";
        public string FontFamily = "Segoe UI";
        public int IdleTimeoutSeconds = 180;
        public string AdminPin = "1234";
        public string ResultsFile = "results.json";
        public bool ShowCorrectAnswers = true, ShowScore = true, OnScreenKeyboard = true;
        public int WinnersCount = 25;
        public List<HeaderImage> Header = new List<HeaderImage>();
        public Dictionary<string, List<HeaderImage>> Decor = new Dictionary<string, List<HeaderImage>>();

        // цвета
        public string ColorPrimary = "#00A99D", ColorText = "#005850", ColorAccent = "#8EE8E3",
                      ColorCorrect = "#1E9E5A", ColorWrong = "#D64545", ColorPlaceholder = "#8FC9C3",
                      ColorSoft = "#DDF7F7", ColorField = "#E6FFFFFF", ColorSelectedText = null, ColorBodyText = null,
                      ColorCard = "#D9FFFFFF", ColorOverlay = "#80000000";

        // контент
        public Dictionary<string, string> Texts = DefaultTexts();
        public List<TaskCfg> Tasks = new List<TaskCfg>();
        public List<string> Specializations = new List<string>();
        public string OtherSpecialization = "другое";
        public List<string> CooperationFormats = new List<string>();
        public List<LinkItem> Links = new List<LinkItem>();
        public double LinksPadding = 140, QrSize = 350;

        public string T(string key)
        {
            string v;
            return Texts.TryGetValue(key, out v) ? v : key;
        }

        public string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(BaseDir, path);
        }

        public static AppConfig Load(string file)
        {
            var c = new AppConfig();
            c.BaseDir = System.IO.Path.GetDirectoryName(file);
            var root = Json.Parse(File.ReadAllText(file, Encoding.UTF8));

            var app = Json.Obj(root, "app");
            c.Fullscreen = Json.Bool(app, "fullscreen", c.Fullscreen);
            c.Topmost = Json.Bool(app, "topmost", c.Topmost);
            c.HideCursor = Json.Bool(app, "hideCursor", c.HideCursor);
            c.DesignWidth = Json.Num(app, "designWidth", c.DesignWidth);
            c.DesignHeight = Json.Num(app, "designHeight", c.DesignHeight);
            c.Background = Json.Str(app, "background", c.Background);
            c.FontFamily = Json.Str(app, "fontFamily", c.FontFamily);
            c.IdleTimeoutSeconds = (int)Json.Num(app, "idleTimeoutSeconds", c.IdleTimeoutSeconds);
            c.AdminPin = Json.Str(app, "adminPin", c.AdminPin);
            c.ResultsFile = Json.Str(app, "resultsFile", c.ResultsFile);
            c.ShowCorrectAnswers = Json.Bool(app, "showCorrectAnswers", c.ShowCorrectAnswers);
            c.ShowScore = Json.Bool(app, "showScore", c.ShowScore);
            c.OnScreenKeyboard = Json.Bool(app, "onScreenKeyboard", c.OnScreenKeyboard);
            c.WinnersCount = (int)Json.Num(app, "winnersCount", c.WinnersCount);
            foreach (var h in Json.Arr(app, "header")) c.Header.Add(ReadImage(h));

            var colors = Json.Obj(root, "colors");
            c.ColorPrimary = Json.Str(colors, "primary", c.ColorPrimary);
            c.ColorText = Json.Str(colors, "text", c.ColorText);
            c.ColorAccent = Json.Str(colors, "accent", c.ColorAccent);
            c.ColorCorrect = Json.Str(colors, "correct", c.ColorCorrect);
            c.ColorWrong = Json.Str(colors, "wrong", c.ColorWrong);
            c.ColorPlaceholder = Json.Str(colors, "placeholder", c.ColorPlaceholder);
            c.ColorSoft = Json.Str(colors, "soft", c.ColorSoft);
            c.ColorField = Json.Str(colors, "fieldBackground", c.ColorField);
            c.ColorSelectedText = Json.Str(colors, "selectedText", c.ColorSelectedText);
            c.ColorBodyText = Json.Str(colors, "bodyText", c.ColorBodyText);
            c.ColorCard = Json.Str(colors, "card", c.ColorCard);
            c.ColorOverlay = Json.Str(colors, "overlay", c.ColorOverlay);

            // декоративные картинки по экранам: start, case, question, done, form, final
            foreach (var kv in Json.Obj(root, "decor"))
            {
                var list = new List<HeaderImage>();
                var arr = kv.Value as List<object>;
                if (arr != null) foreach (var h in arr) list.Add(ReadImage(h));
                c.Decor[kv.Key] = list;
            }

            foreach (var kv in Json.Obj(root, "texts"))
                if (kv.Value != null) c.Texts[kv.Key] = kv.Value.ToString();

            foreach (var t in Json.Arr(root, "tasks"))
            {
                var task = new TaskCfg
                {
                    Id = Json.Str(t, "id", "task" + (c.Tasks.Count + 1)),
                    Title = Json.Str(t, "title", ""),
                    Subtitle = Json.Str(t, "subtitle", "")
                };
                foreach (var s in Json.Arr(t, "sections"))
                    task.Sections.Add(new CaseSection { Title = Json.Str(s, "title", ""), Text = Json.Str(s, "text", "") });
                foreach (var img in Json.Arr(t, "images")) if (img != null) task.Images.Add(img.ToString());
                foreach (var q in Json.Arr(t, "questions"))
                {
                    var qc = new QuestionCfg
                    {
                        Id = Json.Str(q, "id", "q" + (task.Questions.Count + 1)),
                        Text = Json.Str(q, "text", ""),
                        Hint = Json.Str(q, "hint", null),
                        Explanation = Json.Str(q, "explanation", null),
                        Multiple = Json.Bool(q, "multiple", false)
                    };
                    foreach (var o in Json.Arr(q, "options")) qc.Options.Add(o == null ? "" : o.ToString());
                    foreach (var o in Json.Arr(q, "correct")) if (o is double) qc.Correct.Add((int)(double)o);
                    task.Questions.Add(qc);
                }
                c.Tasks.Add(task);
            }

            var reg = Json.Obj(root, "registration");
            foreach (var s in Json.Arr(reg, "specializations")) c.Specializations.Add(s.ToString());
            c.OtherSpecialization = Json.Str(reg, "otherOption", c.OtherSpecialization);
            foreach (var s in Json.Arr(reg, "cooperationFormats")) c.CooperationFormats.Add(s.ToString());

            var fin = Json.Obj(root, "final");
            c.LinksPadding = Json.Num(fin, "linksPadding", c.LinksPadding);
            c.QrSize = Json.Num(fin, "qrSize", c.QrSize);
            foreach (var l in Json.Arr(fin, "links"))
                c.Links.Add(new LinkItem { Label = Json.Str(l, "label", ""), Url = Json.Str(l, "url", ""), QrImage = Json.Str(l, "qrImage", null) });

            return c;
        }

        static HeaderImage ReadImage(object h)
        {
            return new HeaderImage
            {
                Path = Json.Str(h, "image", null),
                X = Json.Num(h, "x", 0), Y = Json.Num(h, "y", 0),
                Width = Json.Num(h, "width", double.NaN), Height = Json.Num(h, "height", double.NaN)
            };
        }

        static Dictionary<string, string> DefaultTexts()
        {
            var t = new Dictionary<string, string>();
            t["start.title"] = "Клинические задачи";
            t["start.subtitle"] = "Решите клинические задачи и примите участие в розыгрыше призов";
            t["start.button"] = "Начать";

            t["case.label"] = "Клиническая задача {task} из {tasks}";
            t["case.toQuestions"] = "Перейти к вопросам";
            t["case.zoomHint"] = "Нажмите на фото, чтобы увеличить";

            t["question.progress"] = "Задача {task} из {tasks}  ·  Вопрос {q} из {qs}";
            t["question.single"] = "Выберите один правильный ответ";
            t["question.multiple"] = "Выберите несколько правильных ответов";
            t["question.answer"] = "Ответить";
            t["question.next"] = "Следующий вопрос";
            t["question.nextTask"] = "Следующая задача";
            t["question.finish"] = "Завершить";
            t["question.showCase"] = "Условие задачи";
            t["question.correct"] = "Верно!";
            t["question.wrong"] = "Неверно. Правильный ответ отмечен зелёным";
            t["question.close"] = "Закрыть";

            t["done.title"] = "Вы прошли обе клинические задачи";
            t["done.subtitle"] = "Зарегистрируйтесь, чтобы принять участие\nв розыгрыше призов от Catalysis";
            t["done.score"] = "Правильных ответов: {correct} из {total}";
            t["done.button"] = "Зарегистрироваться";
            t["done.closed"] = "Регистрация на розыгрыш закрыта. Спасибо за участие!";
            t["done.closedButton"] = "Завершить";

            t["form.fio"] = "ФИО";
            t["form.email"] = "Электронная почта";
            t["form.phone"] = "Номер телефона";
            t["form.spec"] = "Специализация";
            t["form.specOther"] = "Укажите специализацию";
            t["form.coopQuestion"] = "Было бы вам интересно профессиональное сотрудничество с Catalysis?";
            t["form.yes"] = "Да";
            t["form.no"] = "Нет";
            t["form.submit"] = "Подтвердить регистрацию";

            t["err.required"] = "Заполните поле";
            t["err.phone"] = "Введите номер в формате +7 (XXX) XXX-XX-XX";
            t["err.email"] = "Проверьте адрес электронной почты";
            t["err.spec"] = "Выберите специализацию";
            t["err.coop"] = "Ответьте, пожалуйста, на вопрос о сотрудничестве";
            t["err.duplicate"] = "Этот номер телефона уже зарегистрирован для участия в розыгрыше";
            t["err.save"] = "Не удалось сохранить анкету. Попробуйте ещё раз.";
            t["err.closed"] = "Регистрация закрыта";
            t["err.ok"] = "Понятно";

            t["final.title"] = "Вы зарегистрированы!";
            t["final.subtitle"] = "Подпишитесь на канал Catalysis Expert в MAX или Telegram.\nОтсканируйте QR-код нужной платформы";
            t["final.note"] = "**Результаты** розыгрыша опубликуем в каналах Catalysis Expert **в 15:00**";
            t["final.button"] = "Завершить";

            t["keyboard.done"] = "Готово";
            return t;
        }
    }
}
