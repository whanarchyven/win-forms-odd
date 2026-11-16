using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KioskStand
{
    public partial class MainWindow
    {
        static string PhoneFilter(string v)
        {
            var d = new string(v.Where(char.IsDigit).ToArray());
            if (d.Length > 0 && d[0] == '8') d = "7" + d.Substring(1);
            else if (d.Length > 0 && d[0] == '9') d = "7" + d;
            if (d.Length > 11) d = d.Substring(0, 11);
            return d;
        }

        static string PhoneFormat(string d)
        {
            if (d.Length == 0) return "";
            var sb = new StringBuilder("+" + d[0]);
            if (d.Length > 1) sb.Append(" (").Append(d.Substring(1, Math.Min(3, d.Length - 1)));
            if (d.Length >= 4) sb.Append(")");
            if (d.Length > 4) sb.Append(" ").Append(d.Substring(4, Math.Min(3, d.Length - 4)));
            if (d.Length > 7) sb.Append("-").Append(d.Substring(7, Math.Min(2, d.Length - 7)));
            if (d.Length > 9) sb.Append("-").Append(d.Substring(9, Math.Min(2, d.Length - 9)));
            return sb.ToString();
        }

        static string NameFilter(string v)
        {
            v = v.TrimStart();
            while (v.Contains("  ")) v = v.Replace("  ", " ");
            return v;
        }

        static string Collapse(string v) { return Regex.Replace((v ?? "").Trim(), @"\s+", " "); }

        void ShowForm()
        {
            if (!store.RegistrationOpen) { ShowDone(); return; }

            var g = new Grid();
            const double L = 170, R = 1295, W = 1095;

            g.Children.Add(Place(Ui.Text(cfg.T("form.fio"), 32, FontWeights.Bold), L, 232));
            g.Children.Add(Place(Ui.Text(cfg.T("form.email"), 32, FontWeights.Bold), R, 232));
            g.Children.Add(Place(Ui.Text(cfg.T("form.phone"), 32, FontWeights.Bold), L, 414));
            g.Children.Add(Place(Ui.Text(cfg.T("form.spec"), 32, FontWeights.Bold), R, 414));

            var fio = new InputField(Ui.IconUser, null) { Keyboard = KeyboardMode.Ru, AutoCapitalize = true, Filter = NameFilter, MaxLength = 150 };
            var email = new InputField(Ui.IconMail, null) { Keyboard = KeyboardMode.En, Filter = v => v.Replace(" ", ""), MaxLength = 120 };
            var phone = new InputField(Ui.IconPhone, null) { Keyboard = KeyboardMode.Digits, Filter = PhoneFilter, Formatter = PhoneFormat };
            var other = new InputField(Ui.IconPen, cfg.T("form.specOther")) { Keyboard = KeyboardMode.Ru, Filter = NameFilter, MaxLength = 120 };
            fio.Width = email.Width = phone.Width = other.Width = W;
            g.Children.Add(Place(fio, L, 280));
            g.Children.Add(Place(email, R, 280));
            g.Children.Add(Place(phone, L, 463));
            g.Children.Add(Place(other, R, 610));
            // ошибки — справа на строке подписи поля, чтобы не наезжать на соседние элементы
            Action<FrameworkElement, double, double> placeErr = (e, x, y) => { Place(e, x, y); e.Width = W; ((TextBlock)e).TextAlignment = TextAlignment.Right; g.Children.Add(e); };
            placeErr(fio.ErrorLabel, L, 238);
            placeErr(email.ErrorLabel, R, 238);
            placeErr(phone.ErrorLabel, L, 420);
            placeErr(other.ErrorLabel, R, 722);
            other.Visibility = Visibility.Collapsed;

            var fields = new[] { fio, email, phone, other };
            foreach (var f in fields) f.Activated += x => { CloseDropdownIfOpen(); ActivateField(x); };

            // --- специализация: выпадающий список ---
            string spec = null;
            var specBox = new Tap { Width = W, Height = 108, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(3), BorderBrush = Ui.Primary, Background = Ui.FieldBg, Dim = false };
            var specGrid = new Grid();
            var specText = Ui.Text("", 40, FontWeights.Bold);
            specText.VerticalAlignment = VerticalAlignment.Center;
            specText.TextWrapping = TextWrapping.NoWrap;
            specText.TextTrimming = TextTrimming.CharacterEllipsis;
            specText.Margin = new Thickness(50, 0, 120, 0);
            specGrid.Children.Add(specText);
            var chevronHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 30, 0), Focusable = false };
            chevronHost.Content = Ui.Icon(Ui.IconChevronDown, 70, Ui.Primary, 1.6);
            specGrid.Children.Add(chevronHost);
            specBox.Child = specGrid;
            g.Children.Add(Place(specBox, R, 463));
            var specError = Place(Ui.Text(cfg.T("err.spec"), 26, FontWeights.SemiBold, Ui.Wrong, TextAlignment.Right), R, 420);
            specError.Width = W;
            specError.Visibility = Visibility.Collapsed;
            g.Children.Add(specError);

            var list = new StackPanel();
            var dropdown = new Border
            {
                Width = W, Background = Brushes.White, BorderBrush = Ui.Primary, BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(10), Padding = new Thickness(32, 30, 32, 30), Child = list, Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(dropdown, 50);
            g.Children.Add(Place(dropdown, R, 597));

            Action<bool> setDropdown = open =>
            {
                dropdown.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                chevronHost.Content = Ui.Icon(open ? Ui.IconChevronUp : Ui.IconChevronDown, 70, Ui.Primary, 1.6);
            };
            closeDropdown = () => setDropdown(false);

            Action rebuildList = null;
            rebuildList = () =>
            {
                list.Children.Clear();
                foreach (var s in cfg.Specializations)
                {
                    string val = s;
                    var item = new Tap { Height = 90, CornerRadius = new CornerRadius(6), Padding = new Thickness(20, 0, 20, 0), Dim = false };
                    item.Background = val == spec ? Ui.Soft : Brushes.Transparent;
                    var tb = Ui.Text(val, 40, FontWeights.Bold);
                    tb.VerticalAlignment = VerticalAlignment.Center;
                    tb.TextWrapping = TextWrapping.NoWrap;
                    item.Child = tb;
                    item.Click += () =>
                    {
                        spec = val;
                        specText.Text = val;
                        specError.Visibility = Visibility.Collapsed;
                        specBox.BorderBrush = Ui.Primary;
                        bool isOther = string.Equals(val, cfg.OtherSpecialization, StringComparison.OrdinalIgnoreCase);
                        other.Visibility = isOther ? Visibility.Visible : Visibility.Collapsed;
                        if (!isOther) { other.Value = ""; other.SetError(null); }
                        setDropdown(false);
                        rebuildList();
                        if (isOther) ActivateField(other);
                    };
                    list.Children.Add(item);
                }
            };
            rebuildList();
            specBox.Click += () =>
            {
                DeactivateField();
                setDropdown(dropdown.Visibility != Visibility.Visible);
            };

            // --- вопрос о сотрудничестве ---
            bool? coop = null;
            var chosenFormats = new List<string>();
            var bottom = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20, 0, 20, 90) };
            var q = Ui.Text(cfg.T("form.coopQuestion"), 52, FontWeights.Bold, null, TextAlignment.Center);
            q.Width = 2300;
            bottom.Children.Add(q);
            var yn = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
            var yes = Ui.Pill(cfg.T("form.yes"), false, 615, 128, 64, null);
            var no = Ui.Pill(cfg.T("form.no"), false, 615, 128, 64, null);
            no.Margin = new Thickness(60, 0, 0, 0);
            yn.Children.Add(yes); yn.Children.Add(no);
            bottom.Children.Add(yn);

            var coopError = Ui.Text(cfg.T("err.coop"), 30, FontWeights.SemiBold, Ui.Wrong, TextAlignment.Center);
            coopError.Margin = new Thickness(0, 18, 0, 0);
            coopError.Visibility = Visibility.Collapsed;
            bottom.Children.Add(coopError);

            var chips = new StackPanel { Margin = new Thickness(0, 28, 0, 0), Visibility = Visibility.Collapsed };
            BuildChips(chips, chosenFormats);
            bottom.Children.Add(chips);

            Tap submit = null;
            submit = Ui.Pill(cfg.T("form.submit"), true, 925, 128, 64, null);
            submit.Margin = new Thickness(0, 140, 0, 0);
            bottom.Children.Add(submit);
            g.Children.Add(bottom);

            Action refreshCoop = () =>
            {
                Ui.SetPill(yes, coop == true);
                Ui.SetPill(no, coop == false);
                chips.Visibility = coop == true ? Visibility.Visible : Visibility.Collapsed;
                submit.Margin = new Thickness(0, coop == true ? 32 : (coopError.Visibility == Visibility.Visible ? 70 : 140), 0, 0);
            };
            yes.Click += () => { coop = true; coopError.Visibility = Visibility.Collapsed; refreshCoop(); };
            no.Click += () => { coop = false; chosenFormats.Clear(); BuildChips(chips, chosenFormats); coopError.Visibility = Visibility.Collapsed; refreshCoop(); };

            // закрытие выпадающего списка по касанию вне него
            onStageTapForm = src =>
            {
                if (dropdown.Visibility == Visibility.Visible && !Ui.IsInside(src, dropdown) && !Ui.IsInside(src, specBox)) setDropdown(false);
            };

            bool busy = false;
            submit.Click += () =>
            {
                if (busy) return;
                DeactivateField();
                setDropdown(false);

                bool valid = true;
                string fioV = Collapse(fio.Value);
                if (fioV.Length < 2) { fio.SetError(cfg.T("err.required")); valid = false; }
                string emailV = email.Value.Trim();
                if (emailV.Length == 0) { email.SetError(cfg.T("err.required")); valid = false; }
                else if (!Regex.IsMatch(emailV, @"^[^@\s]+@[^@\s]+\.[^@\s.]{2,}$")) { email.SetError(cfg.T("err.email")); valid = false; }
                string phoneN = Storage.NormalizePhone(phone.Value);
                if (phone.Value.Length == 0) { phone.SetError(cfg.T("err.required")); valid = false; }
                else if (phoneN.Length != 11 || phoneN[0] != '7') { phone.SetError(cfg.T("err.phone")); valid = false; }
                bool isOther = spec != null && string.Equals(spec, cfg.OtherSpecialization, StringComparison.OrdinalIgnoreCase);
                if (spec == null) { specError.Visibility = Visibility.Visible; specBox.BorderBrush = Ui.Wrong; valid = false; }
                else if (isOther && Collapse(other.Value).Length == 0) { other.SetError(cfg.T("err.required")); valid = false; }
                if (coop == null) { coopError.Visibility = Visibility.Visible; refreshCoop(); valid = false; }
                if (!valid) return;

                if (!store.RegistrationOpen) { Message(cfg.T("err.closed"), null, ShowDone); return; }
                if (store.PhoneExists(phoneN)) { Message(cfg.T("err.duplicate"), null); return; }

                var p = new Dictionary<string, object>();
                p["id"] = Guid.NewGuid().ToString("N").Substring(0, 10);
                p["registeredAt"] = Storage.Now();
                p["sessionId"] = sessionId;
                p["score"] = scoreTotal > 0 ? scoreCorrect + " из " + scoreTotal : "";
                p["fullName"] = fioV;
                p["phone"] = PhoneFormat(phoneN);
                p["phoneNormalized"] = phoneN;
                p["email"] = emailV;
                p["specialization"] = spec;
                p["specializationOther"] = isOther ? Collapse(other.Value) : "";
                p["cooperation"] = coop == true ? cfg.T("form.yes") : cfg.T("form.no");
                p["cooperationFormats"] = chosenFormats.Cast<object>().ToList();
                p["winner"] = false;

                busy = true;
                try
                {
                    store.AddParticipant(p);
                }
                catch (Exception ex)
                {
                    busy = false;
                    App.Log("Ошибка сохранения анкеты: " + ex);
                    Message(cfg.T("err.save"), null);
                    return;
                }
                ShowFinal();
            };

            SetScreen("form", g);
            onStageTap = src => { if (onStageTapForm != null) onStageTapForm(src); };
        }

        Action closeDropdown;
        Action<DependencyObject> onStageTapForm;

        void CloseDropdownIfOpen() { if (closeDropdown != null) closeDropdown(); }

        /// <summary>Раскладывает «чипсы» форматов сотрудничества по строкам с центрированием.</summary>
        void BuildChips(StackPanel host, List<string> chosen)
        {
            host.Children.Clear();
            const double maxRow = 2520, gap = 23, fs = 30, padH = 26;
            var typeface = new Typeface(Ui.Font, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            StackPanel row = null;
            double rowW = 0;
            foreach (var f in cfg.CooperationFormats)
            {
                string val = f;
                var ft = new FormattedText(val, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fs, Brushes.Black, 1.0);
                double w = Math.Ceiling(ft.Width) + padH * 2 + 8;
                if (row == null || rowW + gap + w > maxRow)
                {
                    row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, host.Children.Count == 0 ? 0 : 22, 0, 0) };
                    host.Children.Add(row);
                    rowW = 0;
                }
                var chip = new Tap
                {
                    Width = w, Height = 84, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(3), BorderBrush = Ui.Primary,
                    Margin = new Thickness(row.Children.Count == 0 ? 0 : gap, 0, 0, 0), Dim = false
                };
                var tb = Ui.Text(val, fs, FontWeights.SemiBold, null, TextAlignment.Center);
                tb.TextWrapping = TextWrapping.NoWrap;
                tb.VerticalAlignment = VerticalAlignment.Center;
                chip.Child = tb;
                Action paint = () => { bool on = chosen.Contains(val); chip.Background = on ? Ui.Accent : Ui.FieldBg; tb.Foreground = on ? Ui.SelectedText : Ui.TextBrush; };
                paint();
                chip.Click += () => { if (chosen.Contains(val)) chosen.Remove(val); else chosen.Add(val); paint(); };
                row.Children.Add(chip);
                rowW += (row.Children.Count == 1 ? 0 : gap) + w;
            }
        }

        // ---------------- финальный экран ----------------

        void ShowFinal()
        {
            var g = new Grid();
            var title = CenterAt(Ui.Text(cfg.T("final.title"), 84, FontWeights.Bold, null, TextAlignment.Center), 250);
            title.Width = 2100;
            g.Children.Add(title);
            var sub = CenterAt(Ui.Text(cfg.T("final.subtitle"), 52, FontWeights.Normal, null, TextAlignment.Center), 375);
            sub.Width = 2100;
            g.Children.Add(sub);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < cfg.Links.Count; i++)
            {
                var l = cfg.Links[i];
                var col = new StackPanel { Margin = new Thickness(i == 0 ? 0 : cfg.LinksPadding, 0, 0, 0) };
                FrameworkElement qr = null;
                var custom = Ui.LoadImage(cfg.Resolve(l.QrImage));
                if (custom != null) qr = new Image { Source = custom, Width = cfg.QrSize, Height = cfg.QrSize, Stretch = Stretch.Uniform };
                else if (!string.IsNullOrEmpty(l.Url))
                {
                    try { qr = Ui.QrElement(l.Url, cfg.QrSize); }
                    catch (Exception ex) { App.Log("QR: " + ex.Message); }
                }
                if (qr != null) col.Children.Add(new Border { Background = Brushes.White, Child = qr, HorizontalAlignment = HorizontalAlignment.Center });
                var lbl = Ui.Text(l.Label, 40, FontWeights.Bold, null, TextAlignment.Center);
                lbl.Margin = new Thickness(0, 34, 0, 0);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                col.Children.Add(lbl);
                row.Children.Add(col);
            }
            g.Children.Add(CenterAt(row, 590));

            var note = CenterAt(Ui.Text(cfg.T("final.note"), 48, FontWeights.Normal, null, TextAlignment.Center), 1100);
            note.Width = 2200;
            g.Children.Add(note);
            g.Children.Add(CenterAt(Ui.Pill(cfg.T("final.button"), true, 925, 128, 64, ShowStart), 1215));
            SetScreen("final", g);
        }
    }
}
