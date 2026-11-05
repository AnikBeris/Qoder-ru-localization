using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QoderRuSetup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "--install" || args[0] == "--rollback"))
            {
                string dir = args.Length > 1 ? args[1] : "";
                string logPath = args.Length > 3 && args[2] == "--log" ? args[3] : Path.Combine(Path.GetTempPath(), "qoder-ru-install.log");
                var sw = new StreamWriter(logPath, false, new UTF8Encoding(false));
                try
                {
                    sw.WriteLine("mode: " + args[0]);
                    sw.WriteLine("dir: " + dir);
                    Action<string> log = s => { sw.WriteLine(s); sw.Flush(); };
                    if (args[0] == "--install") Patcher.Install(dir, log);
                    else Patcher.Rollback(dir, log);
                    log("RESULT: OK");
                    sw.Close();
                    return 0;
                }
                catch (Exception ex)
                {
                    sw.WriteLine("RESULT: FAIL");
                    sw.WriteLine(ex.ToString());
                    sw.Close();
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
            return 0;
        }
    }

    internal sealed class SetupForm : Form
    {
        private const string ReleasesPage = "https://github.com/AnikBeris/Qoder-ru-localization/releases";

        private readonly TextBox _path = new TextBox();
        private readonly TextBox _log = new TextBox();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Button _install = new Button();
        private readonly Button _rollback = new Button();
        private readonly Button _browse = new Button();
        private readonly Label _status = new Label();
        private readonly LinkLabel _update = new LinkLabel();
        private string _updateUrl = ReleasesPage;
        private bool _busy;

        public SetupForm()
        {
            string version = null;
            try { version = Patcher.TargetVersion(); } catch { }
            string suffix = string.IsNullOrEmpty(version) ? "" : " " + version;

            Text = "Qoder RU" + suffix + " — установка русского языка";
            ClientSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(560, 420);
            Font = new Font("Segoe UI", 9f);

            var caption = new Label
            {
                Text = "Русификатор Qoder" + suffix,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 14),
            };
            var hint = new Label
            {
                Text = "Папка установки Qoder (содержит Qoder.exe и resources\\app.asar):",
                AutoSize = true,
                Location = new Point(18, 52),
            };
            _browse.Text = "Директория";
            _browse.Location = new Point(20, 72);
            _browse.Width = 120;
            _browse.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _browse.Click += (s, e) => Browse();

            _path.Location = new Point(150, 74);
            _path.Width = ClientSize.Width - 150 - 20;
            _path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _install.Text = "Установить русский";
            _install.Location = new Point(20, 112);
            _install.Size = new Size(180, 34);
            _install.Click += (s, e) => Run(true);

            _rollback.Text = "Откатить";
            _rollback.Location = new Point(212, 112);
            _rollback.Size = new Size(120, 34);
            _rollback.Click += (s, e) => Run(false);

            _status.Text = "Готово к установке.";
            _status.AutoSize = true;
            _status.Location = new Point(20, 158);

            _update.Text = "Проверка обновлений…";
            _update.AutoSize = true;
            _update.Location = new Point(20, 178);
            _update.LinkClicked += (s, e) => OpenLink(_updateUrl);

            _progress.Location = new Point(20, 202);
            _progress.Size = new Size(598, 16);
            _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _progress.Style = ProgressBarStyle.Continuous;

            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.Location = new Point(20, 230);
            _log.Size = new Size(598, 230);
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _log.BackColor = Color.White;
            _log.Font = new Font("Consolas", 9f);

            var credit = new LinkLabel
            {
                Text = "Разработчик: AnikBeris",
                AutoSize = true,
                LinkColor = Color.RoyalBlue,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            credit.Location = new Point(ClientSize.Width - credit.PreferredWidth - 20, 20);
            credit.LinkClicked += (s, e) => OpenLink("https://github.com/AnikBeris");

            Controls.Add(caption);
            Controls.Add(hint);
            Controls.Add(credit);
            Controls.Add(_path);
            Controls.Add(_browse);
            Controls.Add(_install);
            Controls.Add(_rollback);
            Controls.Add(_status);
            Controls.Add(_update);
            Controls.Add(_progress);
            Controls.Add(_log);

            Shown += (s, e) => new Thread(CheckUpdates) { IsBackground = true }.Start();

            string detected = Patcher.AutoDetect();
            if (!string.IsNullOrEmpty(detected))
            {
                _path.Text = detected;
                Append("Обнаружен Qoder: " + detected);
            }
            else
            {
                Append("Qoder не найден автоматически. Укажите папку установки вручную.");
            }
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Выберите папку установки Qoder";
                if (Directory.Exists(_path.Text)) dlg.SelectedPath = _path.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _path.Text = dlg.SelectedPath;
                    Append("Выбрана папка: " + dlg.SelectedPath);
                }
            }
        }

        private static void OpenLink(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        private void CheckUpdates()
        {
            string current = null;
            try { current = Patcher.TargetVersion(); } catch { }

            string latestTag = null;
            string releaseUrl = ReleasesPage;
            try
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
                var request = (HttpWebRequest)WebRequest.Create(ReleasesPage + "/latest");
                request.AllowAutoRedirect = false;
                request.UserAgent = "QoderRU-Setup";
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    string location = response.Headers["Location"];
                    if (!string.IsNullOrEmpty(location))
                    {
                        int idx = location.IndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            latestTag = Uri.UnescapeDataString(location.Substring(idx + 5).TrimEnd('/'));
                            releaseUrl = location;
                        }
                    }
                }
            }
            catch { }

            if (latestTag == null)
            {
                ShowUpdate("Не удалось проверить обновления — страница релизов", ReleasesPage, true);
                return;
            }

            string latest = latestTag.TrimStart('v', 'V');
            if (current == null)
                ShowUpdate("Последняя версия русификатора: " + latestTag, releaseUrl, true);
            else if (CompareVersions(latest, current) > 0)
                ShowUpdate("Доступна новая версия: " + latestTag + " — скачать", releaseUrl, true);
            else
                ShowUpdate("Установлена последняя версия (" + latestTag + ")", ReleasesPage, false);
        }

        private void ShowUpdate(string text, string url, bool clickable)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string, string, bool>(ShowUpdate), text, url, clickable); return; }
            _updateUrl = url;
            _update.Text = text;
            _update.Enabled = clickable;
            _update.LinkColor = clickable ? Color.RoyalBlue : SystemColors.GrayText;
            Append(text);
        }

        private static int CompareVersions(string a, string b)
        {
            string[] left = (a ?? "").Split('.');
            string[] right = (b ?? "").Split('.');
            int count = Math.Max(left.Length, right.Length);
            for (int i = 0; i < count; i++)
            {
                int x, y;
                int.TryParse(i < left.Length ? left[i] : "0", out x);
                int.TryParse(i < right.Length ? right[i] : "0", out y);
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        private void Append(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Append), text); return; }
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        }

        private void SetBusy(bool busy, string status)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), busy, status); return; }
            _busy = busy;
            _install.Enabled = !busy;
            _rollback.Enabled = !busy;
            _browse.Enabled = !busy;
            _path.Enabled = !busy;
            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            _status.Text = status;
        }

        private void Run(bool install)
        {
            if (_busy) return;
            string dir = _path.Text.Trim();
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this, "Папка не найдена: " + dir, "Qoder RU", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetBusy(true, install ? "Установка…" : "Откат…");
            new Thread(() =>
            {
                try
                {
                    Action<string> log = Append;
                    if (install) Patcher.Install(dir, log);
                    else Patcher.Rollback(dir, log);
                    SetBusy(false, install ? "Установка завершена." : "Откат завершён.");
                    BeginInvoke(new Action(() => MessageBox.Show(this,
                        install ? "Русский язык установлен. Запустите Qoder." : "Изменения отменены.",
                        "Qoder RU", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                }
                catch (Exception ex)
                {
                    Append("ОШИБКА: " + ex.Message);
                    SetBusy(false, "Ошибка.");
                    BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Qoder RU", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            }) { IsBackground = true }.Start();
        }
    }

    internal static class Patcher
    {
        private const string AsarBackupSuffix = ".qoder-ru-backup";
        private const string StateFile = "qoder-ru-state.json";

        internal static string AutoDetect()
        {
            var candidates = new List<string>();
            foreach (var process in Process.GetProcessesByName("Qoder"))
            {
                using (process)
                {
                    try
                    {
                        string file = process.MainModule.FileName;
                        if (!string.IsNullOrEmpty(file)) candidates.Add(Path.GetDirectoryName(file));
                    }
                    catch { }
                }
            }
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                        using (var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (uninstall != null)
                            {
                                foreach (var name in uninstall.GetSubKeyNames())
                                {
                                    using (var key = uninstall.OpenSubKey(name))
                                    {
                                        if (key == null) continue;
                                        string display = key.GetValue("DisplayName") as string;
                                        if (display == null || display.IndexOf("Qoder", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                        string loc = key.GetValue("InstallLocation") as string;
                                        if (!string.IsNullOrEmpty(loc)) candidates.Add(loc.Trim('"'));
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Qoder"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Qoder"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Qoder"));

            foreach (var dir in candidates)
            {
                try
                {
                    if (IsQoderDir(dir)) return Path.GetFullPath(dir);
                }
                catch { }
            }
            return null;
        }

        private static bool IsQoderDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) &&
                   File.Exists(Path.Combine(dir, "Qoder.exe")) &&
                   File.Exists(Path.Combine(dir, "resources", "app.asar"));
        }

        internal static void Install(string dir, Action<string> log)
        {
            if (!IsQoderDir(dir)) throw new InvalidOperationException("В выбранной папке нет Qoder.exe и resources\\app.asar.");
            string asarPath = Path.Combine(dir, "resources", "app.asar");
            string exePath = Path.Combine(dir, "Qoder.exe");
            string backupPath = asarPath + AsarBackupSuffix;
            string statePath = Path.Combine(dir, StateFile);

            EnsureNotRunning(dir, log);

            string targetVersion = TargetVersion();
            if (string.IsNullOrEmpty(targetVersion)) throw new InvalidOperationException("В установщике не указана целевая версия Qoder.");
            string manifestPath = Path.Combine(dir, "resources", "build-manifest.json");
            if (!File.Exists(manifestPath)) throw new InvalidOperationException("Не найден resources\\build-manifest.json — версия Qoder не определена.");
            var manifest = NewSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath, Encoding.UTF8));
            string actualVersion = manifest.ContainsKey("productVersion") ? manifest["productVersion"] as string : null;
            if (actualVersion != targetVersion)
                throw new InvalidOperationException("Этот установщик предназначен для Qoder " + targetVersion + ", а найдена версия " + (actualVersion ?? "неизвестна") + ". Скачайте установщик для вашей версии Qoder.");

            log("Qoder: " + Path.GetFullPath(dir));
            log("Версия Qoder: " + actualVersion);
            if (!File.Exists(backupPath))
            {
                log("Создаю резервную копию app.asar…");
                File.Copy(asarPath, backupPath, false);
            }
            else
            {
                log("Использую существующую резервную копию app.asar.");
            }

            string source = backupPath;
            var asar = AsarFile.Open(source);
            log("ASAR: " + asar.Size.ToString("N0") + " байт");

            string dictJson = ReadResource("qoder-ru-dict.json");
            dictJson = dictJson.Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
            var dict = ParseFlatDictionary(dictJson);
            log("Словарь: " + dict.Count.ToString("N0") + " строк");

            string i18nPath = null, settingsPath = null;
            string i18n = null, settings = null;
            foreach (var entry in asar.ListFiles())
            {
                if (!entry.StartsWith("/out/", StringComparison.Ordinal) || !entry.EndsWith(".js", StringComparison.Ordinal)) continue;
                string text = Encoding.UTF8.GetString(asar.ReadEntry(entry));
                bool isI18n = (text.Contains("St={zh:{translation:") &&
                               (text.Contains("he=\"loop.language\";function Tt(") || text.Contains("const he=\"loop.language\";function Tt(")))
                              || (text.Contains(",Wt={\"zh-HK\":{translation:Object.assign") && text.Contains("Be=\"loop.language\""));
                bool isSettings = (text.Contains("简体中文") && text.Contains("Uk1(") && text.Contains("settings.languageDescription")) ||
                                  (text.Contains("settings.languageDescription") && text.Contains("\"data-settings-search-id\":\"settings.language\""));
                if (isI18n) { i18nPath = entry; i18n = text; }
                if (isSettings) { settingsPath = entry; settings = text; }
            }
            asar.Close();
            if (i18nPath == null) throw new InvalidOperationException("Не найден i18n-чанк. Версия Qoder не поддерживается.");
            bool legacyLayout = i18n.Contains("St={zh:{translation:");
            if (legacyLayout && settingsPath == null) throw new InvalidOperationException("Не найден чанк настроек. Версия Qoder не поддерживается.");
            log("i18n: " + i18nPath);
            if (settingsPath != null) log("settings: " + settingsPath);
            log("Схема каталога: " + (legacyLayout ? "legacy" : "split"));

            if (legacyLayout)
            {
                i18n = ReplaceOnce(i18n,
                    ",he=\"loop.language\";function Tt(o){return o===\"zh\"||o===\"en\"?o:null}",
                    ";St.ru={translation:" + dictJson + "};const he=\"loop.language\";function Tt(o){return o===\"zh\"||o===\"en\"||o===\"ru\"?o:null}",
                    "i18n resources");
                i18n = ReplaceOnce(i18n,
                    "function Ni(o){return o[0]?.toLowerCase().startsWith(\"zh\")?\"zh\":\"en\"}",
                    "function Ni(o){return o[0]?.toLowerCase().startsWith(\"zh\")?\"zh\":o[0]?.toLowerCase().startsWith(\"ru\")?\"ru\":\"en\"}",
                    "Ni");
                i18n = ReplaceOnce(i18n,
                    "function Fi(o,e){return o===\"zh\"||o===\"en\"?o:Ni(e)}",
                    "function Fi(o,e){return o===\"zh\"||o===\"en\"||o===\"ru\"?o:Ni(e)}",
                    "Fi");
                i18n = ReplaceOnce(i18n,
                    "function cn(o){return o.toLowerCase().startsWith(\"zh\")?\"zh\":\"en\"}",
                    "function cn(o){return o.toLowerCase().startsWith(\"zh\")?\"zh\":o.toLowerCase().startsWith(\"ru\")?\"ru\":\"en\"}",
                    "cn");
                i18n = ReplaceOnce(i18n, "Ui=Fi(At(),Li)", "Ui=\"ru\"", "default language");

                settings = ReplaceOnce(settings,
                    "(C1===\"zh\"||C1===\"en\")&&Uk1(C1)",
                    "(C1===\"zh\"||C1===\"en\"||C1===\"ru\")&&Uk1(C1)",
                    "language guard (sidebar)");
                settings = ReplaceOnce(settings,
                    "(J===\"zh\"||J===\"en\")&&Uk1(J)",
                    "(J===\"zh\"||J===\"en\"||J===\"ru\")&&Uk1(J)",
                    "language guard (settings)");
                settings = ReplaceOnce(settings,
                    "s.jsx(A2,{className:$i,onSelect:C(\"gui_e9307d6c06d73212\",s1,\"en\",k0[2],\"activation\"),value:\"en\",children:\"English\"})",
                    "s.jsx(A2,{className:$i,onSelect:C(\"gui_e9307d6c06d73212\",s1,\"en\",k0[2],\"activation\"),value:\"en\",children:\"English\"}),s.jsx(A2,{className:$i,onSelect:C(\"gui_5f3a1c9e7b2d4a60\",s1,\"ru\",k0[2],\"activation\"),value:\"ru\",children:\"Русский\"})",
                    "language option (sidebar)");
                settings = ReplaceOnce(settings,
                    "s.jsx(oe,{value:\"en\",\"data-qoder-gui-id\":\"gui_fd4e04c9f35bbeaf\",\"data-qoder-gui-activation\":\"pointerup\",\"data-qoder-gui-target\":h0(\"en\",Mn[7]),children:\"English\"})",
                    "s.jsx(oe,{value:\"en\",\"data-qoder-gui-id\":\"gui_fd4e04c9f35bbeaf\",\"data-qoder-gui-activation\":\"pointerup\",\"data-qoder-gui-target\":h0(\"en\",Mn[7]),children:\"English\"}),s.jsx(oe,{value:\"ru\",\"data-qoder-gui-id\":\"gui_6a4b2d8f1c3e5a70\",\"data-qoder-gui-activation\":\"pointerup\",\"data-qoder-gui-target\":h0(\"ru\",Mn[7]),children:\"Русский\"})",
                    "language option (settings)");
            }
            else
            {
                i18n = ReplaceOnce(i18n,
                    "function _e(i){return Ot(i).startsWith(\"zh\")?\"zh\":\"en\"}",
                    "function _e(i){return Ot(i).startsWith(\"zh\")?\"zh\":Ot(i).startsWith(\"ru\")?\"ru\":\"en\"}",
                    "_e");
                i18n = ReplaceOnce(i18n,
                    "function Ct(i){return _e(i)===\"zh\"?[\"zh\",\"en\"]:[\"en\"]}",
                    "function Ct(i){return _e(i)===\"zh\"?[\"zh\",\"en\"]:_e(i)===\"ru\"?[\"ru\",\"en\"]:[\"en\"]}",
                    "Ct");
                i18n = ReplaceOnce(i18n,
                    ",xt=[{value:\"zh\",label:\"简体中文\"},{value:\"en\",label:\"English\"},{value:\"zh-HK\",label:\"繁體中文（香港）\"},{value:\"ko-KR\",label:\"한국어\"}],Yi=xt.filter(({value:i})=>i===\"zh\"||i===\"en\")",
                    ",xt=[{value:\"zh\",label:\"简体中文\"},{value:\"en\",label:\"English\"},{value:\"zh-HK\",label:\"繁體中文（香港）\"},{value:\"ko-KR\",label:\"한국어\"},{value:\"ru\",label:\"Русский\"}],Yi=xt.filter(({value:i})=>i===\"zh\"||i===\"en\"||i===\"ru\")",
                    "language lists");
                i18n = ReplaceOnce(i18n,
                    ",...qe.en}}},Be=\"loop.language\";",
                    ",...qe.en}},ru:{translation:" + dictJson + "}},Be=\"loop.language\";",
                    "i18n resources");
                i18n = ReplaceOnce(i18n, ",en=Zi(Ht(),Xi)", ",en=Ht()??\"ru\"", "default language");
                i18n = ReplaceOnce(i18n,
                    "qt=i=>{typeof document>\"u\"||(document.documentElement.lang=i===\"zh\"?\"zh-CN\":i===\"en\"?\"en-US\":i)}",
                    "qt=i=>{typeof document>\"u\"||(document.documentElement.lang=i===\"zh\"?\"zh-CN\":i===\"en\"?\"en-US\":i===\"ru\"?\"ru-RU\":i)}",
                    "document lang");
            }

            string dynamicPath = Path.Combine(dir, "resources", "dynamic-text", "qoder.json");
            string dynamicBackup = dynamicPath + AsarBackupSuffix;
            Dictionary<string, object> dynamicEn = null;
            if (File.Exists(dynamicPath) || File.Exists(dynamicBackup))
            {
                if (!File.Exists(dynamicBackup) && File.Exists(dynamicPath)) File.Copy(dynamicPath, dynamicBackup, false);
                string src = File.Exists(dynamicBackup) ? dynamicBackup : dynamicPath;
                var serializer = NewSerializer();
                var dyn = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(src, Encoding.UTF8));
                if (dyn.ContainsKey("en")) dynamicEn = dyn["en"] as Dictionary<string, object>;
            }

            var dynMap = new Dictionary<string, string>();
            if (dynamicEn != null)
            {
                foreach (var kv in dynamicEn)
                {
                    string en = kv.Value as string;
                    if (string.IsNullOrEmpty(en)) continue;
                    string ru;
                    if (dict.TryGetValue(kv.Key, out ru) && ru != en) dynMap[en] = ru;
                }
            }
            foreach (var kv in ParseFlatDictionary(ReadResource("qoder-ru-extra.json")))
                dynMap[kv.Key] = kv.Value;

            string observer = BuildObserver(NewSerializer().Serialize(dynMap));
            i18n = i18n + "\n" + observer;
            log("Динамических строк для замены: " + dynMap.Count);

            log("Пересобираю app.asar…");
            string tmp = asarPath + ".qoder-ru-new";
            var replacements = new Dictionary<string, byte[]>
            {
                { i18nPath, Encoding.UTF8.GetBytes(i18n) },
            };
            if (legacyLayout && settingsPath != null && settings != null)
                replacements[settingsPath] = Encoding.UTF8.GetBytes(settings);
            string newHash = AsarFile.Rebuild(source, tmp, replacements, log);
            File.Copy(tmp, asarPath, true);
            File.Delete(tmp);
            log("Новый хэш ASAR: " + newHash);

            log("Обновляю контрольную сумму в Qoder.exe…");
            string oldIntegrity;
            PatchExeIntegrity(exePath, newHash, out oldIntegrity);
            log("Исходный хэш exe: " + oldIntegrity);

            if (dynamicEn != null)
            {
                var serializer = NewSerializer();
                var dyn = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(File.Exists(dynamicBackup) ? dynamicBackup : dynamicPath, Encoding.UTF8));
                var ru = new Dictionary<string, object>();
                foreach (var kv in dynamicEn)
                {
                    string en = kv.Value as string;
                    string tr;
                    ru[kv.Key] = dynMap.TryGetValue(en ?? "", out tr) ? tr : en;
                }
                dyn["ru"] = ru;
                File.WriteAllText(dynamicPath, serializer.Serialize(dyn), new UTF8Encoding(false));
                log("dynamic-text: добавлена секция ru");
            }

            string originalIntegrity = oldIntegrity;
            if (File.Exists(statePath))
            {
                try
                {
                    var prev = NewSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(statePath, Encoding.UTF8));
                    string prevHash = prev.ContainsKey("originalExeIntegrity") ? prev["originalExeIntegrity"] as string : null;
                    if (IsHex64(prevHash)) originalIntegrity = prevHash;
                }
                catch { }
            }

            var patchedFiles = new List<string> { i18nPath };
            if (legacyLayout && settingsPath != null) patchedFiles.Add(settingsPath);

            var state = new Dictionary<string, object>
            {
                { "schema", 2 },
                { "patchedAt", DateTime.UtcNow.ToString("o") },
                { "installDir", Path.GetFullPath(dir) },
                { "qoderVersion", actualVersion },
                { "installedLocalizationVersion", targetVersion },
                { "layout", legacyLayout ? "legacy" : "split" },
                { "dictionaryEntries", dict.Count },
                { "defaultLanguage", "ru" },
                { "patchedFiles", patchedFiles.ToArray() },
                { "originalExeIntegrity", originalIntegrity },
                { "patchedAsarIntegrity", newHash },
                { "backupAsar", "resources\\app.asar" + AsarBackupSuffix },
                { "dynamicText", dynamicEn != null },
            };
            File.WriteAllText(statePath, NewSerializer().Serialize(state), new UTF8Encoding(false));
            log("Готово. Запустите Qoder.");
        }

        internal static void Rollback(string dir, Action<string> log)
        {
            if (!IsQoderDir(dir)) throw new InvalidOperationException("В выбранной папке нет Qoder.exe и resources\\app.asar.");
            string asarPath = Path.Combine(dir, "resources", "app.asar");
            string backupPath = asarPath + AsarBackupSuffix;
            string exePath = Path.Combine(dir, "Qoder.exe");
            string statePath = Path.Combine(dir, StateFile);
            EnsureNotRunning(dir, log);

            if (!File.Exists(backupPath)) throw new InvalidOperationException("Резервная копия app.asar не найдена — откат невозможен.");
            if (!File.Exists(statePath)) throw new InvalidOperationException("Файл состояния qoder-ru-state.json не найден.");

            var state = NewSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(statePath, Encoding.UTF8));
            string original = state.ContainsKey("originalExeIntegrity") ? state["originalExeIntegrity"] as string : null;
            if (!IsHex64(original)) throw new InvalidOperationException("В файле состояния нет исходного хэша Qoder.exe.");

            log("Восстанавливаю app.asar из резервной копии…");
            File.Copy(backupPath, asarPath, true);
            log("Восстанавливаю хэш Qoder.exe…");
            string ignored;
            PatchExeIntegrity(exePath, original, out ignored);

            string dynamicPath = Path.Combine(dir, "resources", "dynamic-text", "qoder.json");
            string dynamicBackup = dynamicPath + AsarBackupSuffix;
            if (File.Exists(dynamicBackup))
            {
                File.Copy(dynamicBackup, dynamicPath, true);
                log("dynamic-text восстановлен.");
            }

            state["rolledBackAt"] = DateTime.UtcNow.ToString("o");
            File.WriteAllText(statePath, NewSerializer().Serialize(state), new UTF8Encoding(false));
            log("Откат завершён.");
        }

        private static void EnsureNotRunning(string dir, Action<string> log)
        {
            string full = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
            foreach (var p in Process.GetProcessesByName("Qoder"))
            {
                try
                {
                    string path = p.MainModule.FileName;
                    if (path.StartsWith(full, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Qoder запущен из этой папки. Закройте его и повторите.");
                }
                catch (InvalidOperationException) { throw; }
                catch { }
            }
            log("Проверка процессов Qoder пройдена.");
        }

        private static string ReadResource(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            string full = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            if (full == null) throw new InvalidOperationException("Встроенный ресурс не найден: " + name);
            using (var s = asm.GetManifestResourceStream(full))
            using (var r = new StreamReader(s, Encoding.UTF8))
                return r.ReadToEnd();
        }

        internal static string TargetVersion()
        {
            var info = NewSerializer().Deserialize<Dictionary<string, object>>(ReadResource("qoder-ru-version.json"));
            return info.ContainsKey("qoderVersion") ? info["qoderVersion"] as string : null;
        }

        private static JavaScriptSerializer NewSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
        }

        private static Dictionary<string, string> ParseFlatDictionary(string json)
        {
            var raw = NewSerializer().Deserialize<Dictionary<string, object>>(json);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in raw)
            {
                string v = kv.Value as string;
                if (v != null) result[kv.Key] = v;
            }
            return result;
        }

        private static string BuildObserver(string mapJson)
        {
            return ";(()=>{const M=" + mapJson +
                ";const T=n=>{if(n.nodeType===3){const s=n.nodeValue,t=s.trim(),r=M[t];if(r&&r!==t)n.nodeValue=s.replace(t,r)}else if(n.nodeType===1){for(const a of[\"title\",\"aria-label\",\"placeholder\",\"alt\"]){const v=n.getAttribute&&n.getAttribute(a);if(v&&M[v])n.setAttribute(a,M[v])}for(const c of n.childNodes)T(c)}};" +
                "const S=()=>{const b=document.body;if(!b)return;T(b);const o=new MutationObserver(ms=>{for(const m of ms){if(m.type===\"characterData\")T(m.target);else for(const n of m.addedNodes)T(n)}});o.observe(b,{subtree:true,childList:true,characterData:true})};" +
                "if(document.body)S();else document.addEventListener(\"DOMContentLoaded\",S,{once:true})})();";
        }

        private static string ReplaceOnce(string source, string from, string to, string label)
        {
            int idx = source.IndexOf(from, StringComparison.Ordinal);
            if (idx < 0) throw new InvalidOperationException(label + ": шаблон не найден (версия Qoder не поддерживается).");
            if (source.IndexOf(from, idx + 1, StringComparison.Ordinal) >= 0) throw new InvalidOperationException(label + ": шаблон встречается более одного раза.");
            return source.Substring(0, idx) + to + source.Substring(idx + from.Length);
        }

        private static bool IsHex64(string s)
        {
            if (s == null || s.Length != 64) return false;
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private static void PatchExeIntegrity(string exePath, string newHash, out string oldHash)
        {
            byte[] exe = File.ReadAllBytes(exePath);
            byte[] marker = Encoding.ASCII.GetBytes("\"alg\":\"SHA256\",\"value\":\"");
            var offsets = new List<int>();
            int from = 0;
            while (true)
            {
                int i = IndexOf(exe, marker, from);
                if (i < 0) break;
                offsets.Add(i + marker.Length);
                from = i + 1;
            }
            if (offsets.Count != 1) throw new InvalidOperationException("Не найден уникальный блок целостности ELECTRONASAR в Qoder.exe.");
            int at = offsets[0];
            oldHash = Encoding.ASCII.GetString(exe, at, 64);
            if (!IsHex64(oldHash)) throw new InvalidOperationException("Неожиданное значение целостности в Qoder.exe.");
            byte[] hashBytes = Encoding.ASCII.GetBytes(newHash);
            Buffer.BlockCopy(hashBytes, 0, exe, at, 64);
            File.WriteAllBytes(exePath, exe);
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            int limit = haystack.Length - needle.Length;
            for (int i = start; i <= limit; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }
    }

    internal sealed class AsarFile
    {
        private const int BlockSize = 4 * 1024 * 1024;
        private FileStream _fs;
        private Dictionary<string, object> _header;
        private long _dataStart;
        private long _size;

        public long Size { get { return _size; } }

        public static AsarFile Open(string path)
        {
            var a = new AsarFile();
            a._fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] head = new byte[16];
            a._fs.Read(head, 0, 16);
            uint jsonLen = BitConverter.ToUInt32(head, 12);
            byte[] json = new byte[jsonLen];
            a._fs.Read(json, 0, (int)jsonLen);
            a._header = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 512 }
                .Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(json));
            a._dataStart = 8 + BitConverter.ToUInt32(head, 4);
            a._size = a._fs.Length;
            return a;
        }

        public void Close()
        {
            if (_fs != null) { _fs.Dispose(); _fs = null; }
        }

        public IEnumerable<string> ListFiles()
        {
            var list = new List<string>();
            Walk(_header, "", list);
            return list;
        }

        private static void Walk(Dictionary<string, object> node, string prefix, List<string> list)
        {
            if (!node.ContainsKey("files")) return;
            var files = node["files"] as Dictionary<string, object>;
            foreach (var kv in files)
            {
                string path = prefix + "/" + kv.Key;
                var child = kv.Value as Dictionary<string, object>;
                if (child == null) continue;
                if (child.ContainsKey("files")) Walk(child, path, list);
                else list.Add(path);
            }
        }

        public Dictionary<string, object> FindNode(string path)
        {
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, object> node = _header;
            foreach (var part in parts)
            {
                if (!node.ContainsKey("files")) return null;
                var files = node["files"] as Dictionary<string, object>;
                if (files == null || !files.ContainsKey(part)) return null;
                node = files[part] as Dictionary<string, object>;
            }
            return node;
        }

        public byte[] ReadEntry(string path)
        {
            var node = FindNode(path);
            if (node == null) throw new InvalidOperationException("Нет записи в ASAR: " + path);
            int size = Convert.ToInt32(node["size"]);
            byte[] buf = new byte[size];
            long offset = Convert.ToInt64(node["offset"]);
            _fs.Position = _dataStart + offset;
            int read = 0;
            while (read < size)
            {
                int n = _fs.Read(buf, read, size - read);
                if (n <= 0) break;
                read += n;
            }
            return buf;
        }

        public static string Rebuild(string src, string dest, Dictionary<string, byte[]> replacements, Action<string> log)
        {
            var a = Open(src);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 512 };
            byte[] headerBytes;
            {
                byte[] head = new byte[16];
                a._fs.Position = 0;
                a._fs.Read(head, 0, 16);
                uint jsonLen = BitConverter.ToUInt32(head, 12);
                headerBytes = new byte[jsonLen];
                a._fs.Read(headerBytes, 0, (int)jsonLen);
            }
            var header = serializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(headerBytes));
            var asar = new AsarFile { _header = header, _dataStart = a._dataStart, _size = a._size, _fs = a._fs };
            long originalDataSize = a._size - a._dataStart;

            long appended = 0;
            var chunks = new List<byte[]>();
            foreach (var kv in replacements)
            {
                var node = asar.FindNode(kv.Key);
                if (node == null) throw new InvalidOperationException("Запись ASAR не найдена: " + kv.Key);
                node["size"] = kv.Value.Length;
                node["offset"] = (originalDataSize + appended).ToString();
                var integrity = new Dictionary<string, object>
                {
                    { "algorithm", "SHA256" },
                    { "hash", Sha256Hex(kv.Value) },
                    { "blockSize", BlockSize },
                    { "blocks", Blocks(kv.Value) },
                };
                node["integrity"] = integrity;
                appended += kv.Value.Length;
                chunks.Add(kv.Value);
            }

            string json = serializer.Serialize(header);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            int pad = (4 - (jsonBytes.Length % 4)) % 4;
            uint headerSize = (uint)(8 + jsonBytes.Length + pad);
            byte[] prefix = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes((uint)4), 0, prefix, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(headerSize), 0, prefix, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(headerSize - 4), 0, prefix, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)jsonBytes.Length), 0, prefix, 12, 4);

            using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                output.Write(prefix, 0, 16);
                output.Write(jsonBytes, 0, jsonBytes.Length);
                for (int i = 0; i < pad; i++) output.WriteByte(0);
                a._fs.Position = a._dataStart;
                CopyRange(a._fs, output, originalDataSize);
                foreach (var chunk in chunks) output.Write(chunk, 0, chunk.Length);
                output.Flush(true);
            }
            a.Close();
            log("ASAR пересобран: " + (originalDataSize + appended).ToString("N0") + " байт данных");
            return Sha256Hex(jsonBytes);
        }

        private static void CopyRange(Stream src, Stream dst, long length)
        {
            byte[] buf = new byte[8 * 1024 * 1024];
            long left = length;
            while (left > 0)
            {
                int n = src.Read(buf, 0, (int)Math.Min(buf.Length, left));
                if (n <= 0) break;
                dst.Write(buf, 0, n);
                left -= n;
            }
        }

        private static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(data));
        }

        private static List<string> Blocks(byte[] data)
        {
            var list = new List<string>();
            using (var sha = SHA256.Create())
            {
                for (int off = 0; off < data.Length; off += BlockSize)
                {
                    int len = Math.Min(BlockSize, data.Length - off);
                    byte[] block = new byte[len];
                    Buffer.BlockCopy(data, off, block, 0, len);
                    list.Add(ToHex(sha.ComputeHash(block)));
                }
            }
            return list;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
