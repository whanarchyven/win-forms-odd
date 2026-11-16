using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace KioskStand
{
    /// <summary>Общие стили и фабрики элементов.</summary>
    public static class Ui
    {
        public static Brush Primary, TextBrush, BodyText, SelectedText, Accent, Correct, Wrong, Placeholder, White, FieldBg, CorrectBg, WrongBg, Soft, Card, CardBorder, Overlay;
        public static FontFamily Font;
        public static AppConfig Cfg;

        public static void Init(AppConfig cfg)
        {
            Cfg = cfg;
            Primary = B(cfg.ColorPrimary);
            TextBrush = B(cfg.ColorText);
            Accent = B(cfg.ColorAccent);
            Correct = B(cfg.ColorCorrect);
            Wrong = B(cfg.ColorWrong);
            Placeholder = B(cfg.ColorPlaceholder);
            White = Brushes.White;
            FieldBg = B(cfg.ColorField);
            BodyText = cfg.ColorBodyText != null ? B(cfg.ColorBodyText) : TextBrush;
            SelectedText = cfg.ColorSelectedText != null ? B(cfg.ColorSelectedText) : TextBrush;
            Card = B(cfg.ColorCard);
            Overlay = B(cfg.ColorOverlay);
            var pc = ((SolidColorBrush)Primary).Color;
            CardBorder = new SolidColorBrush(Color.FromArgb(0x40, pc.R, pc.G, pc.B)); CardBorder.Freeze();
            CorrectBg = B("#DDF5E6");
            WrongBg = B("#FBE0E0");
            Soft = B(cfg.ColorSoft);
            try
            {
                if (cfg.FontFamily.Contains("#"))
                    Font = new FontFamily(new Uri(cfg.BaseDir.TrimEnd('\\') + "\\"), "./" + cfg.FontFamily.Replace('\\', '/'));
                else Font = new FontFamily(cfg.FontFamily);
            }
            catch { Font = new FontFamily("Segoe UI"); }
        }

        public static SolidColorBrush B(string hex)
        {
            try
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                b.Freeze();
                return b;
            }
            catch { return new SolidColorBrush(Colors.Magenta); }
        }

        /// <summary>Текст с поддержкой **жирного** и переносов строк \n.</summary>
        public static TextBlock Text(string text, double size, FontWeight weight, Brush fg = null, TextAlignment align = TextAlignment.Left)
        {
            var tb = new TextBlock
            {
                FontFamily = Font,
                FontSize = size,
                FontWeight = weight,
                Foreground = fg ?? TextBrush,
                TextAlignment = align,
                TextWrapping = TextWrapping.Wrap
            };
            FillInlines(tb, text ?? "", weight);
            return tb;
        }

        public static void FillInlines(TextBlock tb, string text, FontWeight weight)
        {
            tb.Inlines.Clear();
            var lines = text.Replace("\r", "").Split('\n');
            bool bold = false;
            for (int li = 0; li < lines.Length; li++)
            {
                if (li > 0) tb.Inlines.Add(new LineBreak());
                var parts = lines[li].Split(new[] { "**" }, StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) bold = !bold;
                    if (parts[i].Length == 0) continue;
                    var r = new Run(parts[i]);
                    if (bold) r.FontWeight = FontWeights.Bold;
                    tb.Inlines.Add(r);
                }
            }
        }

        public static Tap Pill(string text, bool filled, double w, double h, double fontSize, Action onClick)
        {
            var t = new Tap { Width = w, Height = h, CornerRadius = new CornerRadius(h / 2) };
            var tb = Text(text, fontSize, FontWeights.Bold, null, TextAlignment.Center);
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.TextWrapping = TextWrapping.NoWrap;
            t.Child = tb;
            SetPill(t, filled);
            if (onClick != null) t.Click += onClick;
            return t;
        }

        public static void SetPill(Tap t, bool filled)
        {
            t.Background = filled ? Primary : FieldBg;
            t.BorderBrush = Primary;
            t.BorderThickness = new Thickness(filled ? 0 : 2);
            var tb = t.Child as TextBlock;
            if (tb != null) tb.Foreground = filled ? White : Primary;
        }

        public static void SetEnabledLook(Tap t, bool enabled)
        {
            t.IsEnabled = enabled;
            t.Opacity = enabled ? 1 : 0.45;
        }

        public static FrameworkElement Icon(string data, double size, Brush stroke = null, double thickness = 2)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Stroke = stroke ?? Primary,
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 24, Height = 24
            };
            var c = new Canvas { Width = 24, Height = 24 };
            c.Children.Add(p);
            return new Viewbox { Width = size, Height = size, Child = c, Stretch = Stretch.Uniform };
        }

        public const string IconUser = "M12 3.5a4 4 0 1 1 0 8a4 4 0 1 1 0-8z M5 20.5v-1.5a4 4 0 0 1 4-4h6a4 4 0 0 1 4 4v1.5z";
        public const string IconMail = "M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z M3.5 6.5l8.5 6.5 8.5-6.5";
        public const string IconPhone = "M21 16.4v2.8a1.9 1.9 0 0 1-2.1 1.9 18.8 18.8 0 0 1-8.2-2.9 18.5 18.5 0 0 1-5.7-5.7 18.8 18.8 0 0 1-2.9-8.2A1.9 1.9 0 0 1 4 2.2h2.8a1.9 1.9 0 0 1 1.9 1.6c.1.9.4 1.8.7 2.7a1.9 1.9 0 0 1-.4 2L7.7 9.7a15.2 15.2 0 0 0 5.7 5.7l1.2-1.2a1.9 1.9 0 0 1 2-.4c.9.3 1.8.6 2.7.7a1.9 1.9 0 0 1 1.7 1.9z";
        public const string IconChevronDown = "M5 9l7 5 7-5";
        public const string IconChevronUp = "M5 15l7-5 7 5";
        public const string IconPen = "M12 3.5H5.5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h13a2 2 0 0 0 2-2V12 M17.6 3.1a2.1 2.1 0 0 1 3 3L12 14.7l-4 1 1-4z";
        public const string IconCheck = "M5 12.5l4.5 4.5L19 7.5";

        public static ImageSource LoadImage(string path)
        {
            try
            {
                if (path == null || !File.Exists(path)) return null;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        public static FrameworkElement QrElement(string url, double size)
        {
            var qr = QrCode.Encode(url);
            int quiet = 2;
            int n = qr.Size + quiet * 2;
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            for (int y = 0; y < qr.Size; y++)
                for (int x = 0; x < qr.Size; x++)
                    if (qr.Get(x, y)) g.Children.Add(new RectangleGeometry(new Rect(x + quiet, y + quiet, 1.02, 1.02)));
            g.Freeze();
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, n, n))));
            dg.Children.Add(new GeometryDrawing(Brushes.Black, null, g));
            dg.Freeze();
            var img = new Image { Source = new DrawingImage(dg), Width = size, Height = size, Stretch = Stretch.Fill };
            RenderOptions.SetEdgeMode(img, EdgeMode.Aliased);
            return img;
        }

        public static bool IsInside<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T) return true;
                d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        public static bool IsInside(DependencyObject d, DependencyObject target)
        {
            while (d != null)
            {
                if (d == target) return true;
                d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        public static ScrollViewer Scroll(UIElement content)
        {
            var sv = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Focusable = false
            };
            return sv;
        }
    }

    /// <summary>Сенсорная «кнопка» на основе Border: нажатие с визуальным откликом, отмена при сдвиге пальца.</summary>
    public class Tap : Border
    {
        public event Action Click;
        public bool Dim = true;
        bool pressed;
        Point downPos;

        public Tap()
        {
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
            MouseLeftButtonDown += (s, e) =>
            {
                pressed = true;
                downPos = e.GetPosition(null);
                CaptureMouse();
                if (Dim) Opacity = 0.75;
                e.Handled = true;
            };
            MouseMove += (s, e) =>
            {
                if (!pressed) return;
                var p = e.GetPosition(null);
                if (Math.Abs(p.X - downPos.X) > 25 || Math.Abs(p.Y - downPos.Y) > 25) Cancel();
            };
            MouseLeftButtonUp += (s, e) =>
            {
                if (!pressed) return;
                pressed = false;
                ReleaseMouseCapture();
                Opacity = 1;
                var p = e.GetPosition(this);
                if (p.X >= 0 && p.Y >= 0 && p.X <= ActualWidth && p.Y <= ActualHeight && IsEnabled && Click != null) Click();
                e.Handled = true;
            };
            LostMouseCapture += (s, e) => { if (pressed) Cancel(); };
            IsEnabledChanged += (s, e) => { if (!IsEnabled) Cancel(); };
        }

        void Cancel()
        {
            pressed = false;
            Opacity = IsEnabled ? 1 : Opacity;
            if (IsMouseCaptured) ReleaseMouseCapture();
            if (IsEnabled) Opacity = 1;
        }
    }

    public enum KeyboardMode { Ru, En, Digits }

    /// <summary>
    /// Поле ввода для киоска: не использует системный TextBox (чтобы не всплывала системная клавиатура).
    /// Ввод — с экранной клавиатуры или с физической клавиатуры.
    /// </summary>
    public class InputField : Tap
    {
        public event Action Changed;
        public event Action<InputField> Activated;
        public KeyboardMode Keyboard = KeyboardMode.Ru;
        public bool AutoCapitalize;
        public bool Password;
        public int MaxLength = 120;
        public Func<string, string> Filter;      // нормализация «сырого» текста
        public Func<string, string> Formatter;   // отображение

        string raw = "";
        readonly TextBlock textBlock, placeholder, error;
        readonly Rectangle caret;
        readonly StackPanel line;
        readonly Grid clip;
        readonly Canvas lineHost;
        bool active;
        static DispatcherTimer blink;
        static event Action BlinkTick;

        public FrameworkElement ErrorLabel { get { return error; } }

        public InputField(string iconData, string placeholderText, double height = 108)
        {
            Height = height;
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(3);
            BorderBrush = Ui.Primary;
            Background = Ui.FieldBg;
            Cursor = Cursors.IBeam;
            Dim = false;

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconData != null ? 110 : 50) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            if (iconData != null)
            {
                var ic = Ui.Icon(iconData, 58, Ui.Primary, 1.8);
                ic.HorizontalAlignment = HorizontalAlignment.Left;
                ic.Margin = new Thickness(28, 0, 0, 0);
                g.Children.Add(ic);
            }
            clip = new Grid { ClipToBounds = true };
            Grid.SetColumn(clip, 1);
            g.Children.Add(clip);

            placeholder = Ui.Text(placeholderText ?? "", 40, FontWeights.SemiBold, Ui.Placeholder);
            placeholder.TextWrapping = TextWrapping.NoWrap;
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            clip.Children.Add(placeholder);

            line = new StackPanel { Orientation = Orientation.Horizontal };
            textBlock = Ui.Text("", 40, FontWeights.Medium);
            textBlock.TextWrapping = TextWrapping.NoWrap;
            caret = new Rectangle { Width = 3, Height = 50, Fill = Ui.Primary, Margin = new Thickness(2, 0, 0, 0), Visibility = Visibility.Hidden };
            line.Children.Add(textBlock);
            line.Children.Add(caret);
            lineHost = new Canvas();
            lineHost.Children.Add(line);
            clip.Children.Add(lineHost);
            clip.SizeChanged += (s, e) => FitLine();
            line.SizeChanged += (s, e) => FitLine();
            Child = g;

            error = Ui.Text("", 26, FontWeights.SemiBold, Ui.Wrong);
            error.Visibility = Visibility.Collapsed;

            if (blink == null)
            {
                blink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
                blink.Tick += (s, e) => { if (BlinkTick != null) BlinkTick(); };
                blink.Start();
            }
            BlinkTick += () => { if (active) caret.Visibility = caret.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible; };

            Click += () => { if (Activated != null) Activated(this); };
        }

        void FitLine()
        {
            Canvas.SetLeft(line, Math.Min(0, clip.ActualWidth - line.ActualWidth - 6));
            Canvas.SetTop(line, (clip.ActualHeight - line.ActualHeight) / 2);
        }

        public string Value
        {
            get { return raw; }
            set { raw = value ?? ""; Refresh(); }
        }

        public bool IsActive
        {
            get { return active; }
            set
            {
                active = value;
                caret.Visibility = active ? Visibility.Visible : Visibility.Hidden;
                BorderThickness = new Thickness(active ? 4 : 3);
                Refresh();
            }
        }

        public void Insert(string s)
        {
            string nv = raw + s;
            if (Filter != null) nv = Filter(nv);
            if (nv.Length > MaxLength) return;
            raw = nv;
            Refresh();
            SetError(null);
            if (Changed != null) Changed();
        }

        public void Backspace()
        {
            if (raw.Length == 0) return;
            raw = raw.Substring(0, raw.Length - 1);
            if (Filter != null) raw = Filter(raw);
            Refresh();
            if (Changed != null) Changed();
        }

        void Refresh()
        {
            string shown = Password ? new string('•', raw.Length) : (Formatter != null ? Formatter(raw) : raw);
            textBlock.Text = shown;
            placeholder.Visibility = shown.Length == 0 && !active ? Visibility.Visible : Visibility.Collapsed;
            if (shown.Length == 0 && active) placeholder.Visibility = Visibility.Collapsed;
        }

        public void SetError(string msg)
        {
            if (string.IsNullOrEmpty(msg))
            {
                error.Visibility = Visibility.Collapsed;
                BorderBrush = Ui.Primary;
            }
            else
            {
                error.Text = msg;
                error.Visibility = Visibility.Visible;
                BorderBrush = Ui.Wrong;
            }
        }
    }
}
