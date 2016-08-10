---
layout: post
title:  "Holiday Calculator"
date:   2012-12-11
description: "A simple configurable system to determine holiday days"
redirect_from: "/holiday-calculator"
---

[source on github][2]

## Calculating holidays

Handling which dates are public holidays is a fairly common requirement for systems, whether it be to calculate delivery times, highlighting calendar entries or determining how many work days are left before Christmas. Unfortunately for something so common there are surprisingly few public solutions, it seems most people must just resort to having a list of fixed dates that are updated yearly.

Of the few solutions I could find they were either difficult to extend, hidden in a bloated library or were over engineered. Not finding anything I liked, I made my own.

## Hit me

Like most code I post up here I prefer it to be a single file rather than an assembly, so here it is

{% highlight csharp %}
[XmlRoot("HolidaySet")]
public class HolidaySet : List<Holiday>, IConfigurationSectionHandler
{

	/// <summary>
	/// Determines if the given date is a holiday
	/// </summary>
	/// <param name="date"></param>
	/// <returns></returns>
	public bool IsHoliday(DateTime date)
	{
		//are there any holidays which next date is today
		return this.Any(holiday => holiday.NextDate(date.AddDays(-1)) == date);
	}

	/// <summary>
	/// Get all the holidays between two dates
	/// </summary>
	/// <param name="startDate">exclusive start date for range</param>
	/// <param name="endDate">inclusive end date for range</param>
	/// <returns></returns>
	public IEnumerable<DateTime> GetHolidays(DateTime startDate, DateTime endDate)
	{
		return this.SelectMany(h => h.NextDates(startDate).TakeWhile(d => d < endDate)).OrderBy(d => d);
	}

	public object Create(object parent, object configContext, System.Xml.XmlNode section)
	{
		XmlSerializer ser = new XmlSerializer(this.GetType());
		XmlNodeReader xNodeReader = new XmlNodeReader(section);
		return ser.Deserialize(xNodeReader);
	}
}

/// <summary>
/// Base class for calculating complex dates
/// </summary>
public abstract class DateCalculator : IXmlSerializable
{
	/// <summary>
	/// List of DateCalculator types to assist deserialization
	/// </summary>
	protected static Dictionary<string, Type> NameToDateCalculatorType = typeof(DateCalculator).Assembly.GetTypes()
		.Where(t => typeof(DateCalculator).IsAssignableFrom(t))
		.ToDictionary(t => t.Name, t => t);

