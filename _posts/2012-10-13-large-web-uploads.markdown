---
layout: post
title:  "Large Web Uploads"
date:   2012-10-13
description: "Allow your ASP.NET app to accept large file uploads without being blocked by IIS or wasting memory"
redirect_from: "/large-web-uploads"
---

##Something to play with
[RequestStreaming.zip][1]

##Uploading large files
Let say you want a section of your site where users need to upload large files such as movies, files, applications. Naively you might slap in a file input tag create a controller (because we are all MVC junkies) that accepts an HttpPostedFile and you are done ... until someone actually uploads a big file. You will envitably run into two issues

1. ASP.NET by default says NO to big files
2. ASP.NET by default reads the entire file contents into memory before letting you look at it

## Html of our example
Just to make sure we are on the same page, here is the page. A simple html form that will post the file to the StreamToDisk controller. 

{% highlight html %}
<form method="post" enctype="multipart/form-data" action="/StreamToDisk">
				
	<label for="LocationInput">File to upload</label>
	<input id="FileInput" type="file" name="File" />
		
	<input type="submit"/>
	
</form>
{% endhighlight %}

Our goal is simple, when the form is submitted the server will take the uploaded file and write it directly out to the file system.


## Make ASP.NET accept large files

Unfortunatly ASP.NET will outright reject requests larger than 30MB. It is for your own safety and to prevent your server from being maliciously attacked.

Luckily, like most things, this can be turned off through config. If you are wanting to accept files larger than the limit you need to create a few web.config additions. 

We are not entirely security ignorant, make sure you put a location tag around these restrictions, we only want to allow big files on our StreamToDisk controller, not the entire site. There is also a difference in settings for IIS6 and IIS7, so we might as well put both in so we dont run into deployment issues later

{% highlight xml %}
<location path="StreamToDisk">
	<!-- IIS6 -->
	<system.web>
		<httpRuntime maxRequestLength="2147483647" />
	</system.web>

	<!-- IIS7 -->
	<system.webServer>
		<security>
			<requestFiltering>
				<requestLimits maxAllowedContentLength="2147483647" />
			</requestFiltering>
		</security>
	</system.webServer>
</location>
{% endhighlight %}
	
Now ASP.NET will leave us alone for any files smaller than 2GB on the StreamToDisk controller. 2GB is the max, you should set this appropriately depending on what you expect to be uploaded.

## Being memory efficient

ASP.NET may let the upload happen, but all the standard methods to get a file (`HttpPostedFile`, `Request.Files`) seem to have a bad habit of buffering the entire file into memory, in fact anything that even glances at the incoming request will pull the entire file in memory. If we are handling large file uploads, the last thing we want to do is pull in 100's of MB of file bytes into memory.

To make things worse most of the MVC niceties also drag the entire request into memory, as soon as it tries to evaluate action filters, or model bind values we will lose a chunk of our memory. Because of this our `StreamToDisk` controller is going close to the metal - no extending `Controller` for us, we are going to use `IController`. using `IController` is not strictly necessary, it will work with `Controller` if no action filters or parameters are used, but using `IController` will prevent someone in the future from innocently adding something like an `[Authroize]` on the action and destroying all our hard work to stop buffering.

So here is the full controller to stream a request directly to a file

{% highlight csharp %}
public class StreamToDiskController : IController
{
	public static string FileDirectory = @"C:\Temp";

	public void Execute(System.Web.Routing.RequestContext requestContext)
	{
		//unfortunatly the MVC request object doesnt have GetBufferlessInputStream
		//so we have to bypass and go directly to the static httpcontext
		using (var requestStream = HttpContext.Current.Request.GetBufferlessInputStream())
		{

			var mpp = new MultipartPartParser(requestStream);

			while (mpp != null && !mpp.IsEndPart && !string.IsNullOrWhiteSpace(mpp.Filename))
			{
				//we can access the filename from the part

				Directory.CreateDirectory(FileDirectory);
				string fileName = Path.Combine(FileDirectory, mpp.Filename);

				//we can access the part contents as a stream and copy it
				//to a file stream, or any other writable stream
				using (var fileStream = new FileStream(fileName, FileMode.CreateNew, FileAccess.Write))
				{
					mpp.CopyTo(fileStream);
				}

				//move the stream foward until we get to the next part
				mpp = mpp.ReadUntilNextPart();
			}

			requestContext.HttpContext.Response.Redirect("/");
		}
	}
}
{% endhighlight %}

The main part of this is done by the `MultipartPartParser` (see [my previous post][2]) This is the class that is doing the hard work of reading our raw http request input stream and turning it into bytes we can write to disk. The important thing is that it is doing it without reading the entire file into memory, effectively streaming our request straight out to the file system.

Finally we need to produce a response. The easiest (and also the safest for large file uploads) is to redirect out to another page.

## Summing up
We are now handling large file uploads without being rejected by IIS and without sacrificing all of our memory. 

However we are still stuck with the limitation of 2GB file upload limits and the unreliable nature of http uploads. The only way to bypass these restrictions is to drop http uploads for some other more file friendly protocol such as ftp. But as that is more pain than its worth, i'm sticking to http file uploads for now.


  [1]: /assets/posts/code/largewebuploads/RequestStreaming.zip
  [2]: /blog/multipart-part-parser/