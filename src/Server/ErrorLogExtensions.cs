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
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Elmah;

    #endregion

    static class ErrorLogExtensions
    {
        public static IEnumerable<T> PageAllErrors<T>(this ErrorLog log, int pageSize, Func<Task, T> taskSelector, Func<ErrorLogEntry[], T> pageSelector)
        {
            if (log == null) throw new ArgumentNullException("log");
            if (taskSelector == null) throw new ArgumentNullException("taskSelector");
            if (pageSelector == null) throw new ArgumentNullException("pageSelector");

            for (var pageIndex = 0; ; pageIndex++)
            {
                IList entryList = new ArrayList(pageSize);
                Task task;
                yield return taskSelector(task = log.GetErrorsAsync(pageIndex, pageSize, entryList));
                task.Wait();

                if (entryList.Count == 0)
                    break;

                var entries = new ErrorLogEntry[entryList.Count];
                entryList.CopyTo(entries, 0);
                yield return pageSelector(entries);
            }
        }

        public static async Task<int> GetErrorsAsync(this ErrorLog log, int pageIndex, int pageSize, ICollection<ErrorLogEntry> entries, CancellationToken cancellationToken)
        {
            if (log == null) throw new ArgumentNullException("log");
            if (entries == null) throw new ArgumentNullException("entries");

            cancellationToken.ThrowIfCancellationRequested();

            var entryList = entries as IList;
            var indirect = entryList == null;
            if (indirect)
                entryList = new List<ErrorLogEntry>();
            
            var total = await Task.Factory.FromAsync<int, int, IList, int>(log.BeginGetErrors, log.EndGetErrors, pageIndex, pageSize, entryList, null);
            
            if (indirect)
            {
                foreach (ErrorLogEntry e in entryList)
                    entries.Add(e);
            }
            
            return total;
        }

        static Task<int> GetErrorsAsync(this ErrorLog log, int pageIndex, int pageSize, IList entries)
        {
            return Task.Factory.FromAsync<int, int, IList, int>(log.BeginGetErrors, log.EndGetErrors, pageIndex, pageSize, entries, null);
        }

        public static Task<ErrorLogEntry> GetErrorAsync(this ErrorLog log, string id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Factory.FromAsync<string, ErrorLogEntry>(log.BeginGetError, log.EndGetError, id, null);
        }
    }
}