	/// <summary>
	/// Gets the properties that will be used for attributes on serialization
	/// </summary>
	/// <returns></returns>
	protected virtual IEnumerable<PropertyInfo> GetPropertiesForXmlAttributes()
	{
		return this.GetType().GetProperties().Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof (string) || p.PropertyType.IsEnum);
	}

	/// <summary>
	/// Gets the property that will be used for the content node
	/// </summary>
	/// <returns></returns>
	protected virtual PropertyInfo GetPropertyForXmlContent()
	{
		//first property that with a type of DateCalculator is our inner property
		return this.GetType().GetProperties()
			.FirstOrDefault(p => typeof (DateCalculator).IsAssignableFrom(p.PropertyType));
		
	}

	/// <summary>
	/// Returns the next occurrence of the date after the given startDate
	/// </summary>
	/// <param name="startDate"></param>
	/// <returns></returns>
	public abstract DateTime? NextDate(DateTime startDate);

	/// <summary>
	/// Enumerates through all the next dates.
	/// WARNING: this enumerable may be never ending do not try and enumerate
	/// over entire set!
	/// </summary>
	/// <param name="startDate">non-include date to start looking from</param>
	/// <returns></returns>
	public IEnumerable<DateTime> NextDates(DateTime startDate)
	{
		DateTime? newStartDate = this.NextDate(startDate);
		while (newStartDate.HasValue)
		{
			yield return newStartDate.Value;
			newStartDate = this.NextDate(newStartDate.Value);
		}
	}

	public System.Xml.Schema.XmlSchema GetSchema()
	{
		return null;
	}

	public void ReadXml(System.Xml.XmlReader reader)
	{
		//populate our properties from the attribute
		foreach (var property in GetPropertiesForXmlAttributes())
		{
			string valueString = reader.GetAttribute(property.Name);
			object value;
			if(TryConvert(valueString, property.PropertyType, out value))
				property.SetValue(this, value, null);
		}

		//do some special handling to determine empty nodes and read our start element
		bool isEmptyElement = reader.IsEmptyElement;
		reader.ReadStartElement();
		if (isEmptyElement)
			return;

		reader.MoveToContent();

		//if we have an inner property we need to handle that
		var contentProperty = GetPropertyForXmlContent();
		if (contentProperty != null)
		{

			Type innerElementType;
			if (!NameToDateCalculatorType.TryGetValue(reader.Name, out innerElementType))
				throw new ConfigurationErrorsException(string.Format("Unknown element: '{0}'", reader.Name));
			var innerElement = (DateCalculator) Activator.CreateInstance(innerElementType);
			
			//let the element read its own XML
			innerElement.ReadXml(reader);
			
			contentProperty.SetValue(this, innerElement, null);

			
		}
		
		reader.ReadEndElement();
		reader.MoveToContent();

	}

	public void WriteXml(System.Xml.XmlWriter writer)
	{
		var attributeProperties = this.GetType().GetProperties().Where(p=>p.PropertyType.IsPrimitive || p.PropertyType == typeof(string));
		foreach (var property in attributeProperties)
		{
			var value = property.GetValue(this, null);
			if(value == null)
				continue;

			writer.WriteAttributeString(property.Name, null, value.ToString());
		}

		var contentProperties = this.GetType().GetProperties().Where(p => typeof(DateCalculator).IsAssignableFrom(p.PropertyType));
		foreach (var contentProperty in contentProperties)
		{
			var value = contentProperty.GetValue(this, null) as DateCalculator;
			if (value == null)
				continue;

			writer.WriteStartElement(value.GetType().Name);
			value.WriteXml(writer);
			writer.WriteEndElement();
		}

	}

	/// <summary>
	/// Attempts to convert the string to the given type 
	/// </summary>
	/// <param name="str">The string to convert</param>
	/// <param name="type">The type to convert the string into</param>
	/// <param name="result">The result of the conversion, null if conversion failed</param>
	/// <returns>true if the conversion was successful, otherwise false</returns>
	protected static bool TryConvert(string str, Type type, out object result)
	{

		result = null;
		try
		{
			TypeConverter converter = TypeDescriptor.GetConverter(type);
			if (converter.CanConvertFrom(typeof(string)))
			{
				result = converter.ConvertFromInvariantString(str);
				return true;
			}

			//break out of our nullable type so we can handle enum types easier
			bool isNullable = false;
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				isNullable = true;
				type = Nullable.GetUnderlyingType(type);
			}

			//enums are not supported by default, so we will do our own parsing logic
			if (type.IsEnum)
			{
				result = Enum.Parse(type, str, true);
				if (isNullable)
					result = Activator.CreateInstance(typeof(Nullable<>).MakeGenericType(type), result);
				return true;
			}

			return false;
		}
		catch
		{
			//Unfortunatly catching exception is the only way to know if it failed
			return false;
		}
	}
}

/// <summary>
/// Container for a holiday, has name and inner element to perform actual calculation
/// </summary>
public class Holiday : DateCalculator
{
	public string Name { get; set; }
	public DateCalculator Inner { get; set; }

	public override DateTime? NextDate(DateTime startDate)
	{
		return Inner.NextDate(startDate);
	}
}

/// <summary>
/// Represents a date that occurs the same date every year
/// </summary>
public class FixedDate : DateCalculator
{
	public int Day { get; set; }
	public int Month { get; set; }
	public int? Year { get; set; }

