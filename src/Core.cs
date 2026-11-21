using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoDarkMode
{
    static class Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr h);
        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS { public int L, R, T, B; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int L, T, R, B; }
        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [DllImport("user32.dll")]
        public static extern int FillRect(IntPtr hdc, ref RECT r, IntPtr brush);
        [DllImport("gdi32.dll")]
        public static extern IntPtr GetStockObject(int i);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr dc, int x, int y);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    // ---------- Localization: EN, RU, FR, NL, DE ----------
    static class L
    {
        public static int Lang;
        public static readonly string[] Codes = { "EN", "RU", "FR", "NL", "DE", "UA", "ES" };
        public static readonly string[] Iso = { "en", "ru", "fr", "nl", "de", "uk", "es" };   // for web services
        public static readonly string[] Names = { "English", "Русский", "Français", "Nederlands", "Deutsch", "Українська", "Español" };
        static Dictionary<string, string[]> D = new Dictionary<string, string[]>();
        static void A(string k, params string[] v) { D[k] = v; }

        static L()
        {
            // order: EN, RU, FR, NL, DE, UK (Ukrainian), ES (Spanish)
            A("title", "AutoDarkModeX", "AutoDarkModeX", "AutoDarkModeX", "AutoDarkModeX", "AutoDarkModeX", "AutoDarkModeX", "AutoDarkModeX");
            A("light", "Light", "Светлая", "Clair", "Licht", "Hell", "Світла", "Claro");
            A("dark", "Dark", "Тёмная", "Sombre", "Donker", "Dunkel", "Темна", "Oscuro");
            A("auto", "Auto", "Авто", "Auto", "Auto", "Auto", "Авто", "Auto");
            A("next", "{0} at {1} · in {2}", "{0} в {1} · через {2}", "{0} à {1} · dans {2}", "{0} om {1} · over {2}", "{0} um {1} · in {2}", "{0} о {1} · через {2}", "{0} a las {1} · en {2}");
            A("hm", "{0} h {1} min", "{0} ч {1} мин", "{0} h {1} min", "{0} u {1} min", "{0} Std. {1} Min.", "{0} год {1} хв", "{0} h {1} min");
            A("m", "{0} min", "{0} мин", "{0} min", "{0} min", "{0} Min.", "{0} хв", "{0} min");
            A("auto_off", "Automatic switching is off", "Автопереключение выключено", "Changement automatique désactivé", "Automatisch wisselen staat uit", "Automatischer Wechsel ist aus", "Автоперемикання вимкнено", "Cambio automático desactivado");
            A("polar_day", "Polar day: light theme all day", "Полярный день: светлая тема весь день", "Jour polaire : thème clair toute la journée", "Poolse dag: de hele dag licht thema", "Polartag: ganztägig helles Design", "Полярний день: світла тема весь день", "Día polar: tema claro todo el día");
            A("polar_night", "Polar night: dark theme all day", "Полярная ночь: тёмная тема весь день", "Nuit polaire : thème sombre toute la journée", "Poolnacht: de hele dag donker thema", "Polarnacht: ganztägig dunkles Design", "Полярна ніч: темна тема весь день", "Noche polar: tema oscuro todo el día");
            A("schedule", "Schedule", "Расписание", "Planification", "Schema", "Zeitplan", "Розклад", "Horario");
            A("mode_sun", "By location", "По геолокации", "Par position", "Op locatie", "Nach Standort", "За геолокацією", "Por ubicación");
            A("mode_time", "Time", "Время", "Heure", "Tijd", "Uhrzeit", "Час", "Hora");
            A("my_location", "My location", "Моё местоположение", "Ma position", "Mijn locatie", "Mein Standort", "Моє місцезнаходження", "Mi ubicación");
            A("geo_title", "Location", "Местоположение", "Position", "Locatie", "Standort", "Місцезнаходження", "Ubicación");
            A("geo_off", "Windows location service is off or unavailable.\n\nTurn it on in Windows settings (Privacy & security → Location), or find your city by name.",
                         "Служба определения местоположения Windows выключена или недоступна.\n\nВключите её в настройках Windows (Конфиденциальность и защита → Расположение) или найдите город по названию.",
                         "Le service de localisation de Windows est désactivé ou indisponible.\n\nActivez-le dans les paramètres Windows (Confidentialité et sécurité → Localisation) ou cherchez votre ville par son nom.",
                         "De locatievoorziening van Windows staat uit of is niet beschikbaar.\n\nZet deze aan in de Windows-instellingen (Privacy en beveiliging → Locatie) of zoek je stad op naam.",
                         "Der Windows-Standortdienst ist aus oder nicht verfügbar.\n\nAktivieren Sie ihn in den Windows-Einstellungen (Datenschutz und Sicherheit → Standort) oder suchen Sie Ihre Stadt nach Namen.", "Служба визначення місцезнаходження Windows вимкнена або недоступна.\n\nУвімкніть її в налаштуваннях Windows (Конфіденційність і захист → Розташування) або знайдіть місто за назвою.", "El servicio de ubicación de Windows está desactivado o no está disponible.\n\nActívelo en la configuración de Windows (Privacidad y seguridad → Ubicación) o busque su ciudad por nombre.");
            A("find_city", "Find city", "Найти город", "Chercher la ville", "Stad zoeken", "Stadt suchen", "Знайти місто", "Buscar ciudad");
            A("geo_settings", "Location settings", "Настройки Windows", "Paramètres", "Instellingen", "Einstellungen", "Налаштування Windows", "Configuración");
            A("city", "City", "Город", "Ville", "Stad", "Stadt", "Місто", "Ciudad");
            A("find", "Search", "Найти", "Chercher", "Zoeken", "Suchen", "Знайти", "Buscar");
            A("searching", "Searching…", "Ищем…", "Recherche…", "Zoeken…", "Suche…", "Шукаємо…", "Buscando…");
            A("not_found", "Nothing found", "Ничего не найдено", "Aucun résultat", "Niets gevonden", "Nichts gefunden", "Нічого не знайдено", "No se encontró nada");
            A("search_fail", "Search service is not reachable", "Сервис поиска недоступен", "Service de recherche injoignable", "Zoekdienst niet bereikbaar", "Suchdienst nicht erreichbar", "Сервіс пошуку недоступний", "El servicio de búsqueda no responde");
            A("search_hint", "Type a city and press Enter", "Введите город и нажмите Enter", "Saisissez une ville puis Entrée", "Typ een stad en druk op Enter", "Stadt eingeben und Enter drücken", "Введіть місто й натисніть Enter", "Escriba una ciudad y pulse Enter");
            A("or_coords", "or enter coordinates", "или введите координаты", "ou saisissez des coordonnées", "of voer coördinaten in", "oder Koordinaten eingeben", "або введіть координати", "o introduzca coordenadas");
            A("custom_loc", "Custom location", "Свои координаты", "Position personnalisée", "Eigen locatie", "Eigener Standort", "Власні координати", "Ubicación personalizada");
            A("detecting", "Detecting location…", "Определяем местоположение…", "Détection de la position…", "Locatie bepalen…", "Standort wird ermittelt…", "Визначаємо місцезнаходження…", "Detectando ubicación…");
            A("detect_fail", "Couldn't detect location", "Не удалось определить место", "Détection impossible", "Locatie niet gevonden", "Standort nicht ermittelt", "Не вдалося визначити місце", "No se pudo detectar la ubicación");
            A("refresh_tip", "Detect location again", "Определить местоположение заново", "Redétecter la position", "Locatie opnieuw bepalen", "Standort neu ermitteln", "Визначити місцезнаходження знову", "Detectar la ubicación de nuevo");
            A("loc_tip", "Click to enter coordinates", "Нажмите, чтобы ввести координаты", "Cliquez pour saisir les coordonnées", "Klik om coördinaten in te voeren", "Klicken, um Koordinaten einzugeben", "Натисніть, щоб ввести координати", "Haga clic para introducir coordenadas");
            A("coords", "Coordinates", "Координаты", "Coordonnées", "Coördinaten", "Koordinaten", "Координати", "Coordenadas");
            A("lat", "Lat", "Шир.", "Lat", "Br.", "Br.", "Шир.", "Lat");
            A("lon", "Lon", "Дол.", "Lon", "Le.", "Lä.", "Довг.", "Lon");
            A("save", "Save", "Сохранить", "Enregistrer", "Opslaan", "Speichern", "Зберегти", "Guardar");
            A("cancel", "Cancel", "Отмена", "Annuler", "Annuleren", "Abbrechen", "Скасувати", "Cancelar");
            A("opt_system", "Taskbar and Start", "Панель задач и Пуск", "Barre des tâches et Démarrer", "Taakbalk en Start", "Taskleiste und Start", "Панель завдань і Пуск", "Barra de tareas e Inicio");
            A("opt_apps", "Apps", "Приложения", "Applications", "Apps", "Apps", "Програми", "Aplicaciones");
            A("opt_startup", "Start with Windows", "Запуск с Windows", "Démarrer avec Windows", "Starten met Windows", "Mit Windows starten", "Запуск із Windows", "Iniciar con Windows");
            A("opt_tray", "Minimize to tray", "Сворачивать в трей", "Réduire dans la zone de notification", "Minimaliseren naar systeemvak", "In den Infobereich minimieren", "Згортати в трей", "Minimizar a la bandeja");
            A("about", "About", "О программе", "À propos", "Over", "Info", "Про програму", "Acerca de");
            A("about_desc", "Switches Windows between light and dark theme by time or by your location.", "Переключает тему Windows (светлая или тёмная) по времени или местоположению.", "Bascule Windows entre le thème clair et sombre selon l’heure ou votre position.", "Schakelt Windows tussen licht en donker thema op basis van tijd of locatie.", "Wechselt Windows nach Uhrzeit oder Standort zwischen hellem und dunklem Design.", "Перемикає тему Windows (світла чи темна) за часом або місцезнаходженням.", "Cambia Windows entre el tema claro y el oscuro según la hora o su ubicación.");
            A("version", "Version", "Версия", "Version", "Versie", "Version", "Версія", "Versión");
            A("close", "Close", "Закрыть", "Fermer", "Sluiten", "Schließen", "Закрити", "Cerrar");
            A("minimize", "Minimize", "Свернуть", "Réduire", "Minimaliseren", "Minimieren", "Згорнути", "Minimizar");
            A("tray_open", "Open", "Открыть", "Ouvrir", "Openen", "Öffnen", "Відкрити", "Abrir");
            A("tray_exit", "Exit", "Выход", "Quitter", "Afsluiten", "Beenden", "Вийти", "Salir");
            A("tray_hint", "Still running in the tray", "Программа работает в трее", "Toujours actif dans la zone de notification", "Draait nog in het systeemvak", "Läuft weiter im Infobereich", "Програма працює в треї", "Sigue funcionando en la bandeja");
            A("check_updates", "Check for updates", "Проверить обновления", "Mises à jour", "Updates zoeken", "Nach Updates suchen", "Перевірити оновлення", "Buscar actualizaciones");
            A("checking", "Checking…", "Проверка…", "Vérification…", "Controleren…", "Wird geprüft…", "Перевірка…", "Comprobando…");
            A("up_to_date", "Latest version", "Последняя версия", "Dernière version", "Nieuwste versie", "Aktuelle Version", "Остання версія", "Última versión");
            A("update_avail", "Version {0} available", "Доступна версия {0}", "Version {0} disponible", "Versie {0} beschikbaar", "Version {0} verfügbar", "Доступна версія {0}", "Versión {0} disponible");
            A("update_now", "Update", "Обновить", "Mettre à jour", "Bijwerken", "Aktualisieren", "Оновити", "Actualizar");
            A("downloading", "Downloading…", "Загрузка…", "Téléchargement…", "Downloaden…", "Wird geladen…", "Завантаження…", "Descargando…");
            A("update_fail", "Couldn't check", "Не удалось проверить", "Vérification impossible", "Controle mislukt", "Prüfung fehlgeschlagen", "Не вдалося перевірити", "No se pudo comprobar");
        }

        public static string T(string k)
        {
            string[] v;
            return D.TryGetValue(k, out v) ? v[Lang] : k;
        }

        public static string Duration(TimeSpan d)
        {
            int mins = Math.Max(1, (int)Math.Ceiling(d.TotalMinutes));
            int h = mins / 60, m = mins % 60;
            return h > 0 ? string.Format(T("hm"), h, m) : string.Format(T("m"), m);
        }

        public static int Detect()
        {
            switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
            {
                case "ru": return 1;
                case "fr": return 2;
                case "nl": return 3;
                case "de": return 4;
                case "uk": return 5;
                case "es": return 6;
            }
            return 0;
        }
    }

    // ---------- Settings ----------
    class Settings
    {
        // Theme: 0 = always light, 1 = auto (schedule), 2 = always dark
        // Mode:  0 = by sun (location), 1 = by time
        public int Lang = -1, Theme = 1, Mode = 0;
        public double Lat = 50.8503, Lon = 4.3517;
        public string City = "";
        public int LocSrc;   // 0 = coordinates by hand, 1 = Windows location, 2 = IP address (older versions), 3 = city search
        public int WinX = int.MinValue, WinY = int.MinValue;
        public bool Mica = true;
        public bool LocSet;
        public int LH = 7, LM = 0, DH = 19, DM = 0;
        public bool SysTheme = true, AppTheme = true, Startup, Tray = true;

        static string FilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "AutoDarkModeX");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "settings.ini");
            // settings of the earlier "Auto Dark Mode" are taken over once
            string old = Path.Combine(Path.Combine(appData, "AutoDarkMode"), "settings.ini");
            try { if (!File.Exists(file) && File.Exists(old)) File.Copy(old, file); } catch { }
            return file;
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                foreach (string line in File.ReadAllLines(FilePath(), Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i < 1) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    int n; double d;
                    switch (k)
                    {
                        case "lang": if (int.TryParse(v, out n) && n >= 0 && n < L.Codes.Length) s.Lang = n; break;
                        case "theme": if (int.TryParse(v, out n) && n >= 0 && n <= 2) s.Theme = n; break;
                        case "mode": if (int.TryParse(v, out n)) s.Mode = n == 1 ? 1 : 0; break;
                        case "lat": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.Lat = d; break;
                        case "lon": if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) s.Lon = d; break;
                        case "city": s.City = Geo.Clean(v); break;
                        case "locsrc": if (int.TryParse(v, out n) && n >= 0 && n <= 3) s.LocSrc = n; break;
                        case "locset": s.LocSet = v == "1"; break;
                        case "lh": if (int.TryParse(v, out n)) s.LH = Math.Max(0, Math.Min(23, n)); break;
                        case "lm": if (int.TryParse(v, out n)) s.LM = Math.Max(0, Math.Min(59, n)); break;
                        case "dh": if (int.TryParse(v, out n)) s.DH = Math.Max(0, Math.Min(23, n)); break;
                        case "dm": if (int.TryParse(v, out n)) s.DM = Math.Max(0, Math.Min(59, n)); break;
                        case "sys": s.SysTheme = v == "1"; break;
                        case "apps": s.AppTheme = v == "1"; break;
                        case "tray": s.Tray = v == "1"; break;
                        case "winx": if (int.TryParse(v, out n)) s.WinX = n; break;
                        case "winy": if (int.TryParse(v, out n)) s.WinY = n; break;
                        case "mica": s.Mica = v != "0"; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.AppendLine("lang=" + Lang);
                sb.AppendLine("theme=" + Theme);
                sb.AppendLine("mode=" + Mode);
                sb.AppendLine("lat=" + Lat.ToString("0.#####", ci));
                sb.AppendLine("lon=" + Lon.ToString("0.#####", ci));
                sb.AppendLine("city=" + Geo.Clean(City));
                sb.AppendLine("locsrc=" + LocSrc);
                sb.AppendLine("locset=" + (LocSet ? 1 : 0));
                sb.AppendLine("lh=" + LH); sb.AppendLine("lm=" + LM);
                sb.AppendLine("dh=" + DH); sb.AppendLine("dm=" + DM);
                sb.AppendLine("sys=" + (SysTheme ? 1 : 0));
                sb.AppendLine("apps=" + (AppTheme ? 1 : 0));
                sb.AppendLine("tray=" + (Tray ? 1 : 0));
                if (WinX != int.MinValue) { sb.AppendLine("winx=" + WinX); sb.AppendLine("winy=" + WinY); }
                sb.AppendLine("mica=" + (Mica ? 1 : 0));
                File.WriteAllText(FilePath(), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    // ---------- Updates: latest release on GitHub ----------
    // Reads https://api.github.com/repos/<Repo>/releases/latest; works only while the repository is public.
    static class Updater
    {
        public const string Repo = "shakhid/AutoDarkModeX";

        public class Release { public Version Ver; public string Tag = "", Page = "", Installer = "", FileName = ""; }

        public static Version Parse(string tag)
        {
            string t = (tag ?? "").Trim().TrimStart('v', 'V');
            var parts = t.Split('.');
            int[] n = new int[4];
            for (int i = 0; i < parts.Length && i < 4; i++) int.TryParse(parts[i], out n[i]);
            return new Version(n[0], n[1], n[2], n[3]);
        }

        public static Version Current { get { return Parse(AppVersion.Text); } }

        // throws on network / API errors
        public static Release Latest()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            using (var wc = new TimedWebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers["User-Agent"] = "AutoDarkModeX/" + AppVersion.Text;
                wc.Headers["Accept"] = "application/vnd.github+json";
                return ParseRelease(wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest"));
            }
        }

        public static Release ParseRelease(string json)
        {
            {
                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                var root = (Dictionary<string, object>)js.DeserializeObject(json);
                var r = new Release();
                object v;
                if (root.TryGetValue("tag_name", out v) && v != null) r.Tag = v.ToString();
                if (root.TryGetValue("html_url", out v) && v != null) r.Page = v.ToString();
                r.Ver = Parse(r.Tag);
                if (root.TryGetValue("assets", out v) && v is IEnumerable)
                    foreach (object o in (IEnumerable)v)
                    {
                        var d = o as Dictionary<string, object>;
                        if (d == null) continue;
                        string name = d.ContainsKey("name") ? Convert.ToString(d["name"]) : "";
                        string url = d.ContainsKey("browser_download_url") ? Convert.ToString(d["browser_download_url"]) : "";
                        if (name.StartsWith("AutoDarkModeX-Setup", StringComparison.OrdinalIgnoreCase) &&
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            url.StartsWith("https://github.com/" + Repo + "/releases/download/", StringComparison.OrdinalIgnoreCase))
                        { r.Installer = url; r.FileName = Path.GetFileName(name); break; }
                    }
                return r;
            }
        }

        // downloads the installer to %TEMP% and returns its path (throws on error)
        public static string Download(Release r)
        {
            string file = Path.Combine(Path.GetTempPath(), r.FileName);
            using (var wc = new TimedWebClient(120000))
            {
                wc.Headers["User-Agent"] = "AutoDarkModeX/" + AppVersion.Text;
                wc.DownloadFile(r.Installer, file);
            }
            return file;
        }
    }

    // ---------- Location ----------
    class TimedWebClient : WebClient
    {
        readonly int timeout;
        public TimedWebClient() : this(8000) { }
        public TimedWebClient(int ms) { timeout = ms; }
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);
            if (r != null) r.Timeout = timeout;
            return r;
        }
    }

    class Place
    {
        public string Name = "", Region = "", Country = "";
        public string Short { get { return Country.Length > 0 ? Name + ", " + Country : Name; } }
        public double Lat, Lon;
        public string Full { get { return Region.Length > 0 ? Name + ", " + Region : Name; } }
    }

    static class Geo
    {
        // City search by name (Open-Meteo geocoding, data: GeoNames). Only the typed text is sent.
        // Throws on network errors; returns an empty list when nothing matches.
        public static List<Place> Search(string text, string lang)
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            string url = "https://geocoding-api.open-meteo.com/v1/search?count=6&format=json&language=" + lang +
                         "&name=" + Uri.EscapeDataString(text.Trim());
            using (var wc = new TimedWebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers["User-Agent"] = "AutoDarkModeX/" + AppVersion.Text;
                return ParseSearch(wc.DownloadString(url));
            }
        }

        public static List<Place> ParseSearch(string json)
        {
            var list = new List<Place>();
            var js = new System.Web.Script.Serialization.JavaScriptSerializer();
            var root = js.DeserializeObject(json) as Dictionary<string, object>;
            object res;
            if (root == null || !root.TryGetValue("results", out res) || !(res is IEnumerable)) return list;
            foreach (object o in (IEnumerable)res)
            {
                var d = o as Dictionary<string, object>;
                if (d == null || !d.ContainsKey("latitude") || !d.ContainsKey("longitude")) continue;
                var p = new Place();
                p.Name = Clean(Str(d, "name"));
                string a1 = Clean(Str(d, "admin1")), co = Clean(Str(d, "country"));
                p.Country = co;
                p.Region = a1.Length > 0 && a1 != p.Name ? (co.Length > 0 ? a1 + ", " + co : a1) : co;
                p.Lat = Convert.ToDouble(d["latitude"], CultureInfo.InvariantCulture);
                p.Lon = Convert.ToDouble(d["longitude"], CultureInfo.InvariantCulture);
                if (p.Name.Length > 0 && Math.Abs(p.Lat) <= 90 && Math.Abs(p.Lon) <= 180) list.Add(p);
            }
            return list;
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v; return d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        // place names come from outside: keep printable characters only, no line breaks
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char ch in s) if (!char.IsControl(ch)) sb.Append(ch);
            string r = sb.ToString().Trim();
            return r.Length > 80 ? r.Substring(0, 80) : r;
        }

        // Windows location service (System.Device / Location API), loaded at run time.
        // Returns false when the service is off, denied, unavailable or gives no fix within ~15 s.
        public static bool Windows(out double lat, out double lon, out string place)
        {
            lat = 0; lon = 0; place = "";
            object watcher = null; Type wt = null;
            try
            {
                Assembly asm = Assembly.Load("System.Device, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                wt = asm.GetType("System.Device.Location.GeoCoordinateWatcher");
                Type accT = asm.GetType("System.Device.Location.GeoPositionAccuracy");
                watcher = Activator.CreateInstance(wt, new object[] { Enum.ToObject(accT, 0) });
                bool started = (bool)wt.GetMethod("TryStart").Invoke(watcher, new object[] { true, TimeSpan.FromSeconds(10) });
                string perm = wt.GetProperty("Permission").GetValue(watcher, null).ToString();
                if (!started || perm == "Denied") return false;
                object coord = null;
                for (int i = 0; i < 10 && coord == null; i++)
                {
                    object pos = wt.GetProperty("Position").GetValue(watcher, null);
                    object c = pos.GetType().GetProperty("Location").GetValue(pos, null);
                    if (!(bool)c.GetType().GetProperty("IsUnknown").GetValue(c, null)) coord = c; else Thread.Sleep(500);
                }
                if (coord == null) return false;
                lat = (double)coord.GetType().GetProperty("Latitude").GetValue(coord, null);
                lon = (double)coord.GetType().GetProperty("Longitude").GetValue(coord, null);
                try
                {
                    Type rt = asm.GetType("System.Device.Location.CivicAddressResolver");
                    object res = Activator.CreateInstance(rt);
                    object addr = rt.GetMethod("ResolveAddress").Invoke(res, new object[] { coord });
                    if (!(bool)addr.GetType().GetProperty("IsUnknown").GetValue(addr, null))
                    {
                        string city = Clean((string)addr.GetType().GetProperty("City").GetValue(addr, null));
                        string country = Clean((string)addr.GetType().GetProperty("CountryRegion").GetValue(addr, null));
                        place = city.Length > 0 && country.Length > 0 ? city + ", " + country : city;
                    }
                }
                catch { }
                return true;
            }
            catch { return false; }
            finally
            {
                try { if (watcher != null) { wt.GetMethod("Stop").Invoke(watcher, null); ((IDisposable)watcher).Dispose(); } } catch { }
            }
        }

    }
}
