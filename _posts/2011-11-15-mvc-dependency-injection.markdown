---
layout: post
title:  "MVC Controller Dependency Injection"
date:   2011-11-15
description: "Injecting parameters into controller constructors"
redirect_from: "/mvc-dependency-injection"
---

## Why dependancy inject controllers?

You may have heard or read a statement similiar to

<Blockquote>Using inversion of control (IoC) makes your code more testable</Blockquote>

Just to make things clear I want to address two points

* I will not call this inversion of control, it is a very poor name that describes nothing, and propagates confusion. Dependancy injection describes our intent much better, we are automatically inserting parameters and properties into our object so that it can use them
* We are not doing this to make our code more testable (although it is a nice side affect). We are doing this to make our code cleaner, modular and flexible, which is far more important than testability on its own

MVC Controllers are where dependency injection really shines. Usually the code you write never explicitly creates controllers so there is no opportunity to pass things into the constructor. The controllers are left to find references to the things they need by themselves. This almost always degrades into static singleton classes to access everything. Statics always seem like a great solution at the time, but very quickly turn into a mess where you are no longer able to take full advantage of inheritance and have no visibility of where and when your objects are being accessed.

Dependency injection allows us to setup the framework to pass the controllers references to the things it needs through the constructor. With the objects a controller needs being passed to it directly, your controller no longer needs to pull references from static singletons. To set the framework up to do this does require a little bit of effort though (a little bit more than it should) but to make things easier all you need is detailed below. 

## Get your favorite Dependency Injection Framework

First we need to Nuget ourselves a<del>n IoC Container</del> dependancy injection framework. For this I will use [StructureMap][1], however you can use any framework you want, they are all the same and seem to differ by method names more than functionality.

![nuget results][2]


## Create the Controller Factory

Now we need to create a controller factory, this will be responsible for actually instantiating our controllers, for this we can extend the `DefaultControllerFactory` and override the method we want.

{% highlight csharp %}
public class StructureMapControllerFactory : DefaultControllerFactory
{
	/// <summary>
	/// Structure Map Container used to generate controllers
	/// </summary>
	public Container Container { get; private set; }

	/// <summary>
	/// Creates a controller factory with the specified structure map container
	/// </summary>
	/// <param name="container"></param>
	public StructureMapControllerFactory(Container container) : base()
	{
		Container = container;
	}

	/// <summary>
	/// Creates a controller factory with a new structure map container wtih the given configuration
	/// </summary>
	/// <param name="configuration"></param>
	public StructureMapControllerFactory(Action<ConfigurationExpression> configuration)
		: this(new Container(configuration))
	{
	}


	protected override IController GetControllerInstance(RequestContext requestContext,  Type controllerType)
	{
		if (requestContext == null || controllerType == null)
			return null;

		return (IController)Container.GetInstance(controllerType);
		
	}
}
{% endhighlight %}

The main thing you need to focus on here is the GetControllerInstance method is overriden so it creates controllers through our dependancy injector, this will resolve constructor parameters for us


## Wire up the controller factory

Finally we need to make MVC aware of the controller factory and tell our dependancy injector how we want to resolve those parameters, we do this in the `Global.asax` file.

{% highlight csharp %}
protected void Application_Start()
{
	AreaRegistration.RegisterAllAreas();
	RegisterGlobalFilters(GlobalFilters.Filters);
	RegisterRoutes(RouteTable.Routes);

	ControllerBuilder.Current.SetControllerFactory(new StructureMapControllerFactory(c =>
	{
		//Add your own resolutions here
		//In this case if our Controller constructor takes an IFileSystem
		//we will pass a new LocalFileSystem
		//If our Controller constructor takes an IEmailEngine we will pass
		//our instance we created here that points to the right server and port
		c.For<IFileSystem>().Use<LocalFileSystem>();
		c.For<IEmailEngine>().Use(new RazorEmailEngine("smtp.codecutout.com", 25));
	}));
}
{% endhighlight %}

We are just calling `ControllerBuilder.Current.SetControllerFactory()` with our custom controller factory. It is a bit ironic that to avoid static singletons in our controllers we have to use an MVC static singleton to set the controller factory, but its all for the greater good.

Your code will need to determine how you resolve your own classes to be injected, it will usually end up being a mapping between a parameter type and the object you want to be passed, but most frameworks allow all sorts of other mappings if you are trying to something particularly fancy.


## Give the controller a constructor

Now we can just create a constructor in the controller and the parameters will be pushed through for us. No longer do we have to resort to statics to get what we want.

{% highlight csharp %}
public class HomeController : Controller
{
	public IEmailEngine EmailEngine{ get; private set; }

	public HomeController(IEmailEngine emailEngine)
	{
		EmailEngine = emailEngine;
	}

	public ActionResult Index()
	{
		//Can now send email here without ever needing to find out smtp servers and ports
		emailEngine.Send("I don't need to worry about servers and ports");
	}

}
{% endhighlight %}

  [1]: http://structuremap.net/structuremap/-
  [2]: /assets/postimages/structure-map-nuget.jpg