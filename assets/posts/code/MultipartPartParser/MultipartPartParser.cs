using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace MultipartParser
{
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
        public bool IsEndPart { get; private set; }

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
}