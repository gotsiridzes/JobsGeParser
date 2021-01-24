using System;
using System.Collections.Generic;
using System.Text;

namespace JobsGeParser
{
    public static class DateTimeHelper
    {
        public static DateTime GetDate(this string value)
        {
            int year = DateTime.Now.Year;
            string[] split = value.Split(' ');
            int day = int.Parse(split[0]);
            int month = GetMonth(split[1]);

            return new DateTime(year, month, day);
        }

        private static int GetMonth(string value)
        {
            switch (value)
            {
                case "იანვარი":
                    return 1;
                case "თებერვალი":
                    return 2;
                case "მარტი":
                    return 3;
                case "აპრილი":
                    return 4;
                case "მაისი":
                    return 5;
                case "ივნისი":
                    return 6;
                case "ივლისი":
                    return 7;
                case "აგვისტო":
                    return 8;
                case "სექტემბერი":
                    return 9;
                case "ოქტომბერი":
                    return 10;
                case "ნოემბერი":
                    return 11;
                case "დეკემბერი":
                    return 12;
                default:
                    throw new Exception("invalid date");
            }
        }
    }
}
