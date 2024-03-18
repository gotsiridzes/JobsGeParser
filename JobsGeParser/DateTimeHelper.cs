namespace JobsGeParser;

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

	private static int GetMonth(string value) =>
		value switch
		{
			"იანვარი" => 1,
			"თებერვალი" => 2,
			"მარტი" => 3,
			"აპრილი" => 4,
			"მაისი" => 5,
			"ივნისი" => 6,
			"ივლისი" => 7,
			"აგვისტო" => 8,
			"სექტემბერი" => 9,
			"ოქტომბერი" => 10,
			"ნოემბერი" => 11,
			"დეკემბერი" => 12,
			_ => throw new Exception("invalid date")
		};
}