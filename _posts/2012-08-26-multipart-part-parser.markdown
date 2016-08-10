---
layout: post
title:  "The Multipart Part Parser"
date:   2012-08-26
description: "Parse multipart/form-data messages as they are streamed in"
redirect_from: "/multipart-part-parser"
---

## The file

[MultipartPartParser.cs][1]

## Multipart Part Parser

I'll admit the there are very limited uses for this class, however it does fill a very important hole in another piece I was working on (may post later), and was a little bit disappointed I could not find a pre-implemented version of this, so I had to write my own.

So what is it? When your HTML forms upload files they are posted with a mime type of multipart/form-data and streamed up to your webserver. ASP.NET will then read the entire request into memory, parse it and expose convenient properties to access the contents of the request. This is great for small files, but when we are expecting large files, buffering the entire request into memory is a waste of memory, especially since in most cases you are going to stream the uploaded file straight out to disk.

The MultipartPartParser allows us to read the a multipart/form-data request as it is being streamed in, it filters out all the format specific garbage and exposes a raw stream of the uploaded files, we can then send this stream straight to disk (or to anything else that accepts a stream of bytes). This bypasses the need of reading our entire file in memory

The MultipartPartParser assumes you have a multipart/form-data request stream, You can convince ASP.NET to give you this, but that is a topic of another post.

## What are we parsing?

If your HTML trys to post a file you probably have come accross the where you must include the form attribute `enctype="multipart/form-data`. This forces the browser to send a multipart/form-data message.

The HTML form

{% highlight html %}
<form method="post" enctype="multipart/form-data">
	<input type="text" name="Description"/>
	<input type="file" name="MyFile" />
	<input type="submit"/>
</form>
{% endhighlight %}

Will send a request to server that looks similar to

{% highlight css %}
------WebKitFormBoundaryummUFQApPgTA1WSX
Content-Disposition: form-data; name="Description"

This is the description of my file
------WebKitFormBoundaryummUFQApPgTA1WSX
Content-Disposition: form-data; name="MyFile"; filename="1.gif"
Content-Type: image/gif

[Lots of bytes that display as random characters]
------WebKitFormBoundaryummUFQApPgTA1WSX--
{% endhighlight %}

For every input element in our form we will have a part, each part contains

 - Starts with a boundary that looks like `------WebKitFormBoundaryummUFQApPgTA1WSX`
 - Some meta-information such as the content type, the input field name and the original filename.  
 - The actual content of the field, for text fields this is just the text the user wrote in the field, for files it is the actual bytes of the file
 - Once we have sent all the input fields on the form, the request ends with the boundary name appended with two -- at the end

What we want our parser to do is process the boundaries and meta-information and expose a stream to access the raw content, but we need it do this as it reads the request in, not after we have the whole thing in memory

## The parser

The way the parser is structured is that each instance of `MultipartPartParser` represents a single part in the request, or a single form element. `MultipartPartParser` extends stream, which allows access to the actual parts content. Once we finish reading the part, then the stream is finished, and the parser will give us a reference to the next part in the stream.

The constructor will

 - Read in a chunk of bytes from underlying stream
 - Find what the content boundary is and extract the metadata
 - Move the underlying stream to read the contents

The read method will

 - Read from the underlying stream
 - Search for the content boundary in the bytes we read
 - If no content boundary return the read bytes
 - If we found a content boundary, return the bytes up to (but not including) the content boundary and create the next part

So in code that looks like

<script src="https://gist.github.com/codecutout/4accf28d40682231473eff289379aeed.js"></script>

The general interaction with the parser would be something like this

{% highlight csharp %}
var inputStream = requsetBody; //assumes you have a request from somewhere

var mpp = new MultipartPartParser(inputStream);

while (mpp != null && !mpp.IsEndPart && !string.IsNullOrWhiteSpace(mpp.Filename))
{
	//we can access the filename from the part
	string fileName = Path.Combine(@"C:\MyFiles", mpp.Filename);

	//we can access the part contents as a stream and copy it
	//to a file stream, or any other writable stream
	using (var fileStream = new FileStream(fileName, FileMode.CreateNew, FileAccess.Write))
	{
		mpp.CopyTo(fileStream);
	}

	//move the stream foward until we get to the next part
	mpp = mpp.ReadUntilNextPart();
}
{% endhighlight %}


Remember we are reading the request stream as we read the part, so we can not go backwards or jump to parts within the request, we can only read forward. This does make the component a little bit awkward to use if you have lots of parts with not much content, but the component is mainly intended to be used if you have a small form that will include large file uploads, so we have only a few parts but one of them is very large.

## Why so complicated?

You might be surprised about how large the parser is, from the initial description multipart/form-data it sounded reasonably simple to parse, but the amount of code says otherwise. The complication in the code stems from the fact that our input stream is one way read only and we always need to read more than we return. 

We always read more so we can ensure we don't mistake the first part of a content boundary as some actual content. For example if we need to read 1000 bytes, but the 999th byte is the first byte of the content boundary, there is no way we can confirm if we reached the end of the part unless we read 1040 bytes and check for the boundary. This puts us in a position were we read 1040 bytes from the underlying stream, and return only 1000, we need to remember what those 40 bytes are so we can return them on the next read. The large part of the code is moving things around in an internal buffer so we can keep track of extra bytes we needed to read but caller doesn't want yet.

The same issue applies in the constructor, we read a preset number of bytes to extract header information, but at this point in time we have no idea how large the header is, so we will always read too many and need to remember the ones we read but didn't use so we can return them later.

We also end up with a similar problem when we read the end of a part, it is possible to read 1040 bytes but the part finished on the 50th byte. We need to send the other 900 bytes to the next part so it can read those before reading from the underlying stream.

## How production ready is this parser

The code was tested for my specific use case and it worked exactly as I wanted it to, however the tests were far from handling every case so make sure you test it where you use it. The code is fairly well commented and i'm sure you could tweak it to suit your needs if some of the behavior is not as expected.


  [1]: https://gist.github.com/codecutout/4accf28d40682231473eff289379aeed