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
