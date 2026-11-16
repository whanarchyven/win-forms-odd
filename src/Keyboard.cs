using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace KioskStand
{
    /// <summary>Экранная клавиатура (RU / EN / символы / цифры) для сенсорного стенда.</summary>
    public class OnScreenKeyboard : Border
    {
        public event Action Done;
        InputField target;
        KeyboardMode mode = KeyboardMode.Ru;
        bool symbols, shift;
        readonly StackPanel rows = new StackPanel();

        static readonly string[] Ru = { "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю" };
        static readonly string[] En = { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        static readonly string[] Sym = { "1234567890", "-_.,@'\"()+", "!?#/:;&%" };

        const double KeyH = 104, Gap = 12, Unit = 128;

        public OnScreenKeyboard()
        {
            Background = Ui.B("#F7FFFFFF");
            CornerRadius = new CornerRadius(36, 36, 0, 0);
            Padding = new Thickness(30, 30, 30, 36);
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Bottom;
            Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 0, Opacity = 0.25, Color = Colors.Black };
            Child = rows;
            Visibility = Visibility.Collapsed;
        }

        public InputField Target { get { return target; } }

        public void Attach(InputField field)
        {
            if (target != null) target.Changed -= OnTargetChanged;
            target = field;
            target.Changed += OnTargetChanged;
            mode = field.Keyboard;
            symbols = false;
            AutoShift();
            Build();
            Visibility = Visibility.Visible;
        }

        void OnTargetChanged()
        {
            if (target == null || !target.AutoCapitalize || symbols) return;
            bool was = shift;
            AutoShift();
            if (was != shift) Build();
        }

        public void Detach()
        {
            if (target != null) target.Changed -= OnTargetChanged;
            target = null;
            Visibility = Visibility.Collapsed;
        }

        void AutoShift()
        {
            shift = false;
            if (target != null && target.AutoCapitalize)
            {
                string v = target.Value;
                shift = v.Length == 0 || v.EndsWith(" ") || v.EndsWith("-");
            }
        }

        void Build()
        {
            rows.Children.Clear();
            if (mode == KeyboardMode.Digits) { BuildDigits(); return; }

            string[] layout = symbols ? Sym : (mode == KeyboardMode.En ? En : Ru);
            for (int r = 0; r < layout.Length; r++)
            {
                var row = Row();
                if (r == 2 && !symbols) row.Children.Add(Key(shift ? "⇧" : "⇪", 1.5, true, () => { shift = !shift; Build(); }, shift));
                foreach (char ch in layout[r])
                {
                    string s = ch.ToString();
                    if (shift && !symbols) s = s.ToUpperInvariant();
                    string ins = s;
                    row.Children.Add(Key(s, 1, false, () => Type(ins), false));
                }
                if (r == 2) row.Children.Add(Key("⌫", 1.5, true, Back, false));
                rows.Children.Add(row);
            }
            var last = Row();
            last.Children.Add(Key(symbols ? (mode == KeyboardMode.En ? "ABC" : "АБВ") : "123", 1.5, true, () => { symbols = !symbols; Build(); }, false));
            if (!symbols)
                last.Children.Add(Key(mode == KeyboardMode.En ? "RU" : "EN", 1.3, true, () => { mode = mode == KeyboardMode.En ? KeyboardMode.Ru : KeyboardMode.En; Build(); }, false));
            last.Children.Add(Key("@", 1, false, () => Type("@"), false));
            last.Children.Add(Key("", 5, false, () => Type(" "), false));
            last.Children.Add(Key(".", 1, false, () => Type("."), false));
            if (target != null && target.Keyboard == KeyboardMode.En)
            {
                last.Children.Add(Key(".ru", 1.3, false, () => Type(".ru"), false));
                last.Children.Add(Key(".com", 1.5, false, () => Type(".com"), false));
            }
            last.Children.Add(DoneKey(2.2));
            rows.Children.Add(last);
        }

        void BuildDigits()
        {
            string[] d = { "123", "456", "789" };
            foreach (var line in d)
            {
                var row = Row();
                foreach (char c in line) { string s = c.ToString(); row.Children.Add(Key(s, 1.6, false, () => Type(s), false)); }
                rows.Children.Add(row);
            }
            var last = Row();
            last.Children.Add(Key("⌫", 1.6, true, Back, false));
            last.Children.Add(Key("0", 1.6, false, () => Type("0"), false));
            last.Children.Add(DoneKey(1.6));
            rows.Children.Add(last);
        }

        StackPanel Row()
        {
            return new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, Gap) };
        }

        Tap Key(string label, double units, bool special, Action act, bool highlighted)
        {
            var t = new Tap
            {
                Width = Unit * units + Gap * (units - 1),
                Height = KeyH,
                Margin = new Thickness(Gap / 2, 0, Gap / 2, 0),
                CornerRadius = new CornerRadius(18),
                Background = highlighted ? Ui.Accent : (special ? Ui.Soft : Brushes.White),
                BorderBrush = Ui.Primary,
                BorderThickness = new Thickness(2)
            };
            var tb = Ui.Text(label, label.Length > 2 ? 36 : 46, FontWeights.SemiBold, Ui.TextBrush, TextAlignment.Center);
            tb.TextWrapping = TextWrapping.NoWrap;
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            t.Child = tb;
            t.Click += act;
            return t;
        }

        Tap DoneKey(double units)
        {
            var t = Key(Ui.Cfg.T("keyboard.done"), units, true, () => { if (Done != null) Done(); }, false);
            t.Background = Ui.Primary;
            ((TextBlock)t.Child).Foreground = Brushes.White;
            return t;
        }

        void Type(string s)
        {
            if (target == null) return;
            target.Insert(s);
            bool wasShift = shift;
            if (shift && !symbols && !(target.AutoCapitalize)) shift = false;
            if (target.AutoCapitalize) AutoShift();
            if (wasShift != shift) Build();
        }

        void Back()
        {
            if (target == null) return;
            target.Backspace();
            bool wasShift = shift;
            if (target.AutoCapitalize) AutoShift();
            if (wasShift != shift) Build();
        }
    }
}
