using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioSync
{
    internal static class DOS
    {
        public static int GenerateDate(DateTime dateTime)
        {
            int year = dateTime.Year - 1980;
            int month = dateTime.Month;
            int day = dateTime.Day;

            return ((year << 9) | (month << 5) | day);
        }

        public static int GenerateTime(DateTime dateTime)
        {
            int hour = dateTime.Hour;
            int minute = dateTime.Minute;
            int second = dateTime.Second / 2; // MS-DOS stores seconds divided by 2

            return ((hour << 11) | (minute << 5) | second);
        }

        public static DateTime ParseDateTime(int dosDate, int dosTime)
        {
            // Extract date components
            int year = 1980 + (dosDate >> 9);
            int month = (dosDate >> 5) & 0xF;
            int day = dosDate & 0x1F;

            // Extract time components
            int hour = dosTime >> 11;
            int minute = (dosTime >> 5) & 0x3F;
            int second = (dosTime & 0x1F) * 2;

            return new DateTime(year, month, day, hour, minute, second);
        }
    }
}
