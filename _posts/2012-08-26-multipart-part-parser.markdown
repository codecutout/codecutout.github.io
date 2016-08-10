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
/// <summary>
/// Parses a multipart/form-data stream without buffering the entire request into memory
/// </summary>
public class MultipartPartParser : Stream
{
	/// <summary>
	/// Stream the multipart message is being read from
	/// </summary>
	public Stream MultipartStream { get; private set; }

	/// <summary>
	/// Encoding of the multipart stream header data
	/// </summary>
	public Encoding Encoding { get; private set; }

	/// <summary>
	/// The header element of the part
	/// </summary>
	public string Header { get; private set; }

	/// <summary>
	/// The content disposition of the part
	/// </summary>
	public string ContentDisposition { get; private set; }

	/// <summary>
	/// The content type of the part
	/// </summary>
	public string ContentType { get; private set; }

	/// <summary>
	/// The name of the form field that submitted this part
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// The filename if the submitted part was a file, otherwise null
	/// </summary>
	public string Filename { get; private set; }

	/// <summary>
	/// Determines if this is a full part or just a stub to indicate the
	/// end of the stream
	/// </summary>
	protected bool IsEndPart { get; private set; }

	/// <summary>
	/// The next part in the multipart message
	/// </summary>
	protected MultipartPartParser NextPart { get; private set; }

	/// <summary>
	/// Buffer to store data extracted from the multipart stream but not yet returned
	/// </summary>
	protected MemoryStream LocalBuffer { get; private set; }
	
	/// <summary>
	/// The boundary between parts prependned with the newline element
	/// </summary>
	protected byte[] BoundaryWithNewLinePrepend { get; private set; }

	/// <summary>
	/// The bytes that represnt a new line character
	/// </summary>
	protected byte[] NewLine { get; private set; }

	
	public MultipartPartParser(Stream multipartStream) : this(multipartStream, Encoding.UTF8)
	{
	}

	public MultipartPartParser(Stream multipartStream, Encoding encoding, MemoryStream buffer = null)
	{
	   

		this.MultipartStream = multipartStream;
		this.Encoding = encoding;

		LocalBuffer = new MemoryStream();
		if (buffer != null)
			buffer.CopyTo(LocalBuffer);
		LocalBuffer.Position = 0;

		NewLine = Encoding.GetBytes("\r\n");
		var DoubleNewLine = Encoding.GetBytes("\r\n\r\n");

		//set boundary to empty for now, we dont know what it is until we process our header
		BoundaryWithNewLinePrepend = new byte[0];

		byte[] headerBytes = new byte[1024];
		int headerBytesRead = this.Read(headerBytes, 0, headerBytes.Length);

		int boundaryEnd;
		if (!SearchBytePattern(NewLine, headerBytes, out boundaryEnd))
			throw new Exception("No multipart boundary found. Data must begin with a content boundary");


		//copy our boundary so we can use it
		BoundaryWithNewLinePrepend = new byte[boundaryEnd + NewLine.Length];
		Buffer.BlockCopy(NewLine, 0, BoundaryWithNewLinePrepend, 0, NewLine.Length);
		Buffer.BlockCopy(headerBytes, 0, BoundaryWithNewLinePrepend, NewLine.Length, boundaryEnd);

		//if we have reached the end of our stream at the end of our header then
		//this is the end of multipart part, we label this as the end part and return
		//we know we have reached the end when the number bytes we read was our header
		//plus our search pattern (newline)
		if (headerBytesRead == boundaryEnd + NewLine.Length)
		{
			IsEndPart = true;
			return;
		}

		int headerEnd;
		if (!SearchBytePattern(DoubleNewLine, headerBytes, boundaryEnd, out headerEnd))
		{
			//if we cant find the end of the header it could mean our header is massive
			//and it wasnt in the initial block of bytes we read. 
			throw new Exception("Content header is too large to process");
		}
		headerEnd += DoubleNewLine.Length;

		//get the header and header derived fields
		Header = encoding.GetString(headerBytes, boundaryEnd, headerEnd - boundaryEnd).Trim();
		ContentDisposition = RegexFirstGroup(Header, "^Content-Disposition:(.*)$");
		ContentType = RegexFirstGroup(Header, "^Content-Type:(.*)$");
		Filename = RegexFirstGroup(ContentDisposition, @"filename=""(.*?)""");
		Name = RegexFirstGroup(ContentDisposition, @"name=""(.*?)""");



		int CountOfNonHeaderBytes = headerBytesRead - headerEnd;

		//put back the extra non header content so it can be streamed out again
		ReinsertIntoLocalBuffer(headerBytes, headerEnd, CountOfNonHeaderBytes);
	}

