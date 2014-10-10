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
    #region Imports

    using System;
    using System.Linq;
    using System.Reflection;
    using System.Web;

    #endregion

    class HandlerModule : IHttpModule
    {
        readonly Func<HttpContextBase, IHttpHandler> _mapper;

        public HandlerModule(Func<HttpContextBase, IHttpHandler> mapper)
        {
            _mapper = mapper;
        }

        public virtual void Init(HttpApplication app)
        {
            if (_mapper == null)
                return;

            app.Subscribe(h => app.PostMapRequestHandler += h, context =>
            {
                var handler = _mapper(context);
                if (handler != null)
                    context.Handler = handler;
            });
        }

        public virtual void Dispose() { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HandlerFactoryMethodAttribute : Attribute {}

    sealed class HandlerModule<T> : HandlerModule
    {
        // ReSharper disable once StaticFieldInGenericType
        static readonly Func<HttpContextBase, IHttpHandler> Mapper;

        static HandlerModule()
        {
            var methods = typeof(T).FindMembers(MemberTypes.Method,
                                                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                                                (m, _) => Attribute.IsDefined(m, typeof(HandlerFactoryMethodAttribute), true),
                                                null);

            var method = (MethodInfo) methods.SingleOrDefault();
            if (method == null)
                return;

            Mapper = (Func<HttpContextBase, IHttpHandler>)Delegate.CreateDelegate(typeof(Func<HttpContextBase, IHttpHandler>), method, throwOnBindFailure: false);
        }

        public HandlerModule() : base(Mapper) { }
    }
}