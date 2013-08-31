//
// ELMAH Sandbox
// Copyright (c) 2010-11 Atif Aziz. All rights reserved.
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

open System
open System.Diagnostics
open System.Net
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Threading
open HtmlAgilityPack
open Fizzler
open Fizzler.Systems.HtmlAgilityPack
open Elmah
open Mannex
open Mannex.Net

type ArgKind =
    | Named
    | Flag
type Arg =
    | Named of string * string
    | Flag of string
    | Atom of string

let parseOptions lax (names, flags) args =
    // Taken from LitS3:
    //   http://lits3.googlecode.com/svn-history/r109/trunk/LitS3.Commander/s3cmd.py
    // Copyright (c) 2008, Nick Farina
    // Author: Atif Aziz, http://www.raboof.com/
    let required = 
        names |> Seq.filter (fun (name : string) -> name.Slice(-1) = "!") 
              |> Seq.map (fun name -> name.Slice(0, new Nullable<int>(-1)))
              |> List.ofSeq
    let all = 
        names |> Seq.map (fun n -> n.TrimEnd([|'!'|]), ArgKind.Named)
              |> Seq.append(flags |> Seq.map(fun n -> n, ArgKind.Flag))
              |> List.ofSeq
    let rec parse args =
        match args with
        | [] ->
            []
        | arg :: args when arg = "--" -> // comment
            []
        | arg :: args when arg.StartsWith("--") ->
            let name = arg.Substring(2)            
            match all |> Seq.tryFind (fun (n, _) -> n = name) with
            | Some(n, ArgKind.Named) ->
                match args with
                | v :: args ->
                    Named(n, v) :: parse args
                | [] ->
                    failwith (sprintf "Missing argument value: %s" name)
            | Some(n, ArgKind.Flag) ->
                Flag(n) :: parse args
            | None ->
                if not lax then
                    failwith (sprintf "Unknown argument: %s" name)
                else
                    Atom(arg) :: parse args
        | arg :: args ->
            Atom(arg) :: parse args
    let args = parse args
    let nargs, fargs, args = 
        args |> Seq.choose (function | Named(n, v) -> Some(n, v) | _ -> None) |> Map.ofSeq,
        args |> Seq.choose (function | Flag(n)     -> Some(n)    | _ -> None) |> Set.ofSeq, 
        args |> Seq.choose (function | Atom(a)     -> Some(a)    | _ -> None) |> List.ofSeq
    match required |> Seq.tryFind (fun arg -> not (nargs.ContainsKey(arg))) with
    | Some(arg) -> failwith (sprintf "Missing required argument: %s" arg)
    | None -> nargs, fargs, args

let laxParseOptions = 
    parseOptions false

module CSV =
    let parse (reader : TextReader) =
        seq {
            use parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(reader)
            parser.Delimiters <- [|","|]
            while not parser.EndOfData do
                yield parser.ReadFields()
        }

    let parseFile (path : string) =
        seq {
            use reader = new StreamReader(path)
            yield! parse reader
        }

    let parseString text =
        parse (new StringReader(text))

let mapRecords (columns : string list) records =
    // COLUMN  = required
    // COLUMN? = optional
    let columns = 
        match columns with
        | [] -> []
        | _  -> [ for col in columns -> (col.TrimEnd('?'), if col.[col.Length - 1] = '?' then Seq.tryPick (* TODO SingleOrDefault *) else (fun f ihs -> Some(Seq.pick (* TODO Single *) f ihs))) ]
    let binder bindings records = seq {
        for fields in records -> [for b in bindings -> b |> Option.map (fun b -> fields |> Seq.nth b)]
    }
    let records = records |> Seq.cache
    seq {
        let fields = records |> Seq.head
        let bindings = [0..((fields |> Seq.length) - 1)]
        let bindings =
            match columns with
            | [] -> 
                [for i in bindings -> Some(i)]
            | _ ->
                let ifields = Seq.zip bindings fields
                [ for name, lookup in columns -> 
                    ifields |> lookup (fun (i, h) -> if name.Equals(h, StringComparison.OrdinalIgnoreCase) then Some(i) else None) ]
        yield! records |> Seq.skip 1 |> binder bindings
    }