	public override DateTime? NextDate(DateTime startDate)
	{
		//if we have a year then we really are a fixed date
		if(Year.HasValue)
		{
			var nextDate = new DateTime(Year.Value, Month, Day);
			if (nextDate <= startDate)
				return null;
			return nextDate;
		}

		int year = startDate.Year;
		if(startDate.Month > Month || (startDate.Month==Month && startDate.Day >= Day))
			year += 1;

		//make sure 29th of Feb dates are are used in leap years
		while (Day == 29 && Month == 2 && !DateTime.IsLeapYear(year))
			year += 1;

		return new DateTime(year, Month, Day);
	}
}

/// <summary>
/// Finds dates that are so many days before (or after) the inner date
/// </summary>
public class DaysAfter : DateCalculator
{
	public int Days { get; set; }
	public DateCalculator Inner { get; set; }

	public override DateTime? NextDate(DateTime startDate)
	{
		//make suer we step our inner item back, so we can catch days that are in the future
		//but but the inner occurrence day is in the past
		foreach (var innerDate in Inner.NextDates(startDate.AddDays(-Days)))
		{
			var nextDate = innerDate.AddDays(Days);
			if (nextDate > startDate)
				return nextDate;
		}
		return null;
	}
}

/// <summary>
/// Finds the date that falls on the given DayOfWeek that is closest to the inner date
/// </summary>
public class ClosestDayOfWeek : DateCalculator
{
	public DayOfWeek DayOfWeek { get; set; }
	public DateCalculator Inner { get; set; }

	public override DateTime? NextDate(DateTime startDate)
	{
		foreach (var innerDate in Inner.NextDates(startDate.AddDays(-7)))
		{
			var innerDayOfWeek = innerDate.DayOfWeek;

			int dayOfWeekInt = (int) DayOfWeek;
			int innerDayOfWeekInt = (int) innerDayOfWeek;
			if (dayOfWeekInt < innerDayOfWeekInt)
				dayOfWeekInt += 7;

			int distFoward = dayOfWeekInt - innerDayOfWeekInt;
			int distBackward = 7 - distFoward;

			DateTime nextDate;
			if (distFoward < distBackward)
				nextDate = innerDate.AddDays(distFoward);
			else
				nextDate = innerDate.AddDays(-distBackward);

			if (nextDate > startDate)
				return nextDate;
		}
		return null;
	}
}

/// <summary>
/// Gets date that falls on the first weekday on or after the inner date
/// </summary>
public class WeekdayOnOrAfter : DateCalculator
{
	public DateCalculator Inner { get; set; }

	public override DateTime? NextDate(DateTime startDate)
	{
		//Need to stpe back if today is a saturday or sunday, because the inner day may
		//be excluded if our startDate is today, but if today is a weekend the actual date
		//will be hte monday and we would not have returned it.
		int stepBack = 0;
		if(startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday)
			stepBack += 1;
		if(startDate.DayOfWeek == DayOfWeek.Sunday)
			stepBack += 2;


		foreach (var innerDate in Inner.NextDates(startDate.AddDays(-stepBack)))
		{
			var nextDate = innerDate;
			while(nextDate.DayOfWeek == DayOfWeek.Saturday || nextDate.DayOfWeek == DayOfWeek.Sunday)
			{
				nextDate = nextDate.AddDays(1);
			}
			return nextDate;
		}
		return null;
	}
}

/// <summary>
/// Calculates the date that falls on a day occurance within a month
/// for example 1st Monday in June
/// </summary>
public class XthDayOfWeekInMonth : DateCalculator
{

	public DayOfWeek DayOfWeek { get; set; }
	public int DayOccurance { get; set; }
	public int Month { get; set; }

