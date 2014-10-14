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
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Elmah;

    #endregion

    public sealed class ErrorLogArchiveHandler : HttpTaskAsyncHandler
    {
        public static readonly Func<HttpContextBase, bool> DefaultRequestPredicate = context =>
            context.Request.IsAuthenticated
            && context.Request.Path.Split('/').Any(s => "eldump" == s || "eldump.axd" == s);

        static Func<HttpContextBase, bool> _requestPredicate;

        public static Func<HttpContextBase, bool> RequestPredicate
        {
            get { return _requestPredicate ?? DefaultRequestPredicate; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                _requestPredicate = value;
            }
        }

        [HandlerFactoryMethod] // ReSharper disable once UnusedMember.Local
        static IHttpHandler OnRequest(HttpContextBase context)
        {
            return RequestPredicate(context) ? new ErrorLogArchiveHandler() : null;
        }

        public override Task ProcessRequestAsync(HttpContext context)
        {
            var log = ErrorLog.GetDefault(context);
            return ProcessRequestAsync(new HttpContextWrapper(context), log);
        }

        public async static Task ProcessRequestAsync(HttpContextBase context, ErrorLog log)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (log == null) throw new ArgumentNullException("log");

            // TODO By default show a web page where user can parameterize the archival
            //      ...the web page should have FORM using GET method
            // TODO Allow user to set the download file name
            // TODO Allow user to specify download file should be timestamped
            // TODO Allow user to specify stop count, time or Id
            // TODO Allow differential archival through a cookie
            // TODO Support GZIP-ped TAR?
            // TODO Address potential duplication of error log entires

            var response = context.Response;
            response.BufferOutput = false;
            response.ContentType = "application/zip";
            response.Headers["Content-Disposition"] = "attachement; filename=errorlog.zip";

            using (var zip = new ZipArchive(new PositionTrackingOutputStream(response.OutputStream), ZipArchiveMode.Create, leaveOpen: true))
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(context.Request.TimedOutToken, response.ClientDisconnectedToken))
            {
                // ReSharper disable once AccessToDisposedClosure
                await Archive(log, Encoding.UTF8, e => zip.CreateEntry(string.Format("error-{0}.xml", e.Id)).Open(), cts.Token);
            }
        }

        static Task Archive(ErrorLog log, Encoding encoding, Func<ErrorLogEntry, Stream> opener, CancellationToken cancellationToken)
        {
            return Archive(log.GetErrorsAsync, log.GetErrorAsync, encoding, opener, cancellationToken);
        }

        static async Task Archive(
            Func<int, int, ICollection<ErrorLogEntry>, CancellationToken, Task<int>> pager,
            Func<string, CancellationToken, Task<ErrorLogEntry>> detailer, 
            Encoding encoding, Func<ErrorLogEntry, Stream> opener, 
            CancellationToken cancellationToken)
        {
            if (pager == null) throw new ArgumentNullException("pager");
            if (detailer == null) throw new ArgumentNullException("detailer");
            if (encoding == null) throw new ArgumentNullException("encoding");
            if (opener == null) throw new ArgumentNullException("opener");

            for (var pageIndex = 0; ; pageIndex++)
            {
                const int pageSize = 100;
                var entries = new List<ErrorLogEntry>(pageSize);
                cancellationToken.ThrowIfCancellationRequested();
                await pager(pageIndex, pageSize, entries, cancellationToken);

                if (entries.Count == 0)
                    break;

                foreach (var e in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var detail = await detailer(e.Id, cancellationToken);
                    using (var entryStream = opener(e))
                    {
                        var bytes = encoding.GetBytes(ErrorXml.EncodeString(detail.Error));
                        await entryStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    }
                }
            }
        }
    }
}
