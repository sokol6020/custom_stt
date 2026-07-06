# customSTT / Speech to Text

Desktop speech-to-text app for Windows. Records microphone audio, transcribes it locally with [Whisper.NET](https://github.com/sandrohanea/whisper.net), and inserts the result into the active text field.

**Version:** 1.3.0  
**Stack:** .NET 10, WPF, NAudio, Whisper.NET

---

## English

### Features

- Local speech recognition (no cloud) with Whisper models: `tiny`, `base`, `small`, `medium`
- Global hotkey for recording (toggle or hold-to-talk)
- Overlay with recording/processing status
- System tray support
- Transcription history
- GPU acceleration (NVIDIA CUDA / AMD & Intel Vulkan) — optional in settings
- Automatic text injection into the focused window

### Requirements

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Microphone
- For GPU: NVIDIA drivers (CUDA) or Vulkan-compatible GPU drivers

### Build (development)

```bat
dotnet build customSTT\customSTT.csproj --configuration Debug
```

Or use the helper script:

```bat
build.bat
```

### Run (development)

```bat
run.bat
```

This builds Debug and starts `customSTT\bin\Debug\net10.0-windows\customSTT.exe`.

You can also run directly:

```bat
dotnet run --project customSTT\customSTT.csproj
```

> Prefer `run.bat` or the `.exe` so the correct app icon appears in the taskbar (not `dotnet.exe`).

Start hidden in the system tray:

```bat
publish\customSTT.exe --minimize-to-tray
```

Or enable **Hide in tray on startup** in Settings → Behavior.

Aliases: `--tray`, `--minimized`, `/minimize-to-tray`.

### Production build

Creates a self-contained `win-x64` build in the `publish\` folder:

```bat
build-prod.bat
```

Run:

```bat
publish\customSTT.exe
```

> Always run the app from `publish\` after `build-prod.bat`.  
> `run.bat` starts **Debug** from `bin\Debug\...` — it is not the production build.  
> Old copies in `releases\` are not updated automatically.

On first run, the selected Whisper model is downloaded into `whisper-models\` next to the executable.

### Release package

Create a versioned release folder with `customSTT.exe`, a full app zip, and release notes:

```bat
create-release.bat 1.2.0
```

Or with inline notes:

```bat
create-release.bat 1.2.0 "Hold-to-talk hotkey, GPU Vulkan/CUDA, UI fixes"
```

You can also prepare notes in `release-notes\1.2.0.txt` before running the script.

Output:

```
releases\v1.2.0\
  customSTT.exe                  # application executable
  customSTT-1.2.0-win-x64.zip    # full self-contained app archive
  RELEASE_NOTES.txt
  BUILD_INFO.txt
```

`build-prod.bat` always performs a clean rebuild (`dotnet clean`, wipes `publish\`) so the output is not stale.

### Default shortcuts

| Action | Default |
|--------|---------|
| Start/stop recording | `Ctrl+Alt+A` |
| Show/hide overlay | `F1` |

Both can be changed in **Settings**.

### Project layout

```
customSTT/
├── customSTT/          # WPF application
├── tools/              # Icon generator
├── build.bat           # Debug build
├── build-prod.bat      # Release publish
├── run.bat             # Build & run Debug
└── customSTT.sln
```

---

## Русский

Настольное приложение для преобразования речи в текст для Windows. Записывает звук с микрофона, расшифровывает его локально с помощью Whisper.NET и вставляет результат в активное текстовое поле.

Стек: .NET 10, WPF, NAudio, Whisper.NET

### Возможности

- Локальное распознавание речи (без облака) на моделях Whisper: `tiny`, `base`, `small`, `medium`
- Глобальная горячая клавиша записи (режим нажатия или удержания)
- Оверлей со статусом записи и обработки
- Работа из системного трея
- История транскрипций
- GPU-ускорение (NVIDIA CUDA / AMD и Intel Vulkan) — переключается в настройках
- Автоматическая вставка текста в активное окно

### Требования

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Микрофон
- Для GPU: драйверы NVIDIA (CUDA) или видеокарта с поддержкой Vulkan

### Сборка (разработка)

```bat
dotnet build customSTT\customSTT.csproj --configuration Debug
```

Или скрипт:

```bat
build.bat
```

### Запуск (разработка)

```bat
run.bat
```

Скрипт собирает Debug и запускает `customSTT\bin\Debug\net10.0-windows\customSTT.exe`.

Можно также:

```bat
dotnet run --project customSTT\customSTT.csproj
```

> Лучше использовать `run.bat` или `.exe`, чтобы в панели задач была иконка приложения, а не `dotnet.exe`.

Запуск сразу в трей (окно не показывается):

```bat
publish\customSTT.exe --minimize-to-tray
```

Или включите **Скрывать в трей при запуске** в Настройки → Поведение.

Также: `--tray`, `--minimized`, `/minimize-to-tray`.

### Продакшен-сборка

Самодостаточная сборка `win-x64` в папку `publish\`:

```bat
build-prod.bat
```

Запуск:

```bat
publish\customSTT.exe
```

> После `build-prod.bat` запускайте только `publish\customSTT.exe`.  
> `run.bat` собирает и запускает **Debug** из `bin\Debug\...` — это не prod-сборка.  
> Старые копии в `releases\` сами не обновляются.

При первом запуске выбранная модель Whisper скачивается в `whisper-models\` рядом с исполняемым файлом.

### Релиз

Создание релиза с `customSTT.exe`, zip-архивом и описанием изменений:

```bat
create-release.bat 1.2.0
```

Или с текстом изменений в командной строке:

```bat
create-release.bat 1.2.0 "Режим удержания hotkey, GPU Vulkan/CUDA, исправления UI"
```

Можно заранее положить заметки в `release-notes\1.2.0.txt`.

Результат:

```
releases\v1.2.0\
  customSTT.exe
  customSTT-1.2.0-win-x64.zip
  RELEASE_NOTES.txt
  BUILD_INFO.txt
```

`build-prod.bat` всегда делает чистую пересборку (`dotnet clean`, очистка `publish\`), чтобы не попадала старая версия.

### Горячие клавиши по умолчанию

| Действие | По умолчанию |
|----------|----------------|
| Старт/стоп записи | `Ctrl+Alt+A` |
| Показать/скрыть оверлей | `F1` |

Настраиваются в разделе **Настройки**.

### Структура проекта

```
customSTT/
├── customSTT/          # WPF-приложение
├── tools/              # Генератор иконки
├── build.bat           # Debug-сборка
├── build-prod.bat      # Release publish
├── run.bat             # Сборка и запуск Debug
└── customSTT.sln
```

---

## License

MIT (see project sources). Whisper models and Whisper.NET have their own licenses.
