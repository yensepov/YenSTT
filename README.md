# YenSTT

[![Build](https://github.com/yensepov/YenSTT/actions/workflows/build.yml/badge.svg)](https://github.com/yensepov/YenSTT/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/yensepov/YenSTT?display_name=tag)](https://github.com/yensepov/YenSTT/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-167D6B.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-167D6B.svg)](#системные-требования)

**YenSTT** — бесплатная утилита голосового ввода для Windows. Она записывает речь с микрофона, распознаёт её локально через faster-whisper и вставляет текст в активное окно.

Аудио не отправляется в OpenAI или другие облачные сервисы. API-ключ, подписка и установленный Python не требуются.

## Скачать

Откройте раздел [Releases](https://github.com/yensepov/YenSTT/releases) и скачайте один из файлов:

- `YenSTT-Setup-vX.Y.Z-win-x64.exe` — рекомендуемый установщик;
- `YenSTT-vX.Y.Z-win-x64-portable.zip` — portable-версия без установки;
- `SHA256SUMS.txt` — контрольные суммы файлов релиза.

Установщик создаёт ярлык в меню «Пуск» и предлагает опциональные ярлык на рабочем столе и автозапуск с Windows.

Текущие релизы не подписаны коммерческим Windows code-signing сертификатом, поэтому SmartScreen может показать предупреждение для нового приложения. Скачивайте файлы только из официального GitHub Releases и сверяйте SHA-256 с `SHA256SUMS.txt`.

## Возможности

- Локальная модель Whisper `small`, оптимизированная для CPU в режиме `int8`.
- Русский язык и автоматическое определение языка.
- Глобальная горячая клавиша с режимами push-to-talk и toggle.
- Возврат фокуса и автоматическая вставка текста в исходное окно.
- Словарь имён и специальных терминов.
- Голосовые команды пунктуации.
- Автостоп по тишине и обработка длинных записей частями.
- Локальные логи и необязательное сохранение исходного аудио.

## Системные требования

- Windows 10 или Windows 11, x64;
- процессор с поддержкой современных x64-инструкций;
- микрофон;
- около 400 МБ для приложения;
- около 464 МБ для модели `small`;
- интернет при первом распознавании для однократного скачивания модели.

После загрузки модели распознавание работает офлайн. Скорость зависит от процессора и длительности записи.

## Использование

По умолчанию используется сочетание `Ctrl+Alt+Space`.

- **Push-to-talk:** удерживайте сочетание во время речи и отпустите для распознавания.
- **Toggle:** нажмите один раз для начала записи и ещё раз для остановки.

При первом распознавании YenSTT показывает сообщение о скачивании модели. Модель сохраняется в `%APPDATA%\YenSTT\models` и повторно не загружается.

Дважды щёлкните значок YenSTT в системном трее, чтобы открыть настройки микрофона, языка, горячей клавиши и вставки текста.

## Приватность

- Запись и распознавание выполняются локально.
- В сеть не отправляются аудио, распознанный текст или словарь терминов.
- Сетевое соединение используется только для первого скачивания модели с Hugging Face.
- Настройки, модели и логи хранятся в `%APPDATA%\YenSTT`.

Подробнее: [SECURITY.md](SECURITY.md).

## Сборка из исходников

Для обычной разработки требуются .NET 8 SDK и Python 3.13:

```powershell
py -3 -m pip install -r Python\requirements-build.txt
dotnet build YenSTT.csproj -c Release
```

Полная сборка релиза:

```powershell
.\scripts\build-release.ps1 -Version 1.0.0
```

Скрипт создаёт автономный faster-whisper runtime, публикует WinForms-приложение, собирает Inno Setup installer, portable ZIP и SHA-256 checksums в `artifacts\release`.

## Архитектура

```text
микрофон → WAV 16 kHz mono → локальный faster-whisper small
         → обработка текста → вставка в активное окно
```

YenSTT держит worker-процесс загруженным между диктовками, поэтому модель не инициализируется заново при каждом запросе.

## Поддержка и участие

- Ошибка: [создать bug report](https://github.com/yensepov/YenSTT/issues/new?template=bug_report.yml)
- Идея: [предложить функцию](https://github.com/yensepov/YenSTT/issues/new?template=feature_request.yml)
- Участие в разработке: [CONTRIBUTING.md](CONTRIBUTING.md)
- История изменений: [CHANGELOG.md](CHANGELOG.md)

## Лицензия

Исходный код YenSTT распространяется по лицензии [MIT](LICENSE). Сторонние компоненты и загружаемая модель имеют собственные лицензии; см. [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