	/// <summary>
	/// Re-Buffers data extracted from the read
	/// </summary>
	/// <param name="source"></param>
	/// <param name="offset"></param>
	/// <param name="count"></param>
	protected void ReinsertIntoLocalBuffer(byte[] source, int offset, int count)
	{
		//we have our header, but we potentially have read more than we need to
		//we have two cases
		//1. we have exhausted our LocalBuffer and some of the data came from the MultipartStream
		//   in this case we will reset our local buffer and write our remaining bytes back into
		//   our local buffer
		//2. We did not exhaust our local buffer, in which case the remaining bytes are still in
		//   the local buffer so we will just rewind it so they are picked up next read
		if (LocalBuffer.Position == LocalBuffer.Length)
		{
			LocalBuffer.Position = 0;
			LocalBuffer.SetLength(0);
			LocalBuffer.Write(source, offset, count);
			LocalBuffer.Position = 0;
		}
		else
		{
			LocalBuffer.Position -= count;
		}
	}

	/// <summary>
	/// Helper method to easily get the first group of a regex expresion
	/// </summary>
	/// <param name="input"></param>
	/// <param name="pattern"></param>
	/// <returns></returns>
	private string RegexFirstGroup(string input, string pattern)
	{
		var match = Regex.Match(input, pattern, RegexOptions.Multiline);
		if (match.Success)
			return match.Groups[1].Value.Trim();
		return null;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		MultipartPartParser nextPart;
		return ReadForNextPart(buffer, offset, count, out nextPart);
	}

	/// <summary>
	/// Moves the stream foward until a new part is found
	/// </summary>
	/// <param name="bufferSize"></param>
	/// <returns></returns>
	public MultipartPartParser ReadUntilNextPart(int bufferSize = 4096)
	{
		byte[] throwawayBuffer = new byte[bufferSize];
		MultipartPartParser nextpart;
		while (ReadForNextPart(throwawayBuffer, 0, bufferSize, out nextpart) > 0) { }
		return nextpart;
	}