	public override DateTime? NextDate(DateTime startDate)
	{
		var nextDate = new DateTime(startDate.Year, Month, 1);
		nextDate = nextDate.AddDays((DayOccurance - 1) * 7);

		while(nextDate.DayOfWeek != DayOfWeek)
		{
			nextDate = nextDate.AddDays(1);
		}

		if (nextDate > startDate)
			return nextDate;
		return NextDate(new DateTime(startDate.Year + 1, 1, 1));
	}
}

/// <summary>
/// Calculate Easter Sunday
/// </summary>
public class EasterSunday : DateCalculator
{
	public override DateTime? NextDate(DateTime startDate)
	{
		DateTime workDate =  new DateTime(startDate.Year, startDate.Month, 1);
		int year = workDate.Year;
		if (workDate.Month > 4)
			year = year + 1;


		int a = year % 19;
		int b = year / 100;
		int c = year % 100;
		int d = b / 4;
		int e = b % 4;
		int f = (b + 8) / 25;
		int g = (b - f + 1) / 3;
		int h = (19 * a + b - d - g + 15) % 30;
		int i = c / 4;
		int k = c % 4;
		int l = (32 + 2 * e + 2 * i - h - k) % 7;
		int m = (a + 11 * h + 22 * l) / 451;
		int easterMonth = (h + l - 7 * m + 114) / 31;
		int p = (h + l - 7 * m + 114) % 31;
		int easterDay = p + 1;
		DateTime nextDate = new DateTime(year, easterMonth, easterDay);
		if (nextDate > startDate)
			return new DateTime(year, easterMonth, easterDay);
		return NextDate(new DateTime(year + 1, 1, 1));

	}

}
{% endhighlight %}

## How to use

It may not be obvious but dealing with holidays does require configuration - there is no universal set of holiday dates. So you will need to add the following to your app.config (or web.config). Dates here are for Auckland, New Zealand you will likely need modify dates for your use

{% highlight xml %}
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
	<configSections>
		<section name="HolidaySet" type="MyNameSpace.HolidaySet, MyAssembly"/>
	</configSections>

	<HolidaySet>
		<Holiday Name="New Years Day">
			<WeekdayOnOrAfter>
				<FixedDate Day="1" Month="1" />
			</WeekdayOnOrAfter>
		</Holiday>
		
		<Holiday Name="Day after New Years Day">
			<WeekdayOnOrAfter>
				<DaysAfter Days="1">
					<WeekdayOnOrAfter>
						<FixedDate Day="1" Month="1" />
					</WeekdayOnOrAfter>
				</DaysAfter>
			</WeekdayOnOrAfter>
		</Holiday>
		
		<Holiday Name="Auckland Anniversary">
			<ClosestDayOfWeek DayOfWeek="Monday">
				<FixedDate Day="29" Month="1" />
			</ClosestDayOfWeek>
		</Holiday>

		<Holiday Name="Waitangi Day">
			<FixedDate Day="6" Month="2" />
		</Holiday>
		
		<Holiday Name="Good Friday">
			<DaysAfter Days="-2">
				<EasterSunday />
			</DaysAfter>
		</Holiday>
		
		<Holiday Name="Easter Monday">
			<DaysAfter Days="1">
				<EasterSunday />
			</DaysAfter>
		</Holiday>

		<Holiday Name="ANZAC Anniversary">
			<FixedDate Day="25" Month="4" />
		</Holiday>

		<Holiday Name="Queens Birthday">
			<XthDayOfWeekInMonth DayOfWeek="Monday" DayOccurance="1" Month="6" />
		</Holiday>

		<Holiday Name="Labour Day">
			<XthDayOfWeekInMonth DayOfWeek="Monday" DayOccurance="4" Month="10" />
		</Holiday>

		<Holiday Name="Christmas Day">
			<WeekdayOnOrAfter>
				<FixedDate Day="25" Month="12" />
			</WeekdayOnOrAfter>
		</Holiday>
		
		<Holiday Name="Boxing Day">
			<WeekdayOnOrAfter>
				<DaysAfter Days="1">
					<WeekdayOnOrAfter>
						<FixedDate Day="25" Month="12" />
					</WeekdayOnOrAfter>
				</DaysAfter>
			</WeekdayOnOrAfter>
		</Holiday>
		
	</HolidaySet>
	
