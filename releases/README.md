# Releases

Локальные сборки релизов (exe, zip) **не хранятся в git** — они появляются после:

```bat
create-release.bat 1.3.0
```

Результат: `releases\v1.3.0\`

## GitHub Releases

Публичные релизы публикуются автоматически при push тега `v*`:

```bat
create-release.bat 1.3.0
```

Скрипт создаёт локальную папку, затем тег `v1.3.0` и отправляет его на GitHub.  
Workflow `.github/workflows/release.yml` собирает приложение и прикрепляет `customSTT.exe` и zip-архив к [GitHub Releases](https://github.com/sokol6020/custom_stt/releases).

Если тег уже существует и нужна пересборка:

```bat
git tag -d v1.3.0
git push origin :refs/tags/v1.3.0
create-release.bat 1.3.0
```
