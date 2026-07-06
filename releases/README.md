# Releases

Локальные сборки (exe, zip) **не хранятся в git** — только эта инструкция.

## Локально

```bat
create-release.bat 1.3.0
```

Результат: `releases\v1.3.0\`

## GitHub Releases

Страница [Releases](https://github.com/sokol6020/custom_stt/releases) заполняется одним из способов:

### 1. Токен (рекомендуется, сразу после локальной сборки)

```bat
set GITHUB_TOKEN=ghp_ваш_токен
create-release.bat 1.3.0
```

Скрипт загрузит `customSTT.exe` и zip в GitHub Releases.

### 2. GitHub Actions (тег)

Если `GITHUB_TOKEN` не задан, `create-release.bat` отправляет тег `v1.3.0` — CI собирает и публикует релиз.

Пересборка существующего тега:

```bat
git tag -d v1.3.0
git push origin :refs/tags/v1.3.0
create-release.bat 1.3.0
```
