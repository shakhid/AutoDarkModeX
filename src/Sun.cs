using System;

namespace AutoDarkMode
{
    /// <summary>Sunrise / sunset calculation (NOAA / Almanac algorithm).</summary>
    public static class Sun
    {
        const double D2R = Math.PI / 180.0;

        static double Norm(double v, double m)
        {
            v %= m;
            if (v < 0) v += m;
            return v;
        }

        // 0 = ok, 1 = sun never sets (polar day), 2 = sun never rises (polar night)
        static int Event(DateTime date, double lat, double lon, bool rise, out DateTime result)
        {
            result = DateTime.MinValue;
            if (lat > 89.5) lat = 89.5;
            if (lat < -89.5) lat = -89.5;
            double zenith = 90.833;
            int n = date.DayOfYear;
            double lngHour = lon / 15.0;
            double t = n + (((rise ? 6.0 : 18.0) - lngHour) / 24.0);
            double M = (0.9856 * t) - 3.289;
            double L = M + (1.916 * Math.Sin(M * D2R)) + (0.020 * Math.Sin(2 * M * D2R)) + 282.634;
            L = Norm(L, 360);
            double RA = Math.Atan(0.91764 * Math.Tan(L * D2R)) / D2R;
            RA = Norm(RA, 360);
            double Lq = Math.Floor(L / 90) * 90;
            double RAq = Math.Floor(RA / 90) * 90;
            RA = (RA + (Lq - RAq)) / 15.0;
            double sinDec = 0.39782 * Math.Sin(L * D2R);
            double cosDec = Math.Cos(Math.Asin(sinDec));
            double cosH = (Math.Cos(zenith * D2R) - (sinDec * Math.Sin(lat * D2R))) / (cosDec * Math.Cos(lat * D2R));
            if (cosH > 1) return 2;
            if (cosH < -1) return 1;
            double H = rise ? 360 - Math.Acos(cosH) / D2R : Math.Acos(cosH) / D2R;
            H /= 15.0;
            double T = H + RA - (0.06571 * t) - 6.622;
            double UT = Norm(T - lngHour, 24);
            DateTime utc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc).AddHours(UT);
            DateTime local = utc.ToLocalTime();
            if (local.Date > date.Date) local = local.AddDays(-1);
            else if (local.Date < date.Date) local = local.AddDays(1);
            result = local;
            return 0;
        }

        public static int Times(DateTime date, double lat, double lon, out DateTime sunrise, out DateTime sunset)
        {
            int a = Event(date, lat, lon, true, out sunrise);
            int b = Event(date, lat, lon, false, out sunset);
            if (a == 0 && b == 0) return 0;
            if (a == 1 || b == 1) return 1;
            return 2;
        }
    }
}
