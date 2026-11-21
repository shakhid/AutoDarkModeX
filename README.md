# AutoDarkModeX

**English** · [Русский](#русский)

Automatically switches Windows between light and dark theme — by sunrise and sunset at your location, or at times you choose.

<p align="center">
  <img src="assets/screenshot-light.png" width="380" alt="AutoDarkModeX — light theme">
  <img src="assets/screenshot-dark.png" width="380" alt="AutoDarkModeX — dark theme">
</p>

## Features

- **Three modes:** Light, Auto (on a schedule) and Dark.
- **Schedule by location:** sunrise and sunset are calculated for your place every day, including summer and winter time changes.
- **Schedule by time:** two times of your own for the light and the dark theme.
- **Location:** from the Windows location service or by searching a city by name; coordinates can also be entered by hand.
- **Separate switches** for the taskbar and Start menu and for apps.
- **Runs in the background:** starts with Windows, optional tray icon. The theme also switches while the window is hidden, after waking from sleep and after unlocking; if the taskbar gets stuck in the old theme, the app makes it repaint.
- **Windows 11 look:** rounded corners, Mica background, your Windows accent colour, smooth minimize.
- **Update check:** About → Check for updates looks for a newer release here on GitHub and can download and install it.
- **Languages:** English, Русский, Українська, Español, Français, Nederlands, Deutsch, Italiano, Português, Polski, Türkçe.

## Installation

Download `AutoDarkModeX-Setup-<version>.exe` from [Releases](../../releases) and run it.
The app installs to `C:\Program Files\AutoDarkModeX` (you can choose another folder). Settings are stored in `%APPDATA%\AutoDarkModeX`.

Requirements: Windows 10 or 11 (.NET Framework 4.x is already part of Windows).

## Building

You need the Mono C# compiler (`mcs`, with Windows Forms) and NSIS (`makensis`):

```sh
./build.sh
```

This produces `AutoDarkModeX.exe` and the installer `AutoDarkModeX-Setup-<version>.exe`. The version is set in the `VERSION` file.

## Project structure

| Path | Contents |
|---|---|
| `src/Main.cs` | main window and theme switching logic |
| `src/Controls.cs` | UI controls and colour palette |
| `src/Core.cs` | settings, translations, location |
| `src/Sun.cs` | sunrise and sunset calculation |
| `src/About.cs` | About window |
| `installer.nsi` | installer (NSIS) |
| `assets/` | icon and screenshots |

---

<a id="русский"></a>

# AutoDarkModeX

[English](#autodarkmodex) · **Русский**

Автоматически переключает Windows между светлой и тёмной темой — по восходу и закату в вашем городе или по заданному времени.

## Возможности

- **Три режима:** «Светлая», «Авто» (по расписанию) и «Тёмная».
- **Расписание по геолокации:** время восхода и заката рассчитывается для вашего места каждый день, с учётом перехода на летнее и зимнее время.
- **Расписание по времени:** два своих времени для светлой и тёмной темы.
- **Местоположение:** через службу геолокации Windows или поиском города по названию; координаты можно ввести вручную.
- **Отдельные переключатели** для панели задач и меню «Пуск» и для приложений.
- **Работает в фоне:** запуск вместе с Windows, значок в трее по желанию. Тема переключается и при скрытом окне, после выхода из сна и после разблокировки; если панель задач «застряла» в старой теме, программа её перерисовывает.
- **Интерфейс в стиле Windows 11:** скруглённые углы, фон Mica, цвет акцента Windows, плавное сворачивание.
- **Проверка обновлений:** «О программе» → «Проверить обновления» ищет новый релиз здесь, на GitHub, и может сам скачать и установить его.
- **Языки:** English, Русский, Українська, Español, Français, Nederlands, Deutsch, Italiano, Português, Polski, Türkçe.

## Установка

Скачайте `AutoDarkModeX-Setup-<версия>.exe` из раздела [Releases](../../releases) и запустите.
Программа ставится в `C:\Program Files\AutoDarkModeX` (папку можно выбрать). Настройки хранятся в `%APPDATA%\AutoDarkModeX`.

Требования: Windows 10 или 11 (.NET Framework 4.x уже встроен в систему).

## Сборка

Нужны компилятор C# из Mono (`mcs`, с Windows Forms) и NSIS (`makensis`):

```sh
./build.sh
```

Получатся `AutoDarkModeX.exe` и установщик `AutoDarkModeX-Setup-<версия>.exe`. Версия задаётся в файле `VERSION`.

## Структура

| Путь | Что внутри |
|---|---|
| `src/Main.cs` | главное окно и логика переключения темы |
| `src/Controls.cs` | элементы интерфейса и палитра |
| `src/Core.cs` | настройки, переводы, местоположение |
| `src/Sun.cs` | расчёт восхода и заката |
| `src/About.cs` | окно «О программе» |
| `installer.nsi` | установщик (NSIS) |
| `assets/` | иконка и скриншоты |

---

© 2026 Shax
