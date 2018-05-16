using System;
using System.Collections.Generic;
using System.Text;

namespace ProfileUploader
{
    public static class DateHelper
    {
        public static DateTime FromUnixTime(this long unixTime)
        {
            return epoch.AddMilliseconds(unixTime / 1000);
        }
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTime(this DateTime date)
        {
            return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalMilliseconds * 1000);
        }
    }
}
