## 🇷🇺 Русский

### Исправления
- Ошибка «Операцию не удалось выполнить. Попробуйте снова.» при выборе языка «Русский» на экране создания Repo Wiki: до первой генерации язык теперь хранится локально и передаётся вместе с действием «Сгенерировать», без преждевременного обращения к серверу.
- Установщик видел старую версию Qoder (например 0.5.2) после внутреннего обновления программы до 0.5.5: внутренние обновления кладутся в `<папка>\.qoder-versions\<версия>`, и установщик теперь определяет активную копию по ней и по `state.ini` лаунчера Qoder.
- Сообщение о несовпадении версий теперь показывает путь активной копии Qoder.

### Что нового
- Режим командной строки `--detect <папка> --log <файл>`: показывает активную копию Qoder и её версию без установки.
- Вкладка «Русский» в выборе языка Repo Wiki и генерация документации на русском (промпт плана учитывает русский язык).

### Установка
Полностью закройте Qoder (включая значок в трее), скачайте архив ниже, распакуйте и запустите `QoderRU-Setup-0.5.5.exe`. Папка Qoder определится автоматически, включая копии из `.qoder-versions`.

## 🇬🇧 English

### Bug Fixes
- "Operation failed. Please try again." error when picking Russian on the Repo Wiki creation screen: before the first generation the language is now kept locally and sent with the Generate action, without a premature server call.
- Installer detected an old Qoder version (e.g. 0.5.2) after the app self-updated to 0.5.5: internal updates live in `<folder>\.qoder-versions\<version>`, and the installer now resolves the active copy from there and from the Qoder launcher `state.ini`.
- Version mismatch error now shows the path of the active Qoder copy.

### What's New
- Command-line mode `--detect <folder> --log <file>`: prints the active Qoder copy and its version without installing.
- Russian tab in the Repo Wiki language picker and Russian documentation generation (the plan prompt honors Russian).

### Installation
Fully quit Qoder (including the tray icon), download the archive below, unpack it and run `QoderRU-Setup-0.5.5.exe`. The Qoder folder is detected automatically, including `.qoder-versions` copies.

## Downloads

| Platform | File |
|----------|------|
| Windows x64 | [`qoder-ru-localization-0.5.5-2-windows-x64.zip`](../../releases/download/v0.5.5-2/qoder-ru-localization-0.5.5-2-windows-x64.zip) |
| Checksums | [`checksums.txt`](../../releases/download/v0.5.5-2/checksums.txt) |