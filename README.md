# Eldump

Eldump is a client-side and ASP.NET solution for downloading all errors 
logged at a web site using ELMAH.

## Client-Side

Eldump is a console application (written in F#) that can download all errors 
logged at a web site using ELMAH. Each error downloaded is dumped as a 
stand-alone XML file. It does not matter what backing store ELMAH is using on 
the web site. It also does not matter what version of ELMAH is being used.

All you need to dump out the errors is the URL you usually use to visit 
the first page of logged error in ELMAH. The simplest usage is therefore:

    eldump http://www.example.com/elmah.axd

Optionally, you can also specify an output directory like this:

    eldump http://www.example.com/elmah.axd --output-dir %temp%\errors

Eldump needs HTTP GET and HEAD permissions to the ELMAH handler URL to work.

## Server-Side

Eldump is an [ASP.NET HTTP handler][1] that takes all the errors in an ELMAH 
error log and compresses them into a ZIP archive for downloading. 

Eldump can be deployed into the `bin` folder of a running ASP.NET web 
application and enabled via configuration, without the need for any changes 
to the application code. Just add the following to the `web.config` of the 
ASP.NET application (changing the `path` as you like):

```xml
<location path="eldump.axd">  
    <system.web>
        <httpHandlers>
            <add verb="GET,HEAD" 
                 path="eldump.axd" 
                 type="Eldump.AspNet.ErrorLogArchiveHandler, Eldump.AspNet" />
        </httpHandlers>
        <authorization>
            <deny users="*" />  
        </authorization>  
    </system.web>
    <system.webServer>
        <handlers>
            <add name="ELMAH" 
                 verb="GET,HEAD"
                 path="eldump.axd" 
                 type="Eldump.AspNet.ErrorLogArchiveHandler, Eldump.AspNet"
                 preCondition="integratedMode" />
        </handlers>
    </system.webServer>
</location>  
```


As an alternative to the above, Eldump can also register itself 
automatically by simply adding the following entry to the 
[`appSettings`][2]:

```xml
<add key="eldump:enabled" value="true" />
```

With auto-registration, Eldump will respond to any URL with `eldump` or 
`eldump.axd` as a path component. It will also forbid unauthenticated 
requests. You can change these defaults by setting a delegate to 
the `ErrorLogArchiveHandler.RequestPredicate` static property during 
application initialization and which will determine under which request 
conditions Eldump's handler will respond.

Once deployed, a ZIP of the error log can be downloaded by simply visiting
the configured path under the web application root. For example, if the web 
application is hosted at `http://www.example.com/` and the handler is 
configured to respond to the path `eldump.axd` as shown above, then the URL 
to generate the error log archive would be 
`http://www.example.com/eldump.axd`.

[1]: http://msdn.microsoft.com/en-us/library/system.web.ihttphandler.aspx
[2]: http://msdn.microsoft.com/en-us/library/system.configuration.configurationmanager.appsettings.aspx