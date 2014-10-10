#region License, Terms and Author(s)
//
// ELMAH - Error Logging Modules and Handlers for ASP.NET
// Copyright (c) 2004-9 Atif Aziz. All rights reserved.
//
//  Author(s):
//
//      Atif Aziz, http://www.raboof.com
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
    using System;

    static class EventingExtensions
    {
        public static void Subscribe<T>(this T source,
            Action<EventHandler> addHandler, Action<T> handler)
        {
            source.Subscribe(addHandler, null, handler);
        }

        public static IDisposable Subscribe<T>(this T source, 
            Action<EventHandler> addHandler, Action<EventHandler> removeHandler, 
            Action<T> handler)
        {
            // ReSharper disable once CompareNonConstrainedGenericWithNull
            if (source == null) throw new ArgumentNullException("source");
            if (addHandler == null) throw new ArgumentNullException("addHandler");
            if (handler == null) throw new ArgumentNullException("handler");

            EventHandler h = (sender, _) => handler(((T) sender));
            addHandler(h);
            return removeHandler != null 
                ? new DelegatingDisposable(() => removeHandler(h)) 
                : null;
        }
    }
}