	/// <summary>
	/// Reads the stream, if this part has completed the nextpart is returned
	/// </summary>
	/// <param name="buffer"></param>
	/// <param name="offset"></param>
	/// <param name="count"></param>
	/// <param name="nextpart"></param>
	/// <returns></returns>
	public int ReadForNextPart(byte[] buffer, int offset, int count, out MultipartPartParser nextpart)
	{
		//If we have found our next part we have already finsihed this part and should stop here
		if (NextPart != null || IsEndPart)
		{
			nextpart = NextPart;
			return 0;
		}

		//the search buffer is the place where we will scan for part bounderies. We need it to be just
		//a bit bigger than than the size requested, to ensure we dont accidnetly send part of a boundary
		//without realising it
		byte[] searchBuffer = new byte[count + BoundaryWithNewLinePrepend.Length];
		
		int bytesReadThisCall = 0;

		//first read from our local buffer
		int bytesToReadFromLocalBuffer = Math.Min((int)LocalBuffer.Length, searchBuffer.Length);
		if (bytesToReadFromLocalBuffer > 0)
		{
			bytesReadThisCall += LocalBuffer.Read(searchBuffer, bytesReadThisCall, bytesToReadFromLocalBuffer);
		}

		//if we could not fill our search buffer with our local buffer then read from the multipart stream
		int bytesToReadFromStream = searchBuffer.Length - bytesReadThisCall;
		bytesToReadFromStream = Math.Min(bytesToReadFromStream, (int)MultipartStream.Length - (int)MultipartStream.Position);
		
		if (bytesToReadFromStream > 0)
		{
			bytesReadThisCall += MultipartStream.Read(searchBuffer, bytesReadThisCall, bytesToReadFromStream);
		}

		//the number of bytes returned will be one of three cases
		//1. There is still plenty to return so we will return the 'count' they asked for
		//2. We have emptied the stream, we will return the bytes read
		//3. We have run into a new boundary, we will return the bytes up to the boundary end
		int bytesReturned;
		bool isEndOfPart = SearchBytePattern(BoundaryWithNewLinePrepend, searchBuffer, out bytesReturned);
		
		//we can only return the parts we know for sure are not part of the next boundary
		//which is the bytes we read minus the boundary length. This will also ensure we
		//get back to the count we were originally asked for. We also need to make sure we
		//return 0 bytes if we can not gaurentee there are no boundaries parts in what we 
		//did manage to read
		if (!isEndOfPart)
			bytesReturned = Math.Max(0, bytesReadThisCall - BoundaryWithNewLinePrepend.Length);



		Buffer.BlockCopy(searchBuffer, 0, buffer, offset, bytesReturned);

		//We need to handle the bytes that did not get returned by putting them back into
		//the local buffer
		int bytesNotReturned = bytesReadThisCall - bytesReturned;
		ReinsertIntoLocalBuffer(searchBuffer, bytesReturned, bytesNotReturned);

		nextpart = null;
		if (isEndOfPart)
		{
			//the boundary we were looking for had a newline appended to it
			//we dont want to send the newline to the next part so we will skip
			LocalBuffer.Position += NewLine.Length;
			NextPart = new MultipartPartParser(MultipartStream, Encoding, LocalBuffer);
			
			//The next part may actually just the be end indicator, if thats the case
			//we will null it and not return it
			if (NextPart.IsEndPart)
				NextPart = null;
			nextpart = NextPart;
		}

		
		return bytesReturned;
	}

	/// <summary>
	/// Searches for a byte pattern in a block of bytes
	/// </summary>
	/// <param name="pattern"></param>
	/// <param name="bytes"></param>
	/// <param name="matchStartIndex"></param>
	/// <returns></returns>
	protected bool SearchBytePattern(byte[] pattern, byte[] bytes, out int matchStartIndex)
	{
		return SearchBytePattern(pattern, bytes, 0, out matchStartIndex);
	}

	/// <summary>
	/// Searches for a byte pattern in a block of bytes
	/// </summary>
	/// <param name="pattern"></param>
	/// <param name="bytes"></param>
	/// <param name="searchOffset"></param>
	/// <param name="matchStartIndex"></param>
	/// <returns></returns>
	protected bool SearchBytePattern(byte[] pattern, byte[] bytes, int searchOffset, out int matchStartIndex)
	{
		if (pattern == null || pattern.Length == 0 || bytes == null || bytes.Length == 0)
		{
			matchStartIndex = -1;
			return false;
		}

		matchStartIndex = Array.IndexOf<byte>(bytes, pattern[0]);
		int searchUpToIndex = bytes.Length - pattern.Length;
		while (matchStartIndex > 0 && matchStartIndex < searchUpToIndex)
		{
			bool ismatch = true;
			for (int j = 1; j < pattern.Length && ismatch == true; j++)
			{
				if (bytes[matchStartIndex + j] != pattern[j])
					ismatch = false;
			}
			if (ismatch)
				return true;

			matchStartIndex = Array.IndexOf<byte>(bytes, pattern[0], matchStartIndex + 1);
		}

		matchStartIndex = -1;
		return false;
	}

	public override bool CanRead
	{
		get { return true; }
	}

	public override bool CanSeek
	{
		get { return false; }
	}

	public override bool CanWrite
	{
		get { return false; }
	}

	public override void Flush()
	{

	}

	public override long Length
	{
		get { throw new NotSupportedException(); }
	}

	public override long Position
	{
		get
		{
			throw new NotSupportedException();
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		throw new NotSupportedException();
	}

	public override void SetLength(long value)
	{
		throw new NotSupportedException();
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		throw new NotSupportedException();
	}

   
}
{% endhighlight %}


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


  [1]: assets/posts/code/MultipartPartParser/MultipartPartParser.cs