# ELMAH Error Log Export Utility
# Copyright (c) 2010-11 Atif Aziz. All rights reserved.
# Portions Copyright (c) 2008, Nick Farina
#
#  Author(s):
#
#      Atif Aziz, http://www.raboof.com
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#    http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# IronPython script to export an ELMAH error log into XML files

import clr, sys

clr.AddReference('Elmah')
clr.AddReference('HtmlAgilityPack')
clr.AddReference('Fizzler.Systems.HtmlAgilityPack')
clr.AddReference('Mannex')
clr.AddReference('Microsoft.VisualBasic')

from System import Uri, UriKind, Console, Array, StringComparison, Environment
from System.Collections.Specialized import NameValueCollection
from System.IO import StringReader, File, Directory, Path
from System.Net import WebClient
from System.Text.RegularExpressions import Regex
from Elmah import ErrorXml
from HtmlAgilityPack import HtmlDocument
from Fizzler.Systems.HtmlAgilityPack import HtmlNodeSelection
from Microsoft.VisualBasic.FileIO import TextFieldParser
from Mannex.Net.WebClientExtensions import DownloadStringUsingResponseEncoding

clr.AddReference('System.Core')

from System.Linq.Enumerable import Skip as skip
from System.Linq.Enumerable import Take as take
from System.Linq.Enumerable import Single as single
from System.Linq.Enumerable import SingleOrDefault as single_or_default

def parse_options(args, names, flags = None, lax = False):
    # Taken from LitS3:
    #   http://lits3.googlecode.com/svn-history/r109/trunk/LitS3.Commander/s3cmd.py
    # Copyright (c) 2008, Nick Farina
    # Author: Atif Aziz, http://www.raboof.com/
    args = list(args) # copy for r/w
    required = [name[:-1] for name in names if '!' == name[-1:]]
    all = [name.rstrip('!') for name in names]
    if flags:
        all.extend(flags)
    options = {}
    anon = []
    while args:
        arg = args.pop(0)
        if arg[:2] == '--':
            name = arg[2:]
            if not name: # comment
                break
            if not name in all:
                if not lax:
                    raise Exception('Unknown argument: %s' % name)
                anon.append(arg)
                continue
            if flags and name in flags:
                options[name] = True
            elif args:
                options[name] = args.pop(0)
            else:
                raise Exception('Missing argument value: %s' % name)
        else:
            anon.append(arg)
    for name in required:
        if not name in options:
            raise Exception('Missing required argument: %s' % name)
    return options, anon

def lax_parse_options(args, names, flags = None):
    return parse_options(args, names, flags, True)

def parse_csv(reader, selector = None):
    with TextFieldParser(reader) as parser:
        parser.Delimiters = Array[str](',')
        while not parser.EndOfData:
            fields = parser.ReadFields()
            yield selector and selector(fields) or tuple(fields)

def parse_csv_str(text, selector = None):
    return parse_csv(StringReader(text), selector)

def map_records(records, columns = None, selector = None):
    # COLUMN  = required
    # COLUMN? = optional
    columns = [(col.rstrip('?'), col[-1] == '?' and single_or_default[tuple] 
                                                  or single[tuple]) 
                for col in columns]
    bindings = None
    for fields in records:
        if bindings is None:
            bindings = range(len(fields))
            if not columns is None:
                ifields = zip(bindings, fields)
                bindings = [
                    resolution and resolution[0] or -1
                    for resolution in [
                        lookup(ifields, lambda t: name.Equals(t[1], StringComparison.OrdinalIgnoreCase)) 
                        for name, lookup in columns]]
                continue
        values = tuple(b >= 0 and fields[b] or None for b in bindings)
        yield selector and selector(values) or tuple(values)

def download_text(url, wc = None):
    wc = wc and wc or WebClient()
    if url.IsFile:
        return DownloadStringUsingResponseEncoding(wc, url) 
    else:
        return wc.DownloadString(url)

def download_errors_index(url):
    log = download_text(url.IsFile and url or Uri(str(url) + '/download'))
    records = parse_csv_str(log)
    return list(map_records(records, ('URL', 'XMLREF?'), 
                lambda t: tuple(v and Uri(v, UriKind.Absolute) or None 
                                for v in t)))

def resolve_error_xmlref(url, xmlref):
    if xmlref is None:
        html = download_text(url)
        doc = HtmlDocument()
        doc.LoadHtml(html)
        node = HtmlNodeSelection.QuerySelector(doc.DocumentNode, 'a[rel=alternate][type*=xml]')
        if not node:
            return None, None
        href = Uri(node.Attributes['href'].Value, UriKind.RelativeOrAbsolute)
        xmlref = Uri(url, href)
    return xmlref

def download_error(url, xmlref):
    xmlref = resolve_error_xmlref(url, xmlref)
    xml = download_text(xmlref)
    return xmlref, ErrorXml.DecodeString(xml), xml

def tidy_fname(url):
    return Regex.Replace(Regex.Replace(url, r'[^A-Za-z0-9\-]', '-'), '-{2,}', '-')

def main(args):

    OPTION_OUTPUT_DIR = 'output-dir'
    OPTION_SILENT = 'silent'

    named_options = (OPTION_OUTPUT_DIR, )
    bool_options = (OPTION_SILENT, )
    options, args = parse_options(args, named_options, bool_options)

    silent = options.get(OPTION_SILENT, False)
    
    outdir = options.get(OPTION_OUTPUT_DIR, '.')   
    Directory.CreateDirectory(outdir)

    if not args:
        raise Exception('Missing ELMAH index URL (e.g. http://www.example.com/elmah.axd).')
    home_url = Uri(args.pop(0))
    
    urls = download_errors_index(home_url)
    errors = (download_error(url, xmlref) for url, xmlref in urls)
    
    title = Console.Title
    try:
        counter = 0
        for url, error, xml in errors:
            counter = counter + 1
            status = str.Format('Error {0:N0} of {1:N0}', counter, len(urls))
            Console.Title = status
            if not silent:
                print url
                print '%s: %s' % (status, error.Type)
                print error.Message
                print
            fname = "error-" + tidy_fname(url.AbsoluteUri) + ".xml"
            File.WriteAllText(Path.Combine(outdir, fname), xml)
    finally:
        Console.Title = title

if __name__ == '__main__':
    try:
        main(sys.argv[1:])
    except Exception, e:
        print >> sys.stderr, e
        sys.exit(1)
