---
layout: post
title:  "Minify and Bundling Web Resources"
date:   2012-01-13
description: "The new MVC way of minifying javascript and css and how to bundle them together in a single file"
redirect_from: "/resource-minify-bundling"
---

In the world of web we all know its a good idea to combine our javascript and css together and minify, this reduces the size and number of files the browser needs to fetch and improves the performance of our website.

The difficulty is in setting up your development environment so that this is done automatically. There seems to be loads of different ways to do this, varying from HttpHandlers to MsBuild tasks, with so many different ways to achieve the same (and quite simple task) most developers are stung with analysis paralysis.


## MVC, make the decision for me

To make the decision easier MVC4 will ship with the library to fulfill your bundling and minifying needs. The namespace `System.Web.Optimization` contains the required classes and will be available right there in the framework, no worrying about external dependancies. 

If you are still in MVC3 and want in on the bundling action, the same library is available to you through NuGet, just look for the "ASP.Net Optimization - Bundling" package. It is exactly the same as what will come with MVC4 except that the namespace is "Microsoft.Web.Optimization"

![Optimization library from NuGet][2]


## We now have the tools, how do we use them

To fit the majority of cases we need to add one line to the `global.asax` file. The line will tell your app that we will use the default bundling rules

{% highlight csharp %}
protected void Application_Start()
{
	BundleTable.Bundles.EnableDefaultBundles();
	//....
}
{% endhighlight %}

With this setup you can now browse browse "[any folder in your site]/js" or "[any folder in your site]/css". When you browse these it will find all the css or js files in the folder bundle them together, minify them and serve the result as a single file. 

So to get your web pages using these new mini bundled psuedo files we add the appropriate link and script tags to our pages

{% highlight xml %}
<link href="@Url.Content("~/Content/css")" rel="stylesheet" type="text/css"/>
<script src="@Url.Content("~/Scripts/js")" type="text/javascript"></script>
{% endhighlight %}

Now our site is using fewer more compact files which will hopefully lead to a faster cleaner website


## Keeping it in order

Which order each file appears on the page matters, you dont want to load jQuery after your custom scripts as it wont work, similarly you dont want to load a reset.css after your main site.css as your layout will be reset. How do we tell the bundler our ordering requirements?

Turns out the default bundler is actually quite smart. It knows jQuery (or MooTools or Prototype or a bunch of other frameworks) files comes first. It also knows to use .min.js versions over non min'd versions and it knows to skip .debug.js and -vsdoc files too.

However even with all its smarts there will be times that you know best, and it is these times we have to move away from our single line solution and start explicitly stating the contents of our bundles

{% highlight csharp %}
protected void Application_Start()
{
	var bundle = new Bundle("~/CustomScript", typeof(JsMinify));
	bundle.AddFile("~/MyReallyImportantScriptLocation/MyJSPreSetup.js");
	bundle.AddFile("~/MyReallyImportantScriptLocation/MyJS.js");

	BundleTable.Bundles.Add(bundle);
}
{% endhighlight %}

When creating a bundle there are a few things you need to to do
 
 * Pass it the relative URL that we will browse to look at the bundled script.
 * Pass it the minifier we want to use (usually either JsMinify or CssMinify).
 * Add to the list of files that are included in the bundle.
 * Finally add our new Bundle to the bundle table.

Now we are in more control of what we are bundling and more importantly the order in which we bundle them.


## Are all our minifying problems now solved?

One thing that this framework does not seem to offer much guidance on (and neither do many of the other minifying frameworks) is how to change behavior for debugging. Minified code is great for production but during development we would be shooting ourselves in the foot if we implemented this, no one wants to debug minified javascript.

A quick solution to get our real code for debugging and our bundled code for production might be to do something like this in our views

{% highlight csharp %}
@if(Html.ViewContext.HttpContext.IsDebuggingEnabled){
	<script src="@Url.Content("~/Scripts/MyJS-1.js")" type="text/javascript"></script>
	<script src="@Url.Content("~/Scripts/MyJS-2.js")" type="text/javascript"></script>
	<script src="@Url.Content("~/Scripts/MyJS-3.js")" type="text/javascript"></script>
	<script src="@Url.Content("~/Scripts/MyJS-4.js")" type="text/javascript"></script>
}
else{
	<script src="@Url.Content("~/Scripts/js")" type="text/javascript"></script>
}
{% endhighlight %}

However this going to cause you issues in the future. You are now essentially maintaining the files that are in your bundle at two places, in your `global.asax` for production and in your views for debug. Whats worse is if your bundle configurations arent the same you will get different behavior when you build in production from when you build in debug.

