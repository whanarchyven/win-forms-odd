using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace KioskStand
{
    /// <summary>Скрытая панель организатора: регистрация, розыгрыш, выгрузка в Excel.</summary>
    public partial class MainWindow
    {
        void AskPin()
        {
            if (adminOpen) return;
            DeactivateField();
            var card = Card(1100);
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("Введите PIN организатора", 48, FontWeights.Bold, null, TextAlignment.Center));
            var pin = new InputField(null, "PIN") { Keyboard = KeyboardMode.Digits, Password = true, MaxLength = 12, Width = 600 };
            pin.Margin = new Thickness(0, 40, 0, 0);
            pin.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(pin);
            var err = Ui.Text("", 30, FontWeights.SemiBold, Ui.Wrong, TextAlignment.Center);
            err.Margin = new Thickness(0, 16, 0, 0);
            sp.Children.Add(err);
            var cancel = Ui.Pill("Отмена", false, 500, 104, 40, () => { DeactivateField(); CloseModal(); });
            cancel.Margin = new Thickness(0, 30, 0, 0);
            sp.Children.Add(cancel);
            card.Child = sp;
            card.VerticalAlignment = VerticalAlignment.Top;
            ShowModal(card);
            card.VerticalAlignment = VerticalAlignment.Top;
            card.Margin = new Thickness(0, 140, 0, 0);
            adminOpen = true; // не сбрасывать по бездействию, пока открыт ввод PIN

            Action check = () =>
            {
                if (pin.Value == cfg.AdminPin) { DeactivateField(); OpenAdmin(); }
                else if (pin.Value.Length > 0) { err.Text = "Неверный PIN"; pin.Value = ""; ActivateField(pin, null); }
            };
            pin.Changed += () =>
            {
                err.Text = "";
                if (pin.Value == cfg.AdminPin) { DeactivateField(); OpenAdmin(); }
            };
            pin.Activated += f => ActivateField(f, check);
            ActivateField(pin, check);
        }

        void OpenAdmin()
        {
            DeactivateField();
            CloseModal();
            adminOpen = true;
            var card = Card(1500);
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("Панель организатора", 56, FontWeights.Bold, null, TextAlignment.Center));

            bool open = store.RegistrationOpen;
            int winners = 0;
            foreach (var p in store.Participants) if (Json.Bool(p, "winner", false)) winners++;
            var info = string.Format("Зарегистрировано участников: **{0}**\nПрохождений задач: **{1}**\nРегистрация: **{2}**\nРозыгрыш: **{3}**",
                store.Participants.Count, store.Sessions.Count, open ? "открыта" : "закрыта",
                store.DrawAt == null ? "не проводился" : "проведён " + store.DrawAt + " (победителей: " + winners + ")");
            var infoTb = Ui.Text(info, 36, FontWeights.Normal, null, TextAlignment.Center);
            infoTb.LineHeight = 54;
            infoTb.Margin = new Thickness(0, 30, 0, 30);
            sp.Children.Add(infoTb);

            Func<string, bool, Action, Tap> btn = (text, filled, act) =>
            {
                var b = Ui.Pill(text, filled, 1100, 100, 38, act);
                b.Margin = new Thickness(0, 0, 0, 20);
                return b;
            };

            sp.Children.Add(btn(open ? "Закрыть регистрацию" : "Открыть регистрацию", true, () =>
            {
                try { store.SetRegistrationOpen(!open); }
                catch (Exception ex) { App.Log(ex.ToString()); Message("Ошибка записи файла: " + ex.Message, null, OpenAdmin); return; }
                OpenAdmin();
            }));

            sp.Children.Add(btn("Провести розыгрыш (" + cfg.WinnersCount + " победителей)", true, () =>
            {
                if (open) { Message("Сначала закройте регистрацию.", null, OpenAdmin); return; }
                if (store.Participants.Count == 0) { Message("Нет зарегистрированных участников.", null, OpenAdmin); return; }
                string q = store.DrawAt == null
                    ? "Провести розыгрыш среди " + store.Participants.Count + " участников?"
                    : "Розыгрыш уже проводился " + store.DrawAt + ".\nПровести заново? Прежние победители будут сброшены.";
                Confirm(q, "Провести", "Отмена", () =>
                {
                    try { store.Draw(cfg.WinnersCount); ShowWinners(); }
                    catch (Exception ex) { App.Log(ex.ToString()); Message("Ошибка: " + ex.Message, null, OpenAdmin); }
                });
                adminOpen = true;
            }));

            if (store.DrawAt != null) sp.Children.Add(btn("Показать победителей", false, ShowWinners));

            sp.Children.Add(btn("Выгрузить в Excel (.xlsx)", false, () =>
            {
                try
                {
                    string dir = Path.Combine(cfg.BaseDir, "exports");
                    Directory.CreateDirectory(dir);
                    string path = store.ExportXlsx(dir, cfg);
                    Message("Файл сохранён:\n" + path, null, OpenAdmin);
                }
                catch (Exception ex) { App.Log(ex.ToString()); Message("Ошибка выгрузки: " + ex.Message, null, OpenAdmin); }
                adminOpen = true;
            }));

            sp.Children.Add(btn("Открыть папку с данными", false, () =>
            {
                try { Process.Start("explorer.exe", "\"" + cfg.BaseDir + "\""); } catch { }
            }));

            sp.Children.Add(btn("Выйти из программы", false, () =>
            {
                Confirm("Закрыть приложение стенда?", "Выйти", "Отмена", () => Application.Current.Shutdown());
                adminOpen = true;
            }));

            var close = btn("Закрыть панель", false, () => { CloseModal(); ShowStart(); });
            close.Margin = new Thickness(0, 20, 0, 0);
            sp.Children.Add(close);

            card.Child = sp;
            ShowModal(card);
            adminOpen = true;
        }

        void ShowWinners()
        {
            adminOpen = true;
            var card = Card(1900);
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("Победители розыгрыша", 52, FontWeights.Bold, null, TextAlignment.Center));
            var list = new StackPanel { Margin = new Thickness(0, 30, 0, 0) };
            int n = 0;
            foreach (var p in store.Participants)
            {
                if (!Json.Bool(p, "winner", false)) continue;
                n++;
                list.Children.Add(Ui.Text(n + ". **" + Json.Str(p, "fullName", "") + "**   " + Json.Str(p, "phone", "") + "   " + Json.Str(p, "email", ""), 32, FontWeights.Normal));
            }
            var sv = Ui.Scroll(list);
            sv.MaxHeight = 900;
            sp.Children.Add(sv);
            var back = Ui.Pill("Назад", true, 600, 104, 40, OpenAdmin);
            back.Margin = new Thickness(0, 30, 0, 0);
            sp.Children.Add(back);
            card.Child = sp;
            ShowModal(card);
            adminOpen = true;
        }
    }
}
