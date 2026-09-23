using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;

namespace StyleChanger
{
    /// <summary>
    /// UI: lista stylów (domyślny + możliwość wyboru innego), jeden przycisk,
    /// podpis stanu, log. Wyzwalacz: operator zaznacza widok (albo obiekt w
    /// widoku, np. wymiar) w edytorze Tekli, potem klika przycisk - appka
    /// sama znajduje widok z zaznaczenia i podmienia mu styl.
    /// </summary>
    public class MainForm : Form
    {
        private readonly StyleChangerService _service = new StyleChangerService();
        private bool _busy;

        private Button _runButton;
        private TextBox _logBox;
        private Label _statusLabel;
        private ComboBox _styleCombo;

        public MainForm()
        {
            Text = "Style Changer – Tekla 2025";
            Width = 520;
            Height = 400;
            StartPosition = FormStartPosition.CenterScreen;

            _styleCombo = new ComboBox
            {
                Left = 15,
                Top = 15,
                Width = 470,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            _runButton = new Button
            {
                Text = "Pokaż sąsiadów",
                Left = 15,
                Top = 45,
                Width = 470,
                Height = 40
            };
            _runButton.Click += RunButton_Click;

            _statusLabel = new Label
            {
                Left = 15,
                Top = 94,
                Width = 470,
                Height = 20,
                ForeColor = Color.DarkSlateGray
            };

            _logBox = new TextBox
            {
                Left = 15,
                Top = 119,
                Width = 470,
                Height = 220,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            Controls.Add(_styleCombo);
            Controls.Add(_runButton);
            Controls.Add(_statusLabel);
            Controls.Add(_logBox);

            Activated += (s, e) => RefreshState();

            Log($"===== Start sesji {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
            RefreshState();
        }

        private void RefreshState()
        {
            if (_busy) return;
            try
            {
                var dh = new DrawingHandler();
                if (!dh.GetConnectionStatus())
                {
                    _statusLabel.Text = "Brak połączenia z Teklą.";
                    return;
                }
                var drawing = dh.GetActiveDrawing();
                _statusLabel.Text = drawing != null
                    ? $"Aktywny rysunek: {drawing.Mark} / {drawing.Name}"
                    : "Brak otwartego rysunku.";

                RefreshStyleList();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Brak kontaktu z Teklą (" + ex.GetType().Name + ").";
            }
        }

        private void RefreshStyleList()
        {
            if (_styleCombo.Items.Count > 0)
            {
                return; // wczytane raz na sesję - lista stylów na dysku się w trakcie pracy nie zmienia
            }

            var model = new Model();
            if (!model.GetConnectionStatus())
            {
                return;
            }

            var modelPath = model.GetInfo().ModelPath;
            var styles = _service.GetAvailableStyles(modelPath).Keys.OrderBy(k => k).ToArray();
            _styleCombo.Items.AddRange(styles.Cast<object>().ToArray());

            var defaultIndex = Array.IndexOf(styles, StyleChangerService.DefaultStyleName);
            _styleCombo.SelectedIndex = defaultIndex >= 0 ? defaultIndex : (styles.Length > 0 ? 0 : -1);
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;

            var styleName = _styleCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(styleName))
            {
                Log("Nie wybrano stylu z listy.");
                return;
            }

            _logBox.Clear();
            Log($"===== {DateTime.Now:HH:mm:ss} POKAŻ SĄSIADÓW ({styleName}) =====");
            _busy = true;
            _runButton.Enabled = false;

            try
            {
                var dh = new DrawingHandler();
                if (!dh.GetConnectionStatus())
                {
                    SetResult("Brak połączenia z Teklą.");
                    return;
                }

                var drawing = dh.GetActiveDrawing();
                if (drawing == null)
                {
                    SetResult("Brak otwartego rysunku.");
                    return;
                }

                var selected = dh.GetDrawingObjectSelector().GetSelected();
                var view = _service.ResolveTargetView(selected);
                if (view == null)
                {
                    SetResult("Zaznacz widok (albo obiekt w widoku) w Tekli i spróbuj ponownie.");
                    return;
                }

                var ok = _service.ApplyStyle(drawing, view, styleName, Log);
                SetResult(ok
                    ? $"Gotowe - zastosowano styl \"{styleName}\"."
                    : "Błąd - zobacz log.");
            }
            catch (Exception ex)
            {
                SetResult("Błąd – zobacz log.");
                Log("BŁĄD: " + ex.Message);
                Log(ex.StackTrace);
            }
            finally
            {
                _busy = false;
                _runButton.Enabled = true;
            }
        }

        // Etykieta statusu bywa nadpisywana z powrotem na ogólny opis
        // rysunku przez RefreshState() spięte z Activated - w tej sesji
        // zaobserwowane, że okno "aktywuje się" ponownie tuż po kliknięciu
        // (prawdopodobnie Tekla na chwilę bierze fokus przy CommitChanges).
        // Wynik operacji musi więc ZAWSZE trafić też do logu, bo tylko log
        // jest pewnym źródłem prawdy - etykieta jest tylko wygodą.
        private void SetResult(string message)
        {
            _statusLabel.Text = message;
            Log(message);
        }

        private void Log(string message)
        {
            _logBox.AppendText(message + Environment.NewLine);
        }
    }
}
