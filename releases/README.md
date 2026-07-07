# Releases

Локальные сборки (exe, zip) **не хранятся в git** — только эта инструкция.

## Локально

```bat
create-release.bat 1.3.0
```

Результат: `releases\v1.3.0\`

## GitHub Releases

### 1. Токен в конфиге (рекомендуется)

```bat
copy config\release.env.example config\release.env
```

Откройте `config\release.env`, укажите `GITHUB_TOKEN` (scope: **repo**).  
Файл в `.gitignore`, в репозиторий не попадёт.

```bat
create-release.bat 1.3.0
```

Скрипт загрузит zip-архив на [GitHub Releases](https://github.com/sokol6020/custom_stt/releases).

Только загрузка уже собранного релиза:

```bat
powershell -File scripts\publish-github-release.ps1 -Version 1.3.0
```

### 2. GitHub Actions (тег)

Если `config\release.env` не задан, `create-release.bat` отправляет тег `v1.3.0` — CI собирает и публикует релиз.

Пересборка существующего тега:

```bat
git tag -d v1.3.0
git push origin :refs/tags/v1.3.0
create-release.bat 1.3.0
```
