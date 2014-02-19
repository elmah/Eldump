#region License and Terms
//
// ELMAH Error Log Archiver for ASP.NET
// Copyright (c) 2010 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Eldump.AspNet
{
    #region Imports

    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    #endregion

    // Credit: http://stackoverflow.com/a/21513194/6682

    /// <remarks>
    /// This class exists only as a workaround to a 
    /// <a href="https://connect.microsoft.com/VisualStudio/feedback/details/816411/ziparchive-shouldnt-read-the-position-of-non-seekable-streams">bug</a> 
    /// in <c>System.IO.Compression</c> that prevents <c>ZipArchive</c> 
    /// from being used on a non-seekable stream (contrary to the 
    /// documentation stating that <c>ZipArchiveMode.Create</c> should 
    /// not require a seekable stream). This is a minimal implementation
    /// with only operations related to writing being supported.
    /// </remarks>

    sealed class PositionTrackingOutputStream : Stream
    {
        readonly Stream _wrapped;
        long _position;

        public PositionTrackingOutputStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            _wrapped = stream;
        }

        public override bool CanSeek  { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override long Position
        {
            get { return _position; }
            set { throw new NotSupportedException(); }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _position += count;
            _wrapped.Write(buffer, offset, count);
        }

        public override void Flush() { _wrapped.Flush(); }

        protected override void Dispose(bool disposing)
        {
            _wrapped.Dispose();
            base.Dispose(disposing);
        }

        public override int WriteTimeout
        {
            get { return _wrapped.WriteTimeout; }
            set { _wrapped.WriteTimeout = value; }
        }

        public override bool CanTimeout { get { return _wrapped.CanTimeout; } }

        public override void WriteByte(byte value) { _wrapped.WriteByte(value); }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _wrapped.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _wrapped.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult) { _wrapped.EndWrite(asyncResult); }

        public override Task FlushAsync(CancellationToken cancellationToken) { return _wrapped.FlushAsync(cancellationToken); }

        #region NotSupportedException

        public override bool CanRead { get { throw new NotSupportedException(); } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override int ReadTimeout { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override int ReadByte() { throw new NotSupportedException(); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { throw new NotSupportedException(); }
        public override int EndRead(IAsyncResult asyncResult) { throw new NotSupportedException(); }
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { throw new NotSupportedException(); }
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) { throw new NotSupportedException(); }
 
        #endregion
    }
}