</configuration>
{% endhighlight %}

Now in code you can get the HolidaySet out of your config like this

{% highlight csharp %}
var holidaySet = (HolidaySet)ConfigurationManager.GetSection("HolidaySet");

IEnumerable<DateTime> nextYearsHolidays = holidaySet.GetHolidays(DateTime.Now, DateTime.Now.AddYears(1));
bool isItAHolidayToday = holidaySet.IsHoliday(DateTime.Now);
{% endhighlight %}

## Dive deeper

It may look like a lot of code but there really isn't much to it. The top level classes are `HoldiaySet` and `Holiday` but all the real work is done in the classes that extend `DateCalculator`, these are

 * `FixedDate` - to get a date like e.g. 19th of February
 * `WeekdayOnOrAfter` - to force a holiday to fall on a weekday, if it would normally fall on a weekend
 * `DaysAfter` - to allow a holiday to occur some days after (or before) another date
 * `XthDayOfWeekInMonth` - to get dates like the second Wednesday in July
 * `ClosestDayOfWeek` - to allow you to get the closest Monday to a given date
 * `EasterSunday` - because it is hard to calculate

Some of these take inner elements allowing you to nest and build up more complex date calculations, for example in the above config Good Friday is two days before Easter Sunday making it

{% highlight xml %}
<Holiday Name="Good Friday">
	<DaysAfter Days="-2">
		<EasterSunday />
	</DaysAfter>
</Holiday>
{% endhighlight %}
	
Hopefully the XML format is reasonably intuitive. The most complicated nested calculation I've done is Boxing Day (assuming Christmas and Boxing Day both roll forward to the next weekday), but even that becomes obvious once you identify the inner elements as Christmas

{% highlight xml %}
<Holiday Name="Boxing Day">
	<WeekdayOnOrAfter>
		<DaysAfter Days="1">
			<WeekdayOnOrAfter>
				<FixedDate Day="25" Month="12" />
			</WeekdayOnOrAfter>
		</DaysAfter>
	</WeekdayOnOrAfter>
</Holiday>
{% endhighlight %}

## What about townsville's bi-annual jamboree

If you can't make your holiday from the provided elements, it is very easy to write your own. All the calculations extend `DateCalculator` and have to implement one method

{% highlight csharp %}
public abstract DateTime? NextDate(DateTime startDate);
{% endhighlight %}
		
This method will find next occurance of your holiday after the given startDate. If your holiday is for a limited time only just return null when its over. 

If your holiday calcuation requires parameters add some properties to your class, if your holiday is relative to some other date add a property of type `DateCalculator` to your class. The custom XML Serializer will sort it out for you,  allowing you to add your newly created holiday to the config file.


## Intellisense has spoilt me

[Schema.xsd][3]

No one likes to remember XML tags, so here is the XSD file. In Visual Studio go add this schema to your project somewhere and open your app.config (or web.config) file and in the properties window there is a special section for adding a schema

![visual studio xml properties][4]

Open it up, add the schema and now you can intellisense your holidays away

The schema wont include any custom `DateCalculators` you add but it is not too difficult to add another entry in the schema


##  Hometown pride

In case my example config hasn't given it away the code was originally written to handle New Zealand holidays and my tests say it does it correctly. However it should be able to cover most other religious, regional and personal holdiays, and if your requirements are more difficult just extend it. 


  [2]: https://github.com/codecutout/HolidayCalculator
  [3]: https://github.com/codecutout/HolidayCalculator/blob/master/HolidayCalculator/Schema.xsd
  [4]: /assets/posts/img/visual-studio-xml-properties.png