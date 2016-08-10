---
layout: post
title:  "XML deserialization from App.Config"
date:   2012-12-01
description: "The simpler way to create custom app.config sections"
redirect_from: "/xml-deserialization-from-app-config"
---

##Custom entries into App.Config are hard, if only there was an easer way

If you have ever tried to make a custom app.config entry it has probably come out of it with a few traumatic memories - it is hard, much harder than it should be.

However recently i came across an amazing code project article [http://www.codeproject.com/Articles/6730/Custom-Objects-From-the-App-Config-file][1]. Full credit of this whole post goes to Mark Focas who wrote the original article and code. It shows a far simpler way to write custom config sections.

So if the article exists why am I reproducing it here?  My first time reading the article I didn't realize the significance of the code, nor did i realize how little code is required to make it work - just a dozen lines. When it dawned on me what i'd stumbled across I felt the need to shout it from the rooftops, failing to find a ladder I came here. 

I have also made minor tweaks to the original code which I think makes it slightly easier to configure, but the general premise is the same.


## Code first, then example

{% highlight csharp %}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using System.Xml.Serialization;
using System.Xml;
using System.Configuration;

namespace MyProject
{
	/// <summary>
	/// This class contains only one method, Create().
	/// This is designed to handle custom sections in an Applications Configuration file.
	/// By Implementing <see cref="IConfigurationSectionHandler"/>, we can implement the
	/// Create method, which will provide the XmlNode from the configuration file. This is
	/// Deserialized into an object and passed back to the Caller.
	/// </summary>
	/// <example>
	/// Here is a configuration file entry in the <c>configSections</c> sectikon of the <c>App.Config</c>
	/// file.
	///<code>	///
	///&lt;section name="ServerConfig" type="ConfigSectionHandler.ConfigSectionHandler, ConfigSectionHandler" /&gt;
	///</code>
	///This tells the CLR that there is a section further in, with a node name of <c>ServerConfig</c>. When this section
	///is to be parsed, an object of type <c>ConfigSectionHandler.ConfigSectionHandler</c> which resides in the 
	///assembly <c>ConfigSectionHandler</c> will be instantiated. The CLR automatically calls a method in that object
	///called <c>Create</c>
	///</example>
	public abstract class XmlDeserializeConfigSectionHandler : IConfigurationSectionHandler
	{
		public XmlDeserializeConfigSectionHandler()
			: base()
		{
		}

		#region IConfigurationSectionHandler Members

		/// <summary>
		/// A method which is called by the CLR when parsing the App.Config file. If custom sections
		/// are found, then an entry in the configuration file will tell the runtime to call this method,
		/// passing in the XmlNode required.
		/// </summary>
		/// <param name="parent">The configuration settings in a corresponding parent configuration section. Passed in via the CLR</param>
		/// <param name="configContext">An <see cref="HttpConfigurationContext"/> when Create is called from the ASP.NET configuration system. Otherwise, 
		/// this parameter is reserved and is a null reference (Nothing in Visual Basic). Passed in via the CLR</param>
		/// <param name="section">The <see cref="XmlNode"/> that contains the configuration information from the configuration file. 
		/// Provides direct access to the XML contents of the configuration section. 	Passed in via the CLR.</param>
		/// <returns>The Deserialized object as an object</returns>
		/// <exception cref="System.Configuration.ConfigurationException">The Configuration file is not well formed,
		/// or the Custom section is not configured correctly, or the type of configuration handler was not specified correctly
		/// or the type of object was not specified correctly.
		/// or the copn</exception>
		public object Create(object parent, object configContext, System.Xml.XmlNode section)
		{
			Type t = this.GetType();
			XmlSerializer ser = new XmlSerializer(t);
			XmlNodeReader xNodeReader = new XmlNodeReader(section);
			return ser.Deserialize(xNodeReader);
		}
		#endregion
	}
}
{% endhighlight %}


This piece of code allows you to write custom sections in your app.config (or web.config) file that will pulled into memory using the standard XML deserializing in .NET, rather than using the normal comprehensible app.config classes that .net tried to make us use.

Given a class like this (notice we are extending our `XmlDeserializeConfigSectionHandler` class)

{% highlight csharp %}
public class CacheServerConfiguration : XmlDeserializeConfigSectionHandler
{
	public string Address { get; set; }
	public int TimeToLiveInMinutes { get; set; }
	public string Username { get; set; }
	public string Password { get; set; }
}
{% endhighlight %}

I can put this in the config file like this

{% highlight xml %}
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <configSections>
	<!-- Need to use full reference with [namespace].[typename], [assembly] -->
	<!-- The section name will need to be teh same as the classname for deserialization to work -->
  <section name="CacheServerConfiguration" type="MyProject.CacheServerConfiguration, MyProject"/>
  </configSections>

  <CacheServerConfiguration>
	<Address>http://cache.fabrikam.com</Address>
	<Username>CacheUser</Username>
	<Password>C@cheU3er</Password>
	<TimeToLiveInMinutes>60</TimeToLiveInMinutes>
  </CacheServerConfiguration>
  
</configuration>
{% endhighlight %}

and i can get it out of the config like this

{% highlight csharp %}
var config = (CacheServerConfiguration)ConfigurationManager.GetSection("CacheServerConfiguration");
{% endhighlight %}

Because it uses the XML deserilizer you just need to sprinkle your code with `[XmlAttribute]`, `[XmlRoot]` attributes or implement `IXmlSerializable` to make it look like the XML you want. No more messing around with `ConfigurationElement`s and `[ConfigurationProperty]`

##I'm a changed man

All my custom configs from here on in will make use of this gem, and i encourage everyone to go to Mark Focas' post [http://www.codeproject.com/Articles/6730/Custom-Objects-From-the-App-Config-file][1] and give him 5 stars for thinking this up.


  [1]: http://www.codeproject.com/Articles/6730/Custom-Objects-From-the-App-Config-file