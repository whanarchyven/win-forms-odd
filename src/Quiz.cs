using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KioskStand
{
    public partial class MainWindow
    {
        // answers[task][question] = выбранные индексы (null — ещё не отвечено)
        List<List<int>>[] answers;
        string sessionId, sessionStarted;
        int scoreCorrect, scoreTotal;

        void ResetQuiz()
        {
            answers = new List<List<int>>[cfg.Tasks.Count];
            for (int i = 0; i < cfg.Tasks.Count; i++)
                answers[i] = Enumerable.Range(0, cfg.Tasks[i].Questions.Count).Select(_ => (List<int>)null).ToList();
            sessionId = null;
            sessionStarted = null;
            scoreCorrect = scoreTotal = 0;
        }

        void StartQuiz()
        {
            ResetQuiz();
            sessionId = Guid.NewGuid().ToString("N").Substring(0, 10);
            sessionStarted = Storage.Now();
            if (cfg.Tasks.Count == 0) FinishQuiz();
            else ShowCase(0);
        }

        // ---------------- условие задачи ----------------

        FrameworkElement CaseBody(TaskCfg task, double width, double height)
        {
            var g = new Grid { Width = width, Height = height };
            bool hasImages = task.Images.Count > 0;
            double imgW = hasImages ? 770 : 0, gap = hasImages ? 50 : 0;

            var text = new StackPanel { Margin = new Thickness(48, 40, 48, 44) };
            if (!string.IsNullOrEmpty(task.Subtitle))
                text.Children.Add(Ui.Text(task.Subtitle, 36, FontWeights.Bold));
            foreach (var s in task.Sections)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
                if (!string.IsNullOrEmpty(s.Title)) sp.Children.Add(Ui.Text(s.Title, 32, FontWeights.Bold, Ui.Primary));
                var body = Ui.Text(s.Text, 32, FontWeights.Normal, Ui.BodyText);
                body.LineHeight = 46;
                body.Margin = new Thickness(0, 6, 0, 0);
                sp.Children.Add(body);
                text.Children.Add(sp);
            }
            var card = new Border
            {
                Width = width - imgW - gap,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Ui.Card,
                BorderBrush = Ui.CardBorder,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(28),
                Child = Ui.Scroll(text)
            };
            g.Children.Add(card);

            if (hasImages)
            {
                var imgs = new StackPanel();
                double each = Math.Max(360, (height - 60 - 24 * (task.Images.Count - 1)) / task.Images.Count);
                foreach (var path in task.Images)
                {
                    var src = Ui.LoadImage(cfg.Resolve(path));
                    if (src == null) continue;
                    var frame = new Tap
                    {
                        Background = Brushes.White,
                        CornerRadius = new CornerRadius(20),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 24),
                        BorderBrush = Ui.CardBorder,
                        BorderThickness = new Thickness(2),
                        Child = new Image { Source = src, Stretch = Stretch.Uniform, MaxHeight = each - 24 }
                    };
                    frame.Click += () => ZoomImage(src);
                    imgs.Children.Add(frame);
                }
                var hint = Ui.Text(cfg.T("case.zoomHint"), 26, FontWeights.Normal, Ui.Primary, TextAlignment.Center);
                imgs.Children.Add(hint);
                var sv = Ui.Scroll(imgs);
                sv.Width = imgW;
                sv.HorizontalAlignment = HorizontalAlignment.Right;
                g.Children.Add(sv);
            }
            return g;
        }

        void ZoomImage(ImageSource src)
        {
            var img = new Image { Source = src, Stretch = Stretch.Uniform, MaxWidth = cfg.DesignWidth - 160, MaxHeight = cfg.DesignHeight - 160, IsHitTestVisible = false };
            ShowModal(new Border { Background = Brushes.White, Padding = new Thickness(16), CornerRadius = new CornerRadius(20), Child = img, IsHitTestVisible = false }, true);
        }

        void ShowCase(int ti)
        {
            var task = cfg.Tasks[ti];
            var g = new Grid();
            g.Children.Add(Place(Ui.Text(Fill(cfg.T("case.label"), "task", ti + 1, "tasks", cfg.Tasks.Count), 32, FontWeights.SemiBold, Ui.Primary), 170, 232));
            var title = Place(Ui.Text(task.Title, 60, FontWeights.Bold), 170, 278);
            title.Width = 2220;
            g.Children.Add(title);
            g.Children.Add(Place(CaseBody(task, 2220, 790), 170, 385));
            g.Children.Add(CenterAt(Ui.Pill(cfg.T("case.toQuestions"), true, 925, 128, 56, () =>
            {
                if (task.Questions.Count > 0) ShowQuestion(ti, 0);
                else NextAfterTask(ti);
            }), 1230));
            SetScreen("case", g);
        }

        void ShowCaseModal(TaskCfg task)
        {
            var card = Card(2420);
            card.Padding = new Thickness(50, 44, 50, 44);
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(task.Title, 48, FontWeights.Bold));
            var body = CaseBody(task, 2320, 980);
            body.Margin = new Thickness(0, 24, 0, 0);
            sp.Children.Add(body);
            var close = Ui.Pill(cfg.T("question.close"), true, 640, 110, 48, CloseModal);
            close.Margin = new Thickness(0, 30, 0, 0);
            sp.Children.Add(close);
            card.Child = sp;
            ShowModal(card, false);
        }

        // ---------------- вопрос ----------------

        void ShowQuestion(int ti, int qi)
        {
            var task = cfg.Tasks[ti];
            var q = task.Questions[qi];
            var selected = new List<int>();
            bool answered = false;
            bool check = cfg.ShowCorrectAnswers && q.Correct.Count > 0;

            var g = new Grid();
            g.Children.Add(Place(Ui.Text(Fill(cfg.T("question.progress"), "task", ti + 1, "tasks", cfg.Tasks.Count, "q", qi + 1, "qs", task.Questions.Count),
                32, FontWeights.SemiBold, Ui.Primary), 170, 245));

            var caseBtn = Ui.Pill(cfg.T("question.showCase"), false, 460, 88, 34, () => ShowCaseModal(task));
            caseBtn.HorizontalAlignment = HorizontalAlignment.Right;
            caseBtn.VerticalAlignment = VerticalAlignment.Top;
            caseBtn.Margin = new Thickness(0, 222, 170, 0);
            g.Children.Add(caseBtn);

            var body = new StackPanel { Margin = new Thickness(0, 0, 30, 30) };
            body.Children.Add(Ui.Text(q.Text, 48, FontWeights.Bold));
            string hint = q.Hint ?? cfg.T(q.Multiple ? "question.multiple" : "question.single");
            var hintTb = Ui.Text(hint, 32, FontWeights.Normal, Ui.Primary);
            hintTb.FontStyle = FontStyles.Italic;
            hintTb.Margin = new Thickness(0, 14, 0, 30);
            body.Children.Add(hintTb);

            var optionViews = new List<OptionView>();
            for (int i = 0; i < q.Options.Count; i++)
            {
                var ov = new OptionView(Letters[Math.Min(i, Letters.Length - 1)].ToString(), q.Options[i], q.Multiple);
                optionViews.Add(ov);
                body.Children.Add(ov.Root);
            }
            var feedback = Ui.Text("", 38, FontWeights.Bold);
            feedback.Margin = new Thickness(0, 14, 0, 0);
            feedback.Visibility = Visibility.Collapsed;
            body.Children.Add(feedback);
            var expl = Ui.Text(q.Explanation ?? "", 32, FontWeights.Normal);
            expl.Margin = new Thickness(0, 12, 0, 0);
            expl.Visibility = Visibility.Collapsed;
            body.Children.Add(expl);

            var sv = Ui.Scroll(body);
            g.Children.Add(Place(sv, 170, 345));
            sv.Width = 2250; sv.Height = 850;

            bool lastQ = qi == task.Questions.Count - 1;
            bool lastT = ti == cfg.Tasks.Count - 1;
            string nextLabel = !lastQ ? cfg.T("question.next") : (!lastT ? cfg.T("question.nextTask") : cfg.T("question.finish"));

            Tap main = null;
            Action goNext = () =>
            {
                if (!lastQ) ShowQuestion(ti, qi + 1);
                else NextAfterTask(ti);
            };
            main = Ui.Pill(cfg.T("question.answer"), true, 925, 128, 56, () =>
            {
                if (answered) { goNext(); return; }
                if (selected.Count == 0) return;
                answered = true;
                answers[ti][qi] = selected.OrderBy(x => x).ToList();
                if (!check) { goNext(); return; }

                bool ok = q.Correct.Count > 0 && new HashSet<int>(selected).SetEquals(q.Correct);
                for (int i = 0; i < optionViews.Count; i++)
                {
                    bool isC = q.Correct.Contains(i), isS = selected.Contains(i);
                    optionViews[i].Root.IsEnabled = false;
                    optionViews[i].SetState(isC ? OptionState.Correct : (isS ? OptionState.Wrong : OptionState.Normal), isS);
                }
                feedback.Text = cfg.T(ok ? "question.correct" : "question.wrong");
                feedback.Foreground = ok ? Ui.Correct : Ui.Wrong;
                feedback.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(q.Explanation)) expl.Visibility = Visibility.Visible;
                ((TextBlock)main.Child).Text = nextLabel;
                sv.UpdateLayout();
                sv.ScrollToEnd();
            });
            Ui.SetEnabledLook(main, false);
            g.Children.Add(CenterAt(main, 1230));

            for (int i = 0; i < optionViews.Count; i++)
            {
                int idx = i;
                optionViews[i].Root.Click += () =>
                {
                    if (answered) return;
                    if (q.Multiple)
                    {
                        if (selected.Contains(idx)) selected.Remove(idx); else selected.Add(idx);
                    }
                    else { selected.Clear(); selected.Add(idx); }
                    for (int k = 0; k < optionViews.Count; k++)
                        optionViews[k].SetState(selected.Contains(k) ? OptionState.Selected : OptionState.Normal, selected.Contains(k));
                    Ui.SetEnabledLook(main, selected.Count > 0);
                };
            }
            SetScreen("question", g);
        }

        void NextAfterTask(int ti)
        {
            if (ti + 1 < cfg.Tasks.Count) ShowCase(ti + 1);
            else FinishQuiz();
        }

        void FinishQuiz()
        {
            scoreCorrect = 0; scoreTotal = 0;
            var tasks = new List<object>();
            for (int ti = 0; ti < cfg.Tasks.Count; ti++)
            {
                var t = cfg.Tasks[ti];
                var list = new List<object>();
                for (int qi = 0; qi < t.Questions.Count; qi++)
                {
                    var q = t.Questions[qi];
                    var sel = answers[ti][qi] ?? new List<int>();
                    bool ok = q.Correct.Count > 0 && new HashSet<int>(sel).SetEquals(q.Correct);
                    if (q.Correct.Count > 0) { scoreTotal++; if (ok) scoreCorrect++; }
                    var a = new Dictionary<string, object>();
                    a["questionId"] = q.Id;
                    a["selected"] = sel.Cast<object>().ToList();
                    a["letters"] = string.Join(", ", sel.Select(i => Letters[Math.Min(i, Letters.Length - 1)].ToString()));
                    a["selectedText"] = sel.Where(i => i < q.Options.Count).Select(i => (object)q.Options[i]).ToList();
                    a["isCorrect"] = ok;
                    list.Add(a);
                }
                var td = new Dictionary<string, object>();
                td["taskId"] = t.Id;
                td["title"] = t.Title;
                td["answers"] = list;
                tasks.Add(td);
            }
            var s = new Dictionary<string, object>();
            s["id"] = sessionId ?? Guid.NewGuid().ToString("N").Substring(0, 10);
            sessionId = (string)s["id"];
            s["startedAt"] = sessionStarted ?? Storage.Now();
            s["finishedAt"] = Storage.Now();
            s["correct"] = scoreCorrect;
            s["total"] = scoreTotal;
            s["tasks"] = tasks;
            store.AddSession(s);
            ShowDone();
        }

        // ---------------- экран после задач ----------------

        void ShowDone()
        {
            var g = new Grid();
            bool open = store.RegistrationOpen;
            var title = CenterAt(Ui.Text(cfg.T("done.title"), 84, FontWeights.Bold, null, TextAlignment.Center), 470);
            title.Width = 2100;
            g.Children.Add(title);
            var sub = CenterAt(Ui.Text(open ? cfg.T("done.subtitle") : cfg.T("done.closed"), 60, FontWeights.Normal, null, TextAlignment.Center), 625);
            sub.Width = 1900;
            g.Children.Add(sub);
            if (cfg.ShowScore && scoreTotal > 0)
                g.Children.Add(CenterAt(Ui.Text(Fill(cfg.T("done.score"), "correct", scoreCorrect, "total", scoreTotal), 44, FontWeights.SemiBold, Ui.Primary, TextAlignment.Center), 860));
            if (open) g.Children.Add(CenterAt(Ui.Pill(cfg.T("done.button"), true, 925, 128, 64, ShowForm), 1077));
            else g.Children.Add(CenterAt(Ui.Pill(cfg.T("done.closedButton"), true, 925, 128, 64, ShowStart), 1077));
            SetScreen("done", g);
        }
    }

    public enum OptionState { Normal, Selected, Correct, Wrong }

    /// <summary>Вариант ответа: буква в кружке + текст.</summary>
    public class OptionView
    {
        public readonly Tap Root;
        readonly Border badge;
        readonly TextBlock letter, text;

        public OptionView(string l, string t, bool multiple)
        {
            Root = new Tap
            {
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(3),
                Padding = new Thickness(24, 18, 36, 18),
                Margin = new Thickness(0, 0, 0, 18),
                MinHeight = 100,
                Dim = false
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            badge = new Border
            {
                Width = 62, Height = 62,
                CornerRadius = new CornerRadius(multiple ? 14 : 31),
                BorderThickness = new Thickness(3),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            letter = Ui.Text(l, 32, FontWeights.Bold, null, TextAlignment.Center);
            letter.VerticalAlignment = VerticalAlignment.Center;
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            badge.Child = letter;
            g.Children.Add(badge);
            text = Ui.Text(t, 36, FontWeights.Medium, Ui.BodyText);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            Root.Child = g;
            SetState(OptionState.Normal, false);
        }

        public void SetState(OptionState st, bool chosen)
        {
            Brush border = Ui.Primary, bg = Ui.FieldBg, badgeBg = Brushes.Transparent, badgeFg = Ui.Primary;
            switch (st)
            {
                case OptionState.Selected: bg = Ui.Accent; badgeBg = Ui.Primary; badgeFg = Brushes.White; break;
                case OptionState.Correct: border = Ui.Correct; bg = Ui.CorrectBg; badgeBg = Ui.Correct; badgeFg = Brushes.White; break;
                case OptionState.Wrong: border = Ui.Wrong; bg = Ui.WrongBg; badgeBg = Ui.Wrong; badgeFg = Brushes.White; break;
            }
            Root.BorderBrush = border;
            Root.Background = bg;
            Root.BorderThickness = new Thickness(chosen ? 5 : 3);
            badge.BorderBrush = st == OptionState.Normal ? Ui.Primary : border;
            badge.Background = badgeBg;
            letter.Foreground = badgeFg;
        }
    }
}
