---
layout: post
title:  "Dynamic Proxy"
date:   2011-11-20
description: "Creation of a dynamic proxy and a config file reading example"
redirect_from: "/dynamic-proxy"
---

## The files

* Just the [DynamicProxy.cs][1]
* Full [solution file][2] with tests

## Introducing the Dynamic Proxy

The idea of a dynamic proxy is that you automatically implement an interface where every call to your interface methods routes through a single defined method. Your one defined method then can handle every call in the exact same way. 

It is dynamic because this interface it is generated for you and its a proxy because you are really calling your generated class in lieu of your actual object. Therefore we have a dynamic proxy.

 
The two main reasons for dynamic proxies
 
* All the properties/methods on your interface are executed in an almost identical way, your dynamic proxy can implement all those methods exactly the same way and you only have to write (and maintain) one method
* You want to wrap an existing object up and do a common thing either before and after every call. Most commonly this can be used for logging or security, but could also be used for other things such as cache results transparently or load balance to external services.
 
There are [nice proxy frameworks][3] that specialize in doing this sort of thing, but what I am providing here is a simple single C# file that can be added to projects wihtout having to throw yet another dll to the mix.

## Lets see some usage

{% highlight csharp %}
public interface ISpeaker
{
	string Say(string words);
}

ISpeaker person = DynamicProxy.New<ISpeaker>((info, param) =>
{
	return "Proxy Says '" + param[0] + "'";
});

 Assert.AreEqual("Proxy Says 'Hello'", person.Say("Hello"));
{% endhighlight %}

First we need an interface for our dynamic proxy to implement (we can also use abstract classes as well). We then create a our proxy using `DynamicProxy.New` and pass in a `Func<MethodInfo, object[], object>` the parameters of our `Func` are the `MethodInfo` of the method that was invoked and the array of parameters passed into the method, the return value of the `Func` is what our method should return. With this we can implement any method on an interface

## What about properties

Turns out after compilation properties are just a pair of `get_MyPropertyName` and `set_MyPropertyName` methods. Because these are just methods the dynamic proxy handles these the same way as all the other methods.

Although the proxy handles properties it is still awkward to determine in the proxy whether we are handling a property or a method, to simplify this a few extension methods have been added to MethodInfo

* `PropertyInfo MethodInfo.GetProperty()` to get a PropertyInfo for the method (null if not a property)
* `bool MethodInfo.IsSetProperty()` to determine if we are looking at a setter
* `bool MethodInfo.IsGetProperty()` to determine if we are looking at a getter


So now we can work with properties with ease

{% highlight csharp %}
public interface IPerson : ISpeaker
{
	string Name { get; set; }
}

string setValue = null;
IPerson person = DynamicProxy.New<IPerson>((info, param) =>
{
	//get the property info
	PropertyInfo property = info.GetProperty();
			
	//are we implementing the property
	if (property == null)
		return;

	//are we the getter
	if (info.IsGetProperty())
		return "Proxy-Man";

	//are we the setter
	if (info.IsSetProperty())
		setValue = param[0] as string;

	return null;
});
person.Name = "Alex";
Assert.AreEqual("Proxy-Man", person.Name);
Assert.AreEqual("Alex", setValue);
{% endhighlight %}

## The Magic Explained

Conceptually what we are doing here is very simple, using [.NETs Emit APIs][4] we are creating a class at runtime and implementing all the necassary methods to call the designated `Func` delegate.

The only issue is that Emit API does not let you write your methods using a nice language like C#, you have to write them directly in Microsoft Intermediate Language (MSIL). MSIL is what the .NET runtime runs off, and is what your C# compiler spits out after compiling your code. Because MSIL is a lower level language even the simplest code quickly becomes labourous and error prone to write - but thats why this is all done for you.


I wont repeat the code here, once you get your head around MSIL it is straight forward but quite verbose. If you are keen I encourage you to have a look through the code, I find digging into MSIL gives you a deeper understanding of what your C# is really doing (and you can read dissassmbled DLLs to impress your easily impressed friends).