A better solution would be to implement our own minifier, rather than JsMinify or CssMinify, our minifier would not minify at all. We can then use our non-minifer for debugging and our real JsMinify/CssMinify for production. Thankfully someone (thanks Nandip Makwana) has implemented the [non-minifer][1] for us. This will not minify our code, unfortunately it will still have it in one single file. If our solution has lots of javascript files having to trawl through one giant file is still not an ideal debugging solution.

A slightly better solution could be to build an HtmlHelper method that will output our single file if in production, and output the individual files in the bundle if we are in debug. And that is exactly what the HtmlExtension methods do below

{% highlight csharp %}
public static class HtmlExtensions
{
	public static readonly string CssOutputTemplate =  @"<link href=""{0}"" rel=""stylesheet"" type=""text/css""/>";
	public static readonly string JsOutputTemplate = @"<script src=""{0}"" type=""text/javascript""></script>";

	/// <summary>
	/// Outputs the CSS bundle indicated by the bundleURL. If in debug each file will be added seperatly unminified
	/// otherwise the link to the bundle URL will be created
	/// </summary>
	/// <param name="html"></param>
	/// <param name="bundleUrl"></param>
	/// <returns></returns>
	public static MvcHtmlString CssBundle(this HtmlHelper html, string bundleUrl)
	{
		return ResourceBundle(html, bundleUrl, html.ViewContext.HttpContext.IsDebuggingEnabled, CssOutputTemplate);
	}

	/// <summary>
	/// Outputs the JS bundle indicated by the bundleURL. If in debug each file will be added seperatly unminified
	/// otherwise the link to the bundle URL will be created
	/// </summary>
	/// <param name="html"></param>
	/// <param name="bundleUrl"></param>
	/// <returns></returns>
	public static MvcHtmlString JsBundle(this HtmlHelper html, string bundleUrl)
	{
		return ResourceBundle(html, bundleUrl, html.ViewContext.HttpContext.IsDebuggingEnabled, JsOutputTemplate);
	}

	public static MvcHtmlString ResourceBundle(HtmlHelper html, string bundleVirtualPath, bool expandFiles, string outputTemplate)
	{
		var bundle = BundleTable.Bundles.GetBundleFor(bundleVirtualPath);
		var httpContext = html.ViewContext.HttpContext;
		var urlHelper = new UrlHelper(html.ViewContext.RequestContext);
	   

		if (bundle == null)
		{
			throw new ArgumentException(String.Format("Unable to find bundle for virtual path '{0}'. ensure that this bundle is registered", bundleVirtualPath), "bundleVirtualPath");
		}

		//if we are going to compress it just return the appropriate output linking to the given virtual path, the bundling
		//process defined in the global.asax will take care of the rest
		if (!expandFiles)
		{
			//Will just return a string to our bundled URL
			//By using the ResolveBundleUrl it will also but a hash
			//in the generated URL to play nice with browser caching
			return new MvcHtmlString(string.Format(outputTemplate, BundleTable.Bundles.ResolveBundleUrl(bundleVirtualPath)));
		}


		var basePath = httpContext.Server.MapPath("~/");
		var bundleContext = new BundleContext(httpContext, BundleTable.Bundles, bundleVirtualPath);
		var output = new StringBuilder();
		foreach (FileInfo file in bundle.EnumerateFiles(bundleContext))
		{
			//we need to convert our aboslute path into a nice webpath, for this to work we do
			//need our files to be sitting somewhere within the web directory. 
			if(!file.FullName.StartsWith(basePath))
				throw new Exception(String.Format("File {0} is not in a path exposed by the website", file.FullName));

			var relativePath = urlHelper.Content("~/" + file.FullName.Substring(basePath.Length));
			output.AppendLine(String.Format(outputTemplate, relativePath));
		   

		}

		return new MvcHtmlString(output.ToString());
	}
}
{% endhighlight %}

To use in your views you just need to add the following lines. When in debug it will add individual `<script>` or `<link>` tags for each file in the bundle. In production it will add just one that points tag that points to our single minified bundle.

{% highlight csharp %}
@Html.CssBundle("~/Content/css")
@Html.JsBundle("~/CustomScript")
{% endhighlight %}


## A Nutshell
`System.Web.Optimization` (or `Microsoft.Web.Optimization` for MVC3) gives us the power of bundling and minifying javascript and css. If its default behavior falls short the flexibility is there to be more explicit in what goes in your bundles and the order it goes in there.

A bit of work is still required to get your environment setup to be debug friendly. `@if(debug)` your views provides a simple, but limited solution. Telling the bundler not to minify for debug also provides another way to keep your code intact but still causes a one file output. Finally to stop minifying and bundling an HtmlHelper method can provide the shortcut to writing the `<script>` and `<link>` tags for you.



  [1]: http://www.dotnetexpertguide.com/2011/12/custom-transformtype-bundling.html
  [2]: /get/postimages/bundling-nuget.jpg