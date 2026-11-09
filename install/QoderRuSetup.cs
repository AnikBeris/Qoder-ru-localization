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
            if (args.Length > 0 && (args[0] == "--install" || args[0] == "--rollback" || args[0] == "--detect"))
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
                    else if (args[0] == "--rollback") Patcher.Rollback(dir, log);
                    else Patcher.Detect(dir, log);
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
                Text = "Папка установки Qoder (обновления .qoder-versions учитываются автоматически):",
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
            else if (Patcher.CompareVersions(latest, current) > 0)
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
            try
            {
                string launcherState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qoder", "Qoder Launcher", "state.ini");
                if (File.Exists(launcherState))
                {
                    foreach (var line in File.ReadAllLines(launcherState))
                    {
                        if (line.StartsWith("installDir=", StringComparison.OrdinalIgnoreCase))
                            candidates.Insert(0, line.Substring("installDir=".Length).Trim());
                    }
                }
            }
            catch { }

            foreach (var dir in candidates)
            {
                try
                {
                    string resolved = ResolveActiveInstall(dir, null);
                    if (IsQoderDir(resolved)) return Path.GetFullPath(resolved);
                }
                catch { }
            }
            return null;
        }

        // Внутренний обновитель Qoder кладёт новые версии в <папка>\.qoder-versions\<версия>,
        // а базовая папка остаётся со старым build-manifest.json. Активную копию указывает
        // state.ini лаунчера; без него берём старшую версию из .qoder-versions.
        internal static string ResolveActiveInstall(string dir, Action<string> log)
        {
            if (string.IsNullOrEmpty(dir)) return dir;
            string full;
            try { full = Path.GetFullPath(dir); } catch { return dir; }
            string versionsRoot = Path.Combine(full, ".qoder-versions");
            if (!Directory.Exists(versionsRoot)) return full;

            string active = null;
            try
            {
                string launcherState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Qoder", "Qoder Launcher", "state.ini");
                if (File.Exists(launcherState))
                {
                    string installDir = null, appExe = null;
                    foreach (var line in File.ReadAllLines(launcherState))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                        if (k == "installDir") installDir = v;
                        else if (k == "appExecutable") appExe = v;
                    }
                    if (!string.IsNullOrEmpty(installDir) && !string.IsNullOrEmpty(appExe))
                    {
                        string normInstall = Path.GetFullPath(installDir).TrimEnd('\\');
                        if (string.Equals(normInstall, full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        {
                            string exePath = Path.IsPathRooted(appExe) ? appExe : Path.Combine(full, appExe);
                            string candidate = Path.GetDirectoryName(exePath);
                            if (IsQoderDir(candidate)) active = candidate;
                        }
                    }
                }
            }
            catch { }
            if (active == null)
            {
                string best = null;
                foreach (var sub in Directory.GetDirectories(versionsRoot))
                {
                    if (!IsQoderDir(sub)) continue;
                    string name = Path.GetFileName(sub);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d+(\.\d+)*$")) continue;
                    if (best == null || CompareVersions(name, Path.GetFileName(best)) > 0) best = sub;
                }
                active = best;
            }
            if (active != null && !string.Equals(active.TrimEnd('\\'), full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                if (log != null) log("Внутреннее обновление Qoder: активная копия — " + active);
                return active;
            }
            return full;
        }

        internal static void Detect(string dir, Action<string> log)
        {
            if (string.IsNullOrEmpty(dir)) dir = AutoDetect();
            if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("Qoder не найден ни автоматически, ни в указанной папке.");
            dir = ResolveActiveInstall(dir, log);
            log("Папка активной копии: " + Path.GetFullPath(dir));
            string manifestPath = Path.Combine(dir, "resources", "build-manifest.json");
            string actual = null;
            if (File.Exists(manifestPath))
            {
                var manifest = NewSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath, Encoding.UTF8));
                if (manifest.ContainsKey("productVersion")) actual = manifest["productVersion"] as string;
            }
            log("Версия активной копии: " + (actual ?? "неизвестна"));
            log("Целевая версия установщика: " + TargetVersion());
        }

        private static bool IsQoderDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) &&
                   File.Exists(Path.Combine(dir, "Qoder.exe")) &&
                   File.Exists(Path.Combine(dir, "resources", "app.asar"));
        }

        internal static void Install(string dir, Action<string> log)
        {
            dir = ResolveActiveInstall(dir, log);
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
                throw new InvalidOperationException("Этот установщик предназначен для Qoder " + targetVersion + ", а активная копия — версии " + (actualVersion ?? "неизвестна") + " (" + dir + "). Скачайте установщик для вашей версии Qoder.");

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
            var overlay = ParseFlatDictionary(ReadResource("qoder-ru-overlay.json"));
            var catalog = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in dict) catalog[kv.Key] = kv.Value;
            foreach (var kv in overlay) catalog[kv.Key] = kv.Value;

            var presetKeys = new[] { "leader", "researcher", "developer", "tester", "reviewer" };
            var presets = new List<Dictionary<string, object>>();
            foreach (string key in presetKeys)
            {
                string prefix = "settings.squadTemplateAgentPresets." + key + ".";
                string name, description, instructions;
                if (overlay.TryGetValue(prefix + "name", out name) &&
                    overlay.TryGetValue(prefix + "description", out description) &&
                    overlay.TryGetValue(prefix + "instructions", out instructions))
                {
                    presets.Add(new Dictionary<string, object>
                    {
                        { "key", key },
                        { "name", name },
                        { "description", description },
                        { "instructions", instructions },
                    });
                }
            }
            if (presets.Count == presetKeys.Length)
                catalog["settings.squadTemplateAgentPresets"] = presets.ToArray();

            string catalogJson = NewSerializer().Serialize(catalog);
            log("Словарь: " + dict.Count.ToString("N0") + " строк, переопределений: " + overlay.Count.ToString("N0"));

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
                    ";St.ru={translation:" + catalogJson + "};const he=\"loop.language\";function Tt(o){return o===\"zh\"||o===\"en\"||o===\"ru\"?o:null}",
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
                    ",...qe.en}},ru:{translation:" + catalogJson + "}},Be=\"loop.language\";",
                    "i18n resources");
                i18n = ReplaceOnce(i18n, ",en=Zi(Ht(),Xi)", ",en=Ht()??\"ru\"", "default language");
                i18n = ReplaceOnce(i18n,
                    "qt=i=>{typeof document>\"u\"||(document.documentElement.lang=i===\"zh\"?\"zh-CN\":i===\"en\"?\"en-US\":i)}",
                    "qt=i=>{typeof document>\"u\"||(document.documentElement.lang=i===\"zh\"?\"zh-CN\":i===\"en\"?\"en-US\":i===\"ru\"?\"ru-RU\":i)}",
                    "document lang");
                i18n = ReplaceOnce(i18n,
                    "qt(v.language);v.on(\"languageChanged\",qt);",
                    "qt(v.language);v.on(\"languageChanged\",qt);globalThis.__qoderRuLang=v.language;v.on(\"languageChanged\",i=>{globalThis.__qoderRuLang=i});",
                    "language global");
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
            var extraMap = ParseFlatDictionary(ReadResource("qoder-ru-extra.json"));
            foreach (var kv in extraMap)
                dynMap[kv.Key] = kv.Value;

            string observer = BuildObserver(NewSerializer().Serialize(dynMap), "{}");
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

            var patchedPlugins = new List<string>();
            PatchPlugins(dir, log, patchedPlugins);

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
                { "patchedPlugins", patchedPlugins.ToArray() },
            };
            File.WriteAllText(statePath, NewSerializer().Serialize(state), new UTF8Encoding(false));
            log("Готово. Запустите Qoder.");
        }

        internal static void Rollback(string dir, Action<string> log)
        {
            dir = ResolveActiveInstall(dir, log);
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

            var restoredPlugins = state.ContainsKey("patchedPlugins") ? state["patchedPlugins"] as object[] : null;
            if (restoredPlugins != null)
            {
                foreach (object item in restoredPlugins)
                {
                    string relative = item as string;
                    if (string.IsNullOrEmpty(relative)) continue;
                    string pluginPath = Path.Combine(dir, relative);
                    string pluginBackup = pluginPath + AsarBackupSuffix;
                    if (File.Exists(pluginBackup))
                    {
                        File.Copy(pluginBackup, pluginPath, true);
                        log("Восстановлен плагин: " + relative);
                    }
                }
            }

            state["rolledBackAt"] = DateTime.UtcNow.ToString("o");
            File.WriteAllText(statePath, NewSerializer().Serialize(state), new UTF8Encoding(false));
            log("Откат завершён.");
        }

        private static readonly string[] PluginFiles =
        {
            @"resources\extensions\qoder.index-settings\dist\browser\view.cjs",
            @"resources\extensions\qoder.security\dist\browser\view.cjs",
            @"resources\extensions\qoder.knowledge.center\dist\browser\view.cjs",
        };

        private const string PluginCatalogFile = @"resources\extensions\catalog.v1.json";

        private const string IndexSettingsRu = @"var ru = {
  title: ""Индекс рабочей области"",
  description: ""Управление индексами кода для локальных рабочих областей, открытых в Qoder."",
  repositories: ""Репозитории"",
  workspaceIndexTitle: ""Индекс рабочей области"",
  workspaceIndexDescription: ""Индекс рабочей области даёт агенту больше контекста и помогает точнее отвечать. {learnMore} об обработке данных индекса рабочей области."",
  workspaceIndexLearnMore: ""Подробнее"",
  workspaceIndexDocsUrl: ""https://docs.qoder.com/qoder/indexing"",
  sharedWorkspaceBlockedPrefix: ""Этот репозиторий также принадлежит "",
  sharedWorkspaceBlockedTarget: ""другой рабочей области"",
  sharedWorkspaceBlockedTargetPlural: ""другим рабочим областям"",
  sharedWorkspaceBlockedSuffix: "", где индексация рабочей области отключена. Автоматическая индексация приостановлена."",
  workspaceIndexPaused: ""Индексация рабочей области здесь отключена. Автоматическая индексация приостановлена."",
  expandWorkspace: ""Развернуть рабочую область"",
  collapseWorkspace: ""Свернуть рабочую область"",
  saving: ""Сохранение…"",
  opening: ""Открытие…"",
  starting: ""Запуск индексации…"",
  autoUpdateBusy: ""Индекс проверяется или обновляется. Автоматическую индексацию можно изменить после завершения."",
  loading: ""Загрузка настроек индексации"",
  retry: ""Повторить"",
  signedOutTitle: ""Войдите в Qoder"",
  signedOutDescription: ""Войдите, чтобы плагин мог найти службу Qoder Context для вашей учётной записи."",
  daemonUnavailableTitle: ""Служба индексации не запущена"",
  daemonUnavailableDescription: ""Попробуйте перезапустить Qoder, чтобы восстановить службу индексации."",
  daemonNotReadyDescription: ""Рабочие области загружаются из Qoder. Состояние индекса и действия станут доступны после подключения Qoder Context."",
  emptyTitle: ""Нет репозиториев для индексации"",
  emptyDescription: ""Добавьте репозиторий в локальную рабочую область Qoder, чтобы управлять его индексом здесь."",
  loadFailedTitle: ""Не удалось загрузить настройки индексации"",
  policyDisabled: ""Индексация кодовой базы отключена серверной политикой. Ручные и автоматические действия недоступны."",
  indexTitle: ""Состояние индекса"",
  startIndex: ""Начать индексацию"",
  manualUpdate: ""Обновить сейчас"",
  cancel: ""Отмена"",
  cancelling: ""Отмена…"",
  autoUpdate: ""Автоматическая индексация"",
  autoUpdateDescription: ""Автоматически создавать и обновлять индексы для кодовых баз с числом файлов менее 10 000"",
  autoUpdateUnavailable: ""Достигнут лимит автоматической индексации. Ручное обновление остаётся доступным."",
  ignoreFiles: ""Игнорируемые файлы"",
  ignoreFilesDescription: ""Укажите файлы и папки, исключаемые из индексации. Это дополнение к существующим правилам .gitignore"",
  manage: ""Управление игнорируемыми файлами"",
  ignoreFileOpenFailed: ""Не удалось открыть .qoderignore. Убедитесь, что каталог репозитория доступен, и повторите попытку."",
  notIndexed: ""Не индексируется"",
  quotaExceededPrefix: ""Достигнут лимит индексов кодовой базы. "",
  quotaExceededUpgrade: ""Обновите тариф"",
  quotaExceededSuffix: "", чтобы продолжить."",
  indexing: ""Индексация"",
  ready: ""Индексация завершена"",
  errors: {
    signedOut: { title: ""Вы не вошли"", description: ""Войдите в Qoder и повторите попытку."" },
    daemonUnavailable: { title: ""Служба индексации недоступна"", description: ""Поддерживайте локальный сеанс запущенным и повторите попытку."" },
    workspaceNotFound: { title: ""Репозиторий не найден"", description: ""Обновите список: репозитории рабочей области могли измениться."" },
    workspaceUnavailable: { title: ""Репозиторий недоступен"", description: ""Каталог был перемещён, удалён или недоступен."" },
    unauthorized: { title: ""Срок авторизации истёк"", description: ""Поддерживайте сеанс входа запущенным и повторите попытку после переподключения."" },
    network: { title: ""Ошибка сети"", description: ""Служба индексации недоступна. Повторите попытку позже."" },
    quotaExceeded: { title: ""Превышена квота индексации"", description: ""У этой учётной записи не осталось квоты на индексы кода."" },
    fileLimitExceeded: { title: ""Превышен автоматический лимит"", description: ""Вы по-прежнему можете обновить этот репозиторий вручную."" },
    unexpected: { title: ""Не удалось выполнить действие с индексом"", description: ""Произошла непредвиденная ошибка. Повторите попытку позже."" }
  }
};
";

        private const string SecurityCopyRu = @"var ruCopy = {
  title: ""Безопасность"",
  description: ""Qoder встраивает безопасность в процесс разработки с помощью трёхуровневого прогрессивного сканирования и автоматических исправлений, помогая делать каждое изменение кода безопасным."",
  scanRecommendationDescription: ""Когда функция включена, Qoder интеллектуально рекомендует соответствующие проверки безопасности на подходящих этапах, а затем сканирует и исправляет проблемы после вашего подтверждения."",
  scanningTiers: ""Уровни сканирования"",
  free: ""Бесплатно"",
  loading: ""Загрузка настроек сканирования безопасности…"",
  loadFailed: ""Не удалось загрузить настройки сканирования безопасности. Повторите попытку."",
  retry: ""Повторить"",
  saveFailed: ""Не удалось сохранить настройку сканирования безопасности. Переключатель восстановлен. Повторите попытку."",
  managedUpdateDenied: ""Это сканирование безопасности управляется вашей организацией и не может быть изменено здесь."",
  enterpriseManaged: ""Управляется"",
  forceOnDescription: ""Ваша организация требует, чтобы это сканирование оставалось включённым."",
  forceOffDescription: ""Ваша организация отключила это сканирование.""
};
";

        private static void PatchPlugins(string dir, Action<string> log, List<string> patched)
        {
            PatchIndexSettings(Path.Combine(dir, PluginFiles[0]), log, patched);
            PatchSecurity(Path.Combine(dir, PluginFiles[1]), log, patched);
            PatchKnowledgeCenter(Path.Combine(dir, PluginFiles[2]), log, patched);
            if (patched.Count > 0) UpdatePluginCatalog(dir, log, patched);
        }

        private static void PatchKnowledgeCenter(string path, Action<string> log, List<string> patched)
        {
            const string label = "Плагин «База знаний»";
            if (!File.Exists(path))
            {
                log(label + ": файл не найден, пропуск.");
                return;
            }
            string backup = path + AsarBackupSuffix;
            if (!File.Exists(backup)) File.Copy(path, backup, false);
            string text = File.ReadAllText(backup, Encoding.UTF8);

            var ru = NewSerializer().Deserialize<Dictionary<string, string>>(ReadResource("qoder-ru-knowledge.json"));
            string ruJson = NewSerializer().Serialize(ru);
            const string original =
                "function copyForLocale(locale) {\n" +
                "  return messages[isChineseLocale(locale) ? \"zh\" : \"en\"];\n" +
                "}";
            string replacement =
                "var ruOverlay = " + ruJson + ";\n" +
                "var ruMessages = Object.assign({}, messages.en, ruOverlay);\n" +
                "function copyForLocale(locale) {\n" +
                "  const q = (globalThis.__qoderRuLang || \"\").toLowerCase() || locale.toLowerCase();\n" +
                "  if (q.startsWith(\"ru\")) return ruMessages;\n" +
                "  return messages[isChineseLocale(locale) ? \"zh\" : \"en\"];\n" +
                "}";
            if (!TryReplaceOnce(ref text, original, replacement, label, log))
                return;

            // Русский язык генерации Repo Wiki: валидация конфига.
            if (!TryReplaceOnce(ref text,
                "return (language === \"en\" || language === \"zh\") && typeof autoUpdate",
                "return (language === \"en\" || language === \"zh\" || language === \"ru\") && typeof autoUpdate",
                label, log)) return;
            // Промпт плана генерации: указание русского языка.
            if (!TryReplaceOnce(ref text,
                "`The generated documentation language must be ${language === \"zh\" ? \"Simplified Chinese\" : \"English\"}.`,",
                "`The generated documentation language must be ${language === \"zh\" ? \"Simplified Chinese\" : language === \"ru\" ? \"Russian\" : \"English\"}.`,",
                label, log)) return;
            // Фильтры переключателя языка (4 места).
            RequireReplaceAll(ref text,
                "(value === \"en\" || value === \"zh\")",
                "(value === \"en\" || value === \"zh\" || value === \"ru\")", label, log);
            // Вкладка «Русский» рядом с English/简体中文 (4 места, импорты отличаются номерами).
            InsertRussianTabTrigger(ref text, label, log);
            // Подпись языка в сводке состояния (2 места).
            RequireReplaceAll(ref text,
                "language === \"zh\" ? \"\\u7B80\\u4F53\\u4E2D\\u6587\" : \"English\"",
                "language === \"ru\" ? copy.repoWikiRussian : language === \"zh\" ? \"\\u7B80\\u4F53\\u4E2D\\u6587\" : \"English\"", label, log);
            // Подпись языка в списке импорта.
            if (!TryReplaceOnce(ref text,
                "document2.language === \"zh\" ? \"\\u7B80\\u4F53\\u4E2D\\u6587\" : document2.language === \"en\" ? \"English\" : document2.language",
                "document2.language === \"ru\" ? copy.repoWikiRussian : document2.language === \"zh\" ? \"\\u7B80\\u4F53\\u4E2D\\u6587\" : document2.language === \"en\" ? \"English\" : document2.language",
                label, log)) return;
            // Ключ repoWikiRussian во всех локалях.
            if (!TryReplaceOnce(ref text,
                "var ruMessages = Object.assign({}, messages.en, ruOverlay);",
                "messages.en.repoWikiRussian = messages.en.repoWikiRussian || \"Russian\";\n" +
                "messages.zh.repoWikiRussian = messages.zh.repoWikiRussian || \"俄语\";\n" +
                "var ruMessages = Object.assign({}, messages.en, ruOverlay);",
                label, log)) return;
            // Экран создания вики показывается только без сгенерированного прогона; RPC
            // updateRepoWikiLanguage в этом состоянии падает для ЛЮБОГО языка, поэтому
            // выбор языка держим локально и отправляем вместе с действием «Сгенерировать».
            if (!TryReplaceOnce(ref text,
                "  const language = repoWiki.config?.language ?? (locale.toLocaleLowerCase().startsWith(\"zh\") ? \"zh\" : \"en\");\n" +
                "  const config2 = {",
                "  const [localLanguage, setLocalLanguage] = (0, import_react.useState)(null);\n" +
                "  const language = localLanguage ?? repoWiki.config?.language ?? (locale.toLocaleLowerCase().startsWith(\"zh\") ? \"zh\" : \"en\");\n" +
                "  const config2 = {",
                label, log)) return;
            if (!TryReplaceOnce(ref text,
                "if (value === \"en\" || value === \"zh\" || value === \"ru\") onLanguageChange(value);",
                "if (value === \"en\" || value === \"zh\" || value === \"ru\") setLocalLanguage(value);",
                label, log)) return;
            // Действие configure (тумблеры) не должно слать ещё не сохранённый «ru».
            if (!TryReplaceOnce(ref text,
                "    citation: repoWiki.config?.citation ?? DEFAULT_REPO_WIKI_CONFIG.citation\n" +
                "  };\n" +
                "  const updateConfig = (patch) => onAction(\"configure\", { ...config2, ...patch });",
                "    citation: repoWiki.config?.citation ?? DEFAULT_REPO_WIKI_CONFIG.citation\n" +
                "  };\n" +
                "  const updateConfig = (patch) => onAction(\"configure\", { ...config2, ...patch, language: repoWiki.config?.language ?? \"en\" });",
                label, log)) return;

            File.WriteAllText(path, text, new UTF8Encoding(false));
            patched.Add(PluginFiles[2]);
            log(label + ": переведён; язык генерации Wiki: добавлен русский.");
        }

        private static void RequireReplaceAll(ref string source, string from, string to, string label, Action<string> log)
        {
            int count = 0;
            int idx = 0;
            while ((idx = source.IndexOf(from, idx, StringComparison.Ordinal)) >= 0)
            {
                source = source.Substring(0, idx) + to + source.Substring(idx + from.Length);
                idx += to.Length;
                count++;
            }
            if (count == 0) log(label + ": шаблон не найден (" + from.Substring(0, Math.Min(40, from.Length)) + "…), пропуск.");
            else log(label + ": заменено вхождений — " + count + ".");
        }

        private static void InsertRussianTabTrigger(ref string source, string label, Action<string> log)
        {
            var regex = new System.Text.RegularExpressions.Regex(
                "(\\(0, import_jsx_runtime(\\d+)\\.jsx\\)\\(import_design_system(\\d+)\\.TabsTrigger, \\{ value: \"zh\"((?:, disabled: running \\|\\| busy)?), children: copy\\.repoWikiSimplifiedChinese \\}\\))");
            string result = regex.Replace(source, "$1, (0, import_jsx_runtime$2.jsx)(import_design_system$3.TabsTrigger, { value: \"ru\"$4, children: copy.repoWikiRussian })");
            int count = regex.Matches(source).Count;
            if (count == 0) log(label + ": вкладки языка не найдены, пропуск.");
            else log(label + ": добавлена вкладка «Русский» в " + count + " мест(а).");
            source = result;
        }

        private static void UpdatePluginCatalog(string dir, Action<string> log, List<string> patched)
        {
            string catalogPath = Path.Combine(dir, PluginCatalogFile);
            if (!File.Exists(catalogPath))
            {
                log("Каталог плагинов: файл не найден, пропуск обновления хэшей.");
                return;
            }
            string backup = catalogPath + AsarBackupSuffix;
            if (!File.Exists(backup)) File.Copy(catalogPath, backup, false);
            string src = File.Exists(backup) ? backup : catalogPath;
            var serializer = NewSerializer();
            var root = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(src, Encoding.UTF8));
            var plugins = root.ContainsKey("plugins") ? root["plugins"] as System.Collections.IList : null;
            if (plugins == null)
            {
                log("Каталог плагинов: структура не распознана, пропуск обновления хэшей.");
                return;
            }
            int updated = 0;
            foreach (object item in plugins)
            {
                var plugin = item as Dictionary<string, object>;
                if (plugin == null || !plugin.ContainsKey("id") || !plugin.ContainsKey("files")) continue;
                string id = plugin["id"] as string;
                var files = plugin["files"] as System.Collections.IList;
                if (string.IsNullOrEmpty(id) || files == null) continue;
                foreach (object fileItem in files)
                {
                    var file = fileItem as Dictionary<string, object>;
                    if (file == null || !file.ContainsKey("path")) continue;
                    string relative = file["path"] as string;
                    if (string.IsNullOrEmpty(relative)) continue;
                    string full = Path.Combine(dir, @"resources\extensions\" + id, relative.Replace('/', '\\'));
                    if (!File.Exists(full)) continue;
                    file["sha256"] = Sha256FileHex(full);
                    updated++;
                }
            }
            File.WriteAllText(catalogPath, serializer.Serialize(root), new UTF8Encoding(false));
            patched.Add(PluginCatalogFile);
            log("Каталог плагинов: обновлено хэшей — " + updated + ".");
        }

        private static void PatchIndexSettings(string path, Action<string> log, List<string> patched)
        {
            const string label = "Плагин «Индекс рабочей области»";
            if (!File.Exists(path))
            {
                log(label + ": файл не найден, пропуск.");
                return;
            }
            string backup = path + AsarBackupSuffix;
            if (!File.Exists(backup)) File.Copy(path, backup, false);
            string text = File.ReadAllText(backup, Encoding.UTF8);

            int insertAt = text.IndexOf("function copyForLocale(locale) {", StringComparison.Ordinal);
            if (insertAt < 0)
            {
                log(label + ": шаблон copyForLocale не найден, пропуск.");
                return;
            }
            text = text.Substring(0, insertAt) + IndexSettingsRu + text.Substring(insertAt);
            if (!TryReplaceOnce(ref text,
                "return locale.toLowerCase().startsWith(\"zh\") ? zh : en;",
                "const q = (globalThis.__qoderRuLang || \"\").toLowerCase() || locale.toLowerCase();\n  return q.startsWith(\"zh\") ? zh : q.startsWith(\"ru\") ? ru : en;",
                label, log))
                return;

            File.WriteAllText(path, text, new UTF8Encoding(false));
            patched.Add(PluginFiles[0]);
            log(label + ": переведён.");
        }

        private static void PatchSecurity(string path, Action<string> log, List<string> patched)
        {
            const string label = "Плагин «Безопасность»";
            if (!File.Exists(path))
            {
                log(label + ": файл не найден, пропуск.");
                return;
            }
            string backup = path + AsarBackupSuffix;
            if (!File.Exists(backup)) File.Copy(path, backup, false);
            string text = File.ReadAllText(backup, Encoding.UTF8);

            int insertAt = text.IndexOf("function copyForLocale(locale) {", StringComparison.Ordinal);
            if (insertAt < 0)
            {
                log(label + ": шаблон copyForLocale не найден, пропуск.");
                return;
            }
            text = text.Substring(0, insertAt) + SecurityCopyRu + text.Substring(insertAt);

            var replacements = new[]
            {
                new[] { "title: { en: \"Static Check\",", "title: { en: \"Static Check\", ru: \"Статическая проверка\"," },
                new[] { "title: { en: \"Lightweight Scan\",", "title: { en: \"Lightweight Scan\", ru: \"Облегчённое сканирование\"," },
                new[] { "title: { en: \"Deep Scan\",", "title: { en: \"Deep Scan\", ru: \"Глубокое сканирование\"," },
                new[] { "en: \"Checks code generated in the current task for common high-risk patterns and fixes them automatically.\",", "en: \"Checks code generated in the current task for common high-risk patterns and fixes them automatically.\",\n      ru: \"Проверяет код, созданный в текущей задаче, на типовые опасные шаблоны и автоматически исправляет их.\"," },
                new[] { "en: \"Analyzes incremental code for risks such as SQL injection, remote command execution, and sensitive data leaks.\",", "en: \"Analyzes incremental code for risks such as SQL injection, remote command execution, and sensitive data leaks.\",\n      ru: \"Анализирует инкрементальные изменения кода на риски, такие как SQL-инъекции, удалённое выполнение команд и утечка конфиденциальных данных.\"," },
                new[] { "en: \"Traces data flows across files and functions to uncover hidden cross-file vulnerabilities.\",", "en: \"Traces data flows across files and functions to uncover hidden cross-file vulnerabilities.\",\n      ru: \"Отслеживает потоки данных между файлами и функциями, чтобы выявить скрытые межфайловые уязвимости.\"," },
                new[] { "return locale.toLowerCase().startsWith(\"zh\") ? value.zh : value.en;", "const q = (globalThis.__qoderRuLang || \"\").toLowerCase() || locale.toLowerCase();\n  return q.startsWith(\"zh\") ? value.zh : q.startsWith(\"ru\") && value.ru ? value.ru : value.en;" },
                new[] { "if (locale.toLowerCase().startsWith(\"zh\")) {", "const q = (globalThis.__qoderRuLang || \"\").toLowerCase() || locale.toLowerCase();\n  if (q.startsWith(\"ru\")) return ruCopy;\n  if (q.startsWith(\"zh\")) {" },
            };
            foreach (string[] pair in replacements)
            {
                if (!TryReplaceOnce(ref text, pair[0], pair[1], label, log))
                    return;
            }

            File.WriteAllText(path, text, new UTF8Encoding(false));
            patched.Add(PluginFiles[1]);
            log(label + ": переведён.");
        }

        private static string Sha256FileHex(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static bool TryReplaceOnce(ref string source, string from, string to, string label, Action<string> log)
        {
            int idx = source.IndexOf(from, StringComparison.Ordinal);
            if (idx < 0)
            {
                log(label + ": шаблон не найден (" + from.Substring(0, Math.Min(40, from.Length)) + "…), пропуск.");
                return false;
            }
            if (source.IndexOf(from, idx + 1, StringComparison.Ordinal) >= 0)
            {
                log(label + ": шаблон встречается более одного раза, пропуск.");
                return false;
            }
            source = source.Substring(0, idx) + to + source.Substring(idx + from.Length);
            return true;
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

        internal static int CompareVersions(string a, string b)
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

        private static string BuildObserver(string mapJson, string fragmentJson)
        {
            return ";globalThis.__qoderRuObserver=1;(()=>{const M=" + mapJson + ",F=" + fragmentJson +
                ";const KS=Object.keys(F).sort((a,b)=>b.length-a.length);" +
                "const RX=KS.map(k=>{const esc=k.replace(/[.*+?^${}()|[\\]\\\\]/g,\"\\\\$&\");const lb=/^[A-Za-z0-9]/.test(k)?\"(?<![A-Za-z0-9])\":\"\";const la=/[A-Za-z0-9]$/.test(k)?\"(?![A-Za-z0-9])\":\"\";return[new RegExp(lb+esc+la,\"g\"),F[k]]});" +
                "const SK=n=>{const p=n.parentElement;return p&&/^(STYLE|SCRIPT|TEXTAREA|CODE|PRE)$/.test(p.tagName)};" +
                "const TR=s=>{const t=s.trim(),r=M[t];if(r&&r!==t)return s.replace(t,r);if(t.length>3){let o=s;for(const x of RX)o=o.replace(x[0],x[1]);if(o!==s)return o}return s};" +
                "const T=n=>{if(n.nodeType===3){if(SK(n))return;const r=TR(n.nodeValue);if(r!==n.nodeValue)n.nodeValue=r}" +
                "else if(n.nodeType===1||n.nodeType===11){if(n.nodeType===1)for(const a of[\"title\",\"aria-label\",\"placeholder\",\"alt\"]){const v=n.getAttribute&&n.getAttribute(a);if(v&&M[v])n.setAttribute(a,M[v])}" +
                "if(n.shadowRoot){T(n.shadowRoot);O(n.shadowRoot)}for(const c of n.childNodes)T(c)}};" +
                "const W=new WeakSet();" +
                "const O=r=>{if(!r||W.has(r))return;W.add(r);const o=new MutationObserver(ms=>{for(const m of ms){if(m.type===\"characterData\")T(m.target);else for(const n of m.addedNodes)T(n)}});o.observe(r,{subtree:true,childList:true,characterData:true})};" +
                "const S=()=>{const b=document.body;if(!b)return;T(b);O(b)};" +
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
