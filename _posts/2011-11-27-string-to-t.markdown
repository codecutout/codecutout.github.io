---
layout: post
title:  "String to Object Parsing"
date:   2011-11-27
description: "We have a string and we want to parse it as an object, what to do?"
redirect_from: "/string-to-t"
---

## .NET do it for me

In the .NET world there is a lot of confusion around parsing a string to a typed object such as an int, decimal, datetime etc. Its not that the libaries don't exist, its that there are several of them, and they all seem to stop just short of providing an adequate solution.

* `Int32.Parse(string s)`, `DateTime.Parse(string s)`, etc. These work fine, however you have to know at the time you are writing the code what object you need back, there is no way to parse an object to type of `Type`.
* `Convert.ChangeType(object val, Type t)` This fixes the parsing to a given `Type` issue but it does not work for some of the less common case (such as GUIDs). It is also limited in its ability to add custom conversions.
* `TypeDescriptor.GetConverter(type).ConvertTo(Type t, object val)` This is the closest to what we are after, it is extensiable, handles the common cases and works with  converting to a given `Type`. However it does have some drawbacks: it does not work with enums; failed conversions thow an exception that you need to catch to handle uncovertable cases; and its a bit long to write out. But we can fix all those issues with a couple of handy extension methods.

## A couple of handy extension methods

Here are two extension methods, one for when we know what we want at compile time `Convert<T>`, and one if we have a `Type` that we want ot convert it to `TryConvert(Type t)`.

{% highlight csharp %}
public static class ConverterExtensions
{
	/// <summary>
	/// Attempts to convert the string to the given type
	/// </summary>
	/// <typeparam name="T">The type to convert the string. Must be either a class or a nullable struct</typeparam>
	/// <param name="str">The string to convert</param>
	/// <returns>The converted object, or null if conversion failed</returns>
	public static T Convert<T>(this string str)
	{
		//We will not allow non-nullable types, this is for the safety of the caller, 
		//they need to think about what happens if the value can not be converted
		var genericType = typeof(T);
		if (genericType.IsValueType 
			&& !(genericType.IsGenericType && genericType.GetGenericTypeDefinition() == typeof(Nullable<>)))
			throw new ArgumentException("Generic parameter must be nullable to allow the result to indicate an invalid conversion", "T");

		object result;
		if (str.TryConvert(typeof(T), out result))
			return (T)result;
		return default(T);
	}

	/// <summary>
	/// Attempts to convert the string to the given type 
	/// </summary>
	/// <param name="str">The string to convert</param>
	/// <param name="type">The type to convert the string into</param>
	/// <param name="result">The result of the conversion, null if conversion failed</param>
	/// <returns>true if the conversion was successful, otherwise false</returns>
	public static bool TryConvert(this string str, Type type, out object result)
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
				if(isNullable)
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
{% endhighlight %}

A great deal of time was spent on contemplating how invalid conversion should be handled, there are several ways to do it and I wanted to hit the nice balance in enforcing safe code and being simple to use. The `TryConvert` method follows the TryX pattern by returning whether its successful and having the actual result as an out parameter, however the `Convert<T>` does something different and will return null if failed. The reason the `Convert<T>` method returns null as it allows for cleaner use in code, for example I can easily default the value of a failed conversion by using the ?? operator.

    myPotentiallyInvalidIntString.Convert<int?>() ?? 100;

There is also a runtime check to make sure only using nullable structs are used. `"3".Convert<int>()` will throw an argument exception, using `"3".Convert<int?>()` is the expected input. This enforces the fact that conversions can fail and null is always a possible return value. Nullable types will also protect you from making accidents in your code and force you to consider the case when the string is not convertable.

The restrictions to nullable types don't apply to the `TryConvert` method, the reason we are passing a `Type` object is we have no idea what type we want, so it seems unfair to throw argument exceptions. For this case the TryX method works much better and the invalid conversion handling can be done by using the returned boolean value.

{% highlight csharp %}
object result;
if (!myPotentiallyInvalidIntString.TryConvert(typeof(int), out result))
{
	result = 100;
}
{% endhighlight %}

## Custom converters

.NET has a bunch of type converters built in and cover most of the basics

* int
* float
* double
* decimal
* long
* bool
* guid
* (possibly others)

However there will always be custom classes you want to be able to convert and even some in built classes that you wish you could convert. Luckily there is a way to add our own converters by creating a class that extends TypeConverter, then registering it agaisnt the class with an attribute. Below is a an example of a converter that will convert an XDocument to and from a string.

{% highlight csharp %}
/// <summary>
/// Converter to convert between strings and XDocuments
/// </summary>
public class XDocumentConverter : TypeConverter
{
	public static void Register()
	{
		TypeDescriptor.AddAttributes(typeof(XDocument), new TypeConverterAttribute(typeof(XDocumentConverter)));
	}

	public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
	{
		if (sourceType == typeof(string))
			return true;
		
		return base.CanConvertFrom(context, sourceType);
	}

	public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
	{
		if (value == null)
			return new XDocument();

		if (value is string)
		{
			var stringValue = (string)value;
			return XDocument.Parse(stringValue);
		}

		return base.ConvertFrom(context, culture, value);
	}

	public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
	{
		if (destinationType == typeof(string))
			return true;

		return base.CanConvertTo(context, destinationType);
	}

	public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
	{
		var xDocValue = value as XDocument;
		if (value == null)
			return base.ConvertTo(context, culture, value, destinationType);

		if (destinationType == typeof(string))
		{                return xDocValue.ToString();
		}

		return base.ConvertTo(context, culture, value, destinationType);

		
	}
}
{% endhighlight %}

Although I have been discussing converting from a string, the converter framework is far more general and allows converting to and from lots of different types. The four methods that need overriding are

* `bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)` to determine if a conversion from the given type is possible.
* `object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)` to perform the conversion from the given value and type. Culture information is also available to allow for locale specific transformations.
* `bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)` to determine if a conversion is possible to a given type.
* `object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)` to perform the conversion to the given type.

We still need to register the converter with the framework so that it knows that you custom converter exists. Normally this is done with a `TypeConverterAttribute` on the actual class the converter is for, for the XDocument converter this would need to go on XDocument

{% highlight csharp %}
[TypeConverter(typeof(XDocumentTypeConverter))]
public class XDocument
{
	...
}
{% endhighlight %}

Unfortunatly for classes in the .NET framework, like XDocument, we cant just put an attribute on it, we can however add attributes at run time using.

{% highlight csharp %}
TypeDescriptor.AddAttributes(typeof(XDocument), new TypeConverterAttribute(typeof(XDocumentConverter)));
{% endhighlight %}

Which is exactly what our type converter register method does, but because its at runtime we have to actaully run that piece of code, this means at the start of your application you need to run `XDocumentTypeConverter.Register()` for the converter to work.

## Give it to me in bullets

So in summary of all the above

* .NETs handling of conversions are confusing, but TypeConverters are the best way to go.
* Presented is a useful extension method for simplyfing the complexities and shortcomings out of using TypeConverters.
* Can create custom converters by extending TypeConverter.
* Need to register custom converters, if you own the source code for the class you can add an attribute to the class you are converting to/from, if you dont own the source code you need to add the attribute at runtime using ` TypeDescriptor.AddAttributes(Type t, params Attribute[] attrs)`.
