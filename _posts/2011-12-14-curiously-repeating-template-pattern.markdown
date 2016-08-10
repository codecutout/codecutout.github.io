---
layout: post
title:  "Curiously Repeating Template Pattern in C#"
date:   2011-12-14
description: "Through a bizarre setup of generics we can create fantastical yet sinister set of classes"
redirect_from: "/curiously-repeating-template-pattern"
---

Recently I came across a interesting design pattern, The [Curiously Repeating Template Pattern][1] (CRTP).  It has its origins in C++ but we can achieve something similar C#. The pattern is characterized by a class with the definition

{% highlight csharp %}
public class MyObject<T> where T : MyObject<T>{

}
{% endhighlight %}

On initial glance you think "Oh its just generics with a condition argument". Then you look a bit harder and you release that `T` has to be a reference to itself, but you cant define itself without defining a T. Its at this point your eyes glaze over and your brain starts melting.

Finally after a long while you realize the only way you can create an instance of `MyObject<T>` is to subclass it.

{% highlight csharp %}
public class MyDerivedObject : MyObject<MyDerivedObject>{

}
{% endhighlight %}

And that is the  Curiously Repeating Template Pattern in C#


## Cool trick, but why?

Finding information on how to setup the CRTP is easy, but there is a lack of information on why you would use it (at least in C#). When setting classes up this way there are a few interesting properties

* The CRTP class can never be instantiated
* When we extend the CRTP class the value we give `T` must be itself (or another class that extends the CRTP). This is a weak form of type safety, we cant go creating objects that extend `MyObject<string>` the compiler just wont allow it
* The base class can accept and return the type of the derived class. In our fictitious example above we can have methods of `MyObject` that effectively return a type of `MyDerivedObject` without it ever knowing that the class `MyDerivedObject` exists

One of the uses of this pattern is to implement fluent interfaces (although its not the only way or even the preferred way)


## The curiously repeating SVG builder

Enough with the high level descriptions and made up class names, lets see something real with this pattern, well at least real enough for you to get the idea. We are going to build an SVG writing fluent interface. An example usage of our end product will be something like 

{% highlight csharp %}
static void Main(string[] args)
{
	SVGSquare rect = new SVGSquare()
		.StrokeWidth(2)     //defined on SVGElement, returns an SVGSquare
		.Fill("blue");      //defined on SVGElement, returns an SVGSquare
		.X(30)              //defined on SVGSquare, returns an SVGSquare
		.Y(30)              //defined on SVGSquare, returns an SVGSquare
		.Width(100)         //defined on SVGSquare, returns an SVGSquare
		.Height(100)        //defined on SVGSquare, returns an SVGSquare
		

	var line = new SVGPolyLine()
	   .StrokeWidth(2)      //defined on SVGElement, returns an SVGPolyLine
	   .Stroke("blue")      //defined on SVGElement, returns an SVGPolyLine
	   .Add(200, 200)       //defined on SVGPolyLine, returns an SVGPolyLine
	   .Add(250, 300)       //defined on SVGPolyLine, returns an SVGPolyLine
	   .Add(200, 300);      //defined on SVGPolyLine, returns an SVGPolyLine


	output(@"c:\mysvg.svg", rect, line); //writes our two objects to an SVG file
}
{% endhighlight %}

The important thing to note here is that we are calling methods on SVGElement but they are returning either an SVGSquare or SVGPolyline depending on what our initial object was. That is, we have one method definition with different return types depending on what our actual class is.

To achieve this sorcery of generics we need to define our base class, it will have some of the common methods and properties of all SVG elements, afterall thats what base classes are for

{% highlight csharp %}
public abstract class SVGElement<T> : ISVGElement where T : SVGElement<T>
{
	public static readonly XNamespace SVGNameSpace = @"http://www.w3.org/2000/svg";

	public virtual XElement XElement { get; private set; }


	public SVGElement()
	{
		XElement = new XElement(SVGNameSpace + NodeName);
	}

	public abstract string NodeName {get;}

	public T Attribute(string key, string value)
	{
		var attribute = XElement.Attribute(key);
		if (attribute != null)
		{
			attribute.Value = value;
		}
		else
		{
			XElement.Add(new XAttribute(key, value));
		}
		return (T)this;
	}

	public string Attribute(string key)
	{
		var attribute = XElement.Attribute(key);
		if (attribute != null)
			return attribute.Value;
		return null;
	}

	public T Fill(string color)
	{
		return Attribute("fill", color);
	}

	public T Stroke(string color)
	{
		return Attribute("stroke", color);
	}

	public T StrokeWidth(int width)
	{
		return Attribute("stroke-width", width.ToString());
	}
}
{% endhighlight %}

The interesting part about this is all our methods return a type of `T`. Since this is a fluent interface we need to return itself so we can chain methods and `T` will be whatever our derived class is when `SVGElement` gets extended

You may have also noticed that our class implements the interface

{% highlight csharp %}
public interface ISVGElement
{
	XElement XElement { get; }
}
{% endhighlight %}

this is so we can create a variable of `ISVGElement` to store any subclass of `SVGElement<T>`, normally we would defined the variable type to be that of our base class, but because of this pattern we get stuck in a curiously repeating loop when trying to write the variable type. To fix this we give it an interface so we can pass this object and all possible derived objects around after its been created. This will make more sense when you get to the `output()` method further down.

As you now know the CRTP class is useless without some subclasses, in this case we will have two `SVGSquare` and `SVGPolyLine`

{% highlight csharp %}
public class SVGSquare : SVGElement<SVGSquare>
{
	public SVGSquare X(int x)
	{
		return Attribute("x", x.ToString());
	}

	public SVGSquare Y(int y)
	{
		return Attribute("y", y.ToString());
	}

	public SVGSquare Width(int width)
	{
		return Attribute("width", width.ToString());
	}

	public SVGSquare Height(int height)
	{
		return Attribute("height", height.ToString());
	}

	public override string NodeName
	{
		get { return "rect"; }
	}
}

public class SVGPolyLine : SVGElement<SVGPolyLine>
{

	public SVGPolyLine Add(int x, int y)
	{
		var pointsString = Attribute("points") ?? "";
		pointsString = pointsString + string.Format(" {0},{1}", x, y);
		return Attribute("points", pointsString.Trim());
	}


	public override string NodeName
	{
		get { return "polyline"; }
	}
}
{% endhighlight %}

These add additional functionality and are instantiable. They will also inherit all the functionality of the base class, furthermore when the base class methods are called they will return an `SVGSquare` or an `SVGPolyline` and NOT a `SVGElement`. It is this feature that makes this a useful pattern for fluent interfaces, this sort of behavior is not easily achieved without the CRTP

Finally for completeness here is the output method, there is not too much clever going on here
     
{% highlight csharp %}
 public static void output(string fileName, params ISVGElement[] elements)
{
	XNamespace ns = @"http://www.w3.org/2000/svg";
	var root = new XElement(ns + "svg");

	root.Add(new XAttribute("version", "1.1"));
	root.Add(elements.Select(e => e.XElement).ToArray());
	using (XmlWriter writer = XmlWriter.Create(fileName))
	{
		root.WriteTo(writer);
	}
}
{% endhighlight %}

The only thing to note is that the parameter needs to take instances of the `ISVGElement`. We can not make it take a parameter of `SVGElement<T>` as we can not define `T`

There we have it, an example of the CRPT in making a fluent interface. The main property we are using here is that our base class can return instances of our dervied types without knowing they even exist. If we did not use this pattern as soon as we called a method on our base type it would have to return an instance of `SVGElement` and we would have to cast it back in the calling code.

## That's amazing! lets use this everywhere!

Just because you can now use the CRTP is not necessarily a good reason to use it. Although you now understand the CRTP, the next developer who tries interpret `SVGElement<T> where T : SVGElement<T>` will likely suffer a brain hernia.

Additionally the class hierarchy is not as flexible as it might initially look. If we were to now extend `SVGPolyLine` to make a class `SVGPath` we no longer get the polyline methods returning our subclass, they will return the wrong object, they will return an `SVGPolyLine`. Additionally the only way to make them return the correct subclass is to make `SVGPolyLine` a CRTP class as well and this would mean it cant be instantiated on its own. So in short for all this to work only leaf classes can be instantiated and all non leaf classes have to CRTP classes. This is a big hit to the flexibility of your code and might well cause you troubles when you need to extend what you have.

With great power comes great responsibility. Feel enlightened that you know the trickery of the C# CRTP, but you will need to think long and hard before you ever try and use it in any production code. There are very few situations where it could be useful and even fewer where it actaully is.


  [1]: http://en.wikipedia.org/wiki/Curiously_recurring_template_pattern