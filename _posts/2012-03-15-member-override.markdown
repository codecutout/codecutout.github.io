---
layout: post
title:  "Member Override"
date:   2012-03-15
description: "A little play with expressions to temporarily override a value over a specific scope"
redirect_from: "/member-override"
---

Have you ever found yourself writing

{% highlight csharp %}
var oldValue = SomeField;
SomeField = 3;
try
{
	//...
}
finally
{
	SomeField = oldValue;
}
{% endhighlight %}

Despite taking up a lot of space it doesn't do anything terribly exciting. It just temporarily overrides the value of a variable, then after we finished doing some operations we set it back to what it was before we messed with it. 

So why would anyone want to do this? Primarily i have used such code to fudge frameworks that depend heavily on static variables, i can override static variables to temporarily modify the behavior of my static-heavy framework and trick it into doing what i want. I'm sure there are  other less devious (and more thread safe) uses of such code but it is the coding hacks that readily come to mind.

Every time I find myself writing such code I always get turned off by the amount of extra space it adds, that's 9 additional lines in my method that just obfuscate my codes intention. I would extract the temporary assignment into its own method, but coming up with intuitive parameters that would work was too difficult

... well that was the way i thought until i learned the power of C# Expressions

## Enter the Expression 

Expressions are the hidden gem in C#. They allow you to pass what looks like an innocent lambda expression into a method, but inside your method you get a full expression tree that represents everything your lambda function is doing. This includes being able to read what members your lambda is reading, what methods are being called, any arithmatic operators used, the whole lot completely visible in your method.

This is very different to our normal understanding of how code is executed, normally if I pass something to a method only the final result it passed. But expressions pass the code that is used to generate the result. We can then examine the code passed and makes decisions on the code, not the values.

How does one write one of these mighty methods, well suprisingly it is very easy

{% highlight csharp %}
public void MyExpressionMethod(Expression<Func<int, int>> exp) { ... }
{% endhighlight %}

then you might think the difficulty is in calling this amazing method, how is one going to pass an Expression<Func<int,int>>? Well it turns out thats even easier than the method definition

{% highlight csharp %}
MyExpressionMethod(i=>i+1);
{% endhighlight %}

Yes it looks just like you passed a regular delegate to the method! you have probably been calling Expression methods all this time but never realised, they have been hiding under the guise of regular lambda expressions.

## How can I harness such power

Back to my value overriding, would it not be great to have something like

{% highlight csharp %}
using (Override.Set(() => this.SomeProperty, "ValueHack"))
{
	...
}
{% endhighlight %}

(it would be greater to have the assignment in the expression as well, but expressions don't like assignments in them) 

Using unexpressioned methods such a method would be impossible, all we pass it is a delegate to read the value and the value we want it to be, our method would have no idea how to set the value. But with expressions we know what member is being used so we can use that to both get and set the value.

So how does such a method work

{% highlight csharp %}
	/// <summary>
	/// Overrides the value of a field, when the returned value is disposed the field
	/// is set back to the original value
	/// </summary>
	/// <typeparam name="T">type of the field to set</typeparam>
	/// <param name="field">An expression that points ot the field to override</param>
	/// <param name="value">the new value to set the field to</param>
	/// <returns></returns>
	public static IDisposable Set<T>(this Expression<Func<T>> field, T value)
	{
		var exp = field.Body as MemberExpression;
		if (exp == null)
			throw new ArgumentException("Input expression function must be a member");
		
		var member = exp.Member;

		//Compile the source of the expression. This will convert it  into real
		//runnable code so we can determine what object our member is on
		object baseObject = null;
		if (exp.Expression != null)
		{
			LambdaExpression lambda = Expression.Lambda(exp.Expression);
			Delegate fn = lambda.Compile();
			baseObject = fn.DynamicInvoke();
		}

		//get the current value of the member
		var oldValue = (T)member.GetValue(baseObject);

		//set the new value of hte member
		member.SetValue(baseObject, value);

		//return a disposable that will set it back to the old value
		return new SimpleDisposer(() => member.SetValue(baseObject, oldValue));

	}

	/// <summary>
	/// Heleper method to get the value of a either property or a field
	/// </summary>
	/// <param name="info"></param>
	/// <param name="obj"></param>
	/// <returns></returns>
	private static object GetValue(this MemberInfo info, object obj)
	{
		FieldInfo fi = info as FieldInfo;
		if (fi != null) return fi.GetValue(obj);

		PropertyInfo pi = info as PropertyInfo;
		if (pi != null) return pi.GetValue(obj);

		throw new NotSupportedException("Member {0} is not supported");
	}

	/// <summary>
	/// Helper method to set a value on either a property or a field
	/// </summary>
	/// <param name="info"></param>
	/// <param name="obj"></param>
	/// <param name="value"></param>
	private static void SetValue(this MemberInfo info, object obj, object value)
	{
		FieldInfo fi = info as FieldInfo;
		if (fi != null)
		{
			fi.SetValue(obj, value);
			return;
		}

		PropertyInfo pi = info as PropertyInfo;
		if (pi != null) 
		{
			pi.SetValue(obj, value);
			return;
		}

		throw new NotSupportedException("Member {0} is not supported");
	}

	/// <summary>
	/// The non-expression solution
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="oldValue"></param>
	/// <param name="newValue"></param>
	/// <param name="setValue"></param>
	/// <returns></returns>
	public static IDisposable Set<T>(T oldValue, T newValue, Action<T> setValue)
	{
		setValue(newValue);
		return new SimpleDisposer(()=>setValue(oldValue));
	}

	/// <summary>
	/// Class that allows an anonymous like class to represnt an IDisposable
	/// </summary>
	private class SimpleDisposer : IDisposable
	{
		private readonly Action _dispose;
		public SimpleDisposer(Action dispose){
			_dispose = dispose;
		}

		public void Dispose()
		{
			_dispose();
		}
	}

}
{% endhighlight %}

Most of the code is just allowing us to get/set values on both properties and fields, the actual code we are interested in is the `Set` method. Firstly we take an expression and convert it into the member it is accessing and another expression of the `baseObject` the method is being called on. To convert our `baseObject` from an expression to a real object we have to compile it. Once we have the member and the `baseObject` its just a matter of using reflection to set/get the values

## Was all the expressions really necassary

In the above code I have also included the non expression version, it takes one extra parameter and doesn't look as cool when called, but its code is significantly shorter and executes a whole lot faster. So should we be using expressions to do this?

Ill let you make the call, either way it is an interesting piece of code and its a a good gateway into the wonderful world of expressions.