let mapRecords2 col1 col2 f records =
    mapRecords [col1; col2] records |> Seq.map (fun fs -> f fs.[0] fs.[1])

let downloadText (url : Uri) = async {
    use wc = new WebClient()
    return! wc.AsyncDownloadString(url)
}

let downloadErrorsIndex (url : Uri) =
    let url = if url.IsFile then url else new Uri(url.ToString() + "/download")
    let log = downloadText url |> Async.RunSynchronously
    let selector url xmlref = 
        let url = new Uri(url |> Option.get, UriKind.Absolute)
        let xmlref = xmlref |> Option.map (fun v -> new Uri(v, UriKind.Absolute))
        url, xmlref
    log |> CSV.parseString 
        |> mapRecords2 "URL" "XMLREF?" selector
        |> List.ofSeq

let resolveErrorXmlRef url xmlref = async {
    match xmlref with
    | Some(url) -> return url
    | None ->
        let! html = downloadText url
        let doc = new HtmlDocument()
        doc.LoadHtml(html)
        let node = doc.DocumentNode.QuerySelector("a[rel=alternate][type*=xml]")
        if node = null then
            return failwith (sprintf "XML data for not found for [%s]." (url.ToString()))
        else
            let href = new Uri(node.Attributes.["href"].Value, UriKind.RelativeOrAbsolute)
            return new Uri(url, href)
}

let downloadError url xmlref = async {
    let! xmlref = resolveErrorXmlRef url xmlref
    let! xml = downloadText xmlref
    return xmlref, ErrorXml.DecodeString(xml), xml
}

let slugize url =
    Regex.Replace(Regex.Replace(url, @"[^A-Za-z0-9\-]", "-"), "-{2,}", "-")

module Options =
    [<Literal>] 
    let OUTPUT_DIR = "output-dir"
    [<Literal>] 
    let SILENT = "silent"
    [<Literal>] 
    let TRACE = "trace"

type DownloadResult =
| NewDownload of (Uri * Error * string * string)
| PreDownloaded of (Uri * string)

let run args =
    
    let namedOptions = [Options.OUTPUT_DIR]
    let boolOptions = [Options.SILENT; Options.TRACE]
        
    let nargs, flags, args = 
        args |> List.ofArray 
             |> laxParseOptions (namedOptions, boolOptions)

    let verbose = not (flags.Contains Options.SILENT)

    if (flags.Contains Options.TRACE) then 
        Trace.Listeners.Add(new ConsoleTraceListener(true)) |> ignore

    let outdir = defaultArg (nargs.TryFind Options.OUTPUT_DIR) "."
    Directory.CreateDirectory(outdir) |> ignore

    match args with
    | [] ->
        failwith "Missing ELMAH index URL (e.g. http://www.example.com/elmah.axd)."
    | arg :: _ -> 

        let homeUrl = new Uri(arg)
        let urls = downloadErrorsIndex(homeUrl)

        let title = Console.Title
        try

            let counter = ref 0
            let tick() =
                Interlocked.Increment(&counter.contents) |> ignore
                String.Format("Error {0:N0} of {1:N0}", !counter, urls.Length)
            
            seq {

                for url, xmlref in urls -> async {

                    let fname = "error-" + (slugize (url.AbsoluteUri)) + ".xml"
                    let path = Path.Combine(outdir, fname)

                    let! status = async {
                        if File.Exists(path) then
                            if verbose then Console.WriteLine((sprintf "%s SKIPPED" (url.ToString())))
                            return tick()
                        else
                            let! url, error, xml = downloadError url xmlref
                            let status = tick()
                            if verbose then
                                let lines = [|
                                    sprintf "%s" (url.ToString());
                                    sprintf "%s: %s" status (error.Type);
                                    sprintf "%s\n" (error.Message);
                                |]
                                Console.WriteLine(String.Join(Environment.NewLine, lines))
                            File.WriteAllText(path, xml)
                            return status
                    }

                    Console.Title <- status
                }
            }
            |> Async.Parallel |> Async.RunSynchronously |> ignore

        finally
            Console.Title <- title

[<EntryPoint>]
let main args =
    try
        run args        
        0
    with
    | e -> 
        eprintfn "%s" (e.GetBaseException().Message)
        Trace.TraceError(e.ToString())
        1