## Enough with the Factitious examples

The example usages above arent exactly real-life examples. Presented here is a usage of the proxy to read values from an app.config file in a type safe way. 

Let us say we have an appSettings section like

{% highlight xml %}
<appSettings>
	<add key="ApplicationName" value="Data-Accessor"/>
	<add key="Version" value="3"/>
	<add key="CacheSettings.UserCacheSize" value="1000"/>
	<add key="CacheSettings.ProductCacheSize" value="1000"/>
</appSettings>
{% endhighlight %}

It would be nice if we could read our config file in a type safe way, so lets define an interface that represents our config file and gives our code something to intellisense from

{% highlight csharp %}
public interface ISettings
{
	string ApplicationName { get; }

	int Version { get; }

	ICacheSettings CacheSettings { get; }
}

public interface ICacheSettings
{
	int ProductCacheSize {get;}
	int UserCacheSize {get;}
}
{% endhighlight %}

You will notice we have a hierarchy of settings, this allows some grouping of settings (CacheSizes in this case) so when accessing the settings you get a logical property chain, e.g. `var cacheSize = mySettings.CacheSettings.ProductCacheSize`

Now we need to write something to implement our interfaces, we could go through each property individually and implement it to read the appSettings and make the appropriate type conversion, but as the number of settings grows we will end up implementing the same type of logic over and over again. I think by now you would've guessed that we are going to use the dynamic proxy

{% highlight csharp %}
static void Main(string[] args)
{
	ISettings settings = DynamicProxy.New<ISettings>((i,p)=>ReadConfigSetting(i,p));


	Console.WriteLine(string.Format("ApplicationName = {0}", settings.ApplicationName));
	Console.WriteLine(string.Format("Version = {0}", settings.Version));
	Console.WriteLine(string.Format("CacheSettings.UserCacheSize = {0}", settings.CacheSettings.UserCacheSize)); 
	Console.ReadKey();
}

public static object ReadConfigSetting(MethodInfo info, object[] param, IEnumerable<string> path = null)
{
	if (!info.IsGetProperty())
		throw new Exception("Unable to execute " + info.Name);

	PropertyInfo property = info.GetProperty();

	//append the name of our property onto the path
	var newPath = new List<string>(path ?? new string[0]);
	newPath.Add(property.Name);
	path = newPath;

	if(property.PropertyType.IsInterface){
	   
		return DynamicProxy.New(property.PropertyType, (i, p) => ReadConfigSetting(i, p, newPath));
	}

	string appSettingValue = ConfigurationManager.AppSettings[string.Join(".", path)];
	if (appSettingValue == null)
		return null;

	var descriptor = TypeDescriptor.GetConverter(property.PropertyType);
	return descriptor.ConvertFromInvariantString(appSettingValue);
}
{% endhighlight %}


You may have noticed this implementation we will only work for get properties, this is making an implicit assumption our interfaces wont have any methods or setter properties on them. If someone did add a method or setter property our implementation of such a method is simple; we throw an error.

The implementation of getter properties is 

* if the return type is not an interface we will look in the appSettings file for a key that is equal to the current path and convert the result to be the same type as the interface expects.
* if the return type is another interface, we return a new proxy which calls the same method with a slightly longer path. This allows us to handle arbitrary depth hierarchies through recursion 

Every time a new property that returns an interface is called our path gets longer. It is only when we read a property that is not an interface will we combine our path and look in the appSettings to retrieve the actual value.

Adding new settings would just require us to add another value in our interface (and in the appSettings too) the implementation of retrieving the setting is done for us already. Also if sometime in the future you decide settings should be stored in a database then there is only one method you need to modify to change every setting in your application.


  [1]: https://gist.github.com/codecutout/e863e22428e36d4d15c6e93ea8fb29bb
  [2]: /assets/posts/code/dynamicproxy/DynamicProxy.zip
  [3]: http://castleproject.org/dynamicproxy/index.html
  [4]: http://msdn.microsoft.com/en-us/library/xd5fw18y.aspx