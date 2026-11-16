using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KioskStand
{
    public partial class MainWindow : Window
    {
        readonly AppConfig cfg;
        readonly Storage store;
        readonly Grid stage, content, modalLayer, decorLayer;
        readonly OnScreenKeyboard keyboard;
        InputField activeField;
        DateTime lastInput = DateTime.Now;
        string screen = "start";
        bool adminOpen;
        Action onKeyboardDone;
        Action<DependencyObject> onStageTap;

        const string Letters = "АБВГДЕЖЗИКЛМНОП";

        public MainWindow(AppConfig cfg, Storage store)
        {
            this.cfg = cfg;
            this.store = store;
            Ui.Init(cfg);

            Title = "Клинические задачи";
            Background = Brushes.White;
            if (cfg.Fullscreen)
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
            else { Width = 1280; Height = 760; }
            Topmost = cfg.Topmost;
            if (cfg.HideCursor) Cursor = Cursors.None;
            Stylus.SetIsPressAndHoldEnabled(this, false);
            Stylus.SetIsFlicksEnabled(this, false);
            Stylus.SetIsTapFeedbackEnabled(this, false);

            var root = new Grid();
            var bg = Ui.LoadImage(cfg.Resolve(cfg.Background));
            if (bg != null) root.Children.Add(new Image { Source = bg, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

            stage = new Grid { Width = cfg.DesignWidth, Height = cfg.DesignHeight, ClipToBounds = true };
            root.Children.Add(new Viewbox { Child = stage, Stretch = Stretch.Uniform });

            foreach (var h in cfg.Header) AddImage(stage, h);

            decorLayer = new Grid { IsHitTestVisible = false };
            stage.Children.Add(decorLayer);

            content = new Grid();
            stage.Children.Add(content);

            // скрытая зона входа в панель организатора: 5 быстрых касаний в левом верхнем углу
            var hot = new Tap { Width = 200, Height = 200, Dim = false, Cursor = Cursor };
            Place(hot, 0, 0);
            var taps = new List<DateTime>();
            hot.Click += () =>
            {
                taps.Add(DateTime.Now);
                taps.RemoveAll(t => (DateTime.Now - t).TotalSeconds > 3);
                if (taps.Count >= 5) { taps.Clear(); AskPin(); }
            };
            stage.Children.Add(hot);

            modalLayer = new Grid { Visibility = Visibility.Collapsed, Background = Ui.Overlay };
            stage.Children.Add(modalLayer);

            keyboard = new OnScreenKeyboard();
            keyboard.Done += () => { var a = onKeyboardDone; DeactivateField(); if (a != null) a(); };
            stage.Children.Add(keyboard);

            Content = root;

            // отслеживание бездействия и касаний вне полей
            PreviewMouseDown += (s, e) => lastInput = DateTime.Now;
            PreviewTouchDown += (s, e) => lastInput = DateTime.Now;
            PreviewKeyDown += OnKey;
            PreviewTextInput += OnTextInput;
            ManipulationBoundaryFeedback += (s, e) => e.Handled = true;
            stage.PreviewMouseDown += (s, e) =>
            {
                var src = e.OriginalSource as DependencyObject;
                if (activeField != null && !Ui.IsInside<InputField>(src) && !Ui.IsInside<OnScreenKeyboard>(src)) DeactivateField();
                if (onStageTap != null) onStageTap(src);
            };

            var idle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            idle.Tick += (s, e) =>
            {
                if (cfg.IdleTimeoutSeconds <= 0 || adminOpen || screen == "start") return;
                if ((DateTime.Now - lastInput).TotalSeconds > cfg.IdleTimeoutSeconds) ShowStart();
            };
            idle.Start();

            ShowStart();
        }

        // ---------------- инфраструктура ----------------

        void AddImage(Panel host, HeaderImage h)
        {
            var src = Ui.LoadImage(cfg.Resolve(h.Path));
            if (src == null) return;
            var img = new Image { Source = src, Stretch = Stretch.Uniform, IsHitTestVisible = false };
            var bs = src as System.Windows.Media.Imaging.BitmapSource;
            // размер по умолчанию — в пикселях исходного файла (DPI в PNG игнорируем)
            if (!double.IsNaN(h.Width)) img.Width = h.Width; else if (bs != null && double.IsNaN(h.Height)) img.Width = bs.PixelWidth;
            if (!double.IsNaN(h.Height)) img.Height = h.Height; else if (bs != null && double.IsNaN(h.Width)) img.Height = bs.PixelHeight;
            Place(img, h.X, h.Y);
            host.Children.Add(img);
        }

        static T Place<T>(T el, double x, double y) where T : FrameworkElement
        {
            el.HorizontalAlignment = HorizontalAlignment.Left;
            el.VerticalAlignment = VerticalAlignment.Top;
            el.Margin = new Thickness(x, y, 0, 0);
            return el;
        }

        static T CenterAt<T>(T el, double y) where T : FrameworkElement
        {
            el.HorizontalAlignment = HorizontalAlignment.Center;
            el.VerticalAlignment = VerticalAlignment.Top;
            el.Margin = new Thickness(0, y, 0, 0);
            return el;
        }

        void SetScreen(string name, UIElement el)
        {
            DeactivateField();
            CloseModal();
            onStageTap = null;
            onKeyboardDone = null;
            screen = name;
            decorLayer.Children.Clear();
            List<HeaderImage> decor;
            if (cfg.Decor.TryGetValue(name, out decor)) foreach (var d in decor) AddImage(decorLayer, d);
            content.Children.Clear();
            content.Children.Add(el);
            lastInput = DateTime.Now;
        }

        void ActivateField(InputField f, Action onDone = null)
        {
            if (activeField != null && activeField != f) activeField.IsActive = false;
            activeField = f;
            onKeyboardDone = onDone;
            f.IsActive = true;
            if (cfg.OnScreenKeyboard) keyboard.Attach(f);
        }

        void DeactivateField()
        {
            if (activeField != null) activeField.IsActive = false;
            activeField = null;
            keyboard.Detach();
        }

        void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            if (activeField == null || string.IsNullOrEmpty(e.Text)) return;
            foreach (char c in e.Text) if (!char.IsControl(c)) activeField.Insert(c.ToString());
            e.Handled = true;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            lastInput = DateTime.Now;
            if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { AskPin(); e.Handled = true; return; }
            if (activeField == null) return;
            if (e.Key == Key.Back) { activeField.Backspace(); e.Handled = true; }
            else if (e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab)
            {
                var a = onKeyboardDone; DeactivateField(); if (a != null) a(); e.Handled = true;
            }
        }

        void ShowModal(UIElement card, bool closeOnBackdrop = false)
        {
            modalLayer.Children.Clear();
            var back = new Tap { Background = Brushes.Transparent, Dim = false, Cursor = Cursor };
            if (closeOnBackdrop) back.Click += CloseModal;
            modalLayer.Children.Add(back);
            var fe = card as FrameworkElement;
            if (fe != null) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }
            modalLayer.Children.Add(card);
            modalLayer.Visibility = Visibility.Visible;
        }

        void CloseModal()
        {
            modalLayer.Children.Clear();
            modalLayer.Visibility = Visibility.Collapsed;
            adminOpen = false;
        }

        Border Card(double width)
        {
            return new Border
            {
                Width = width,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(36),
                Padding = new Thickness(70, 60, 70, 60),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 50, ShadowDepth = 0, Opacity = 0.3 }
            };
        }

        void Message(string text, string button, Action onOk = null)
        {
            var card = Card(1400);
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(text, 46, FontWeights.SemiBold, null, TextAlignment.Center));
            var b = Ui.Pill(button ?? cfg.T("err.ok"), true, 640, 116, 48, () => { CloseModal(); if (onOk != null) onOk(); });
            b.Margin = new Thickness(0, 56, 0, 0);
            sp.Children.Add(b);
            card.Child = sp;
            ShowModal(card);
        }

        void Confirm(string text, string yes, string no, Action onYes)
        {
            var card = Card(1500);
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(text, 44, FontWeights.SemiBold, null, TextAlignment.Center));
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 56, 0, 0) };
            var bNo = Ui.Pill(no, false, 520, 110, 44, CloseModal);
            var bYes = Ui.Pill(yes, true, 520, 110, 44, () => { CloseModal(); onYes(); });
            bYes.Margin = new Thickness(40, 0, 0, 0);
            row.Children.Add(bNo); row.Children.Add(bYes);
            sp.Children.Add(row);
            card.Child = sp;
            ShowModal(card);
        }

        string Fill(string template, params object[] kv)
        {
            string s = template;
            for (int i = 0; i + 1 < kv.Length; i += 2) s = s.Replace("{" + kv[i] + "}", Convert.ToString(kv[i + 1], CultureInfo.InvariantCulture));
            return s;
        }

        // ---------------- стартовый экран ----------------

        void ShowStart()
        {
            ResetQuiz();
            var g = new Grid();
            var title = CenterAt(Ui.Text(cfg.T("start.title"), 96, FontWeights.Bold, null, TextAlignment.Center), 470);
            title.Width = 2000;
            g.Children.Add(title);
            var sub = CenterAt(Ui.Text(cfg.T("start.subtitle"), 60, FontWeights.Normal, null, TextAlignment.Center), 620);
            sub.Width = 1900;
            g.Children.Add(sub);
            g.Children.Add(CenterAt(Ui.Pill(cfg.T("start.button"), true, 925, 128, 64, StartQuiz), 1077));
            SetScreen("start", g);
        }
    }
}
