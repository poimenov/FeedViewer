[<AutoOpen>]
module FeedViewer.Services

open FSharp.Data
open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Xml.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open System.Text
open CodeHollow.FeedReader

type Platform =
    | Windows
    | Linux
    | MacOS
    | Unknown

type IPlatformService =
    abstract member GetPlatform: unit -> Platform

type PlatformService() =
    interface IPlatformService with
        member this.GetPlatform() =
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                Windows
            elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                Linux
            elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                MacOS
            else
                Unknown

type IProcessService =
    abstract member Run: command: string * arguments: string -> unit

type ProcessService() =
    interface IProcessService with
        member this.Run(command: string, arguments: string) =
            let psi = new ProcessStartInfo(command)
            psi.RedirectStandardOutput <- false
            psi.UseShellExecute <- false
            psi.CreateNoWindow <- false
            psi.Arguments <- arguments

            let p = new Process()
            p.StartInfo <- psi
            p.Start() |> ignore

type ILinkOpeningService =
    abstract member OpenUrl: url: string -> unit

type LinkOpeningService(platformService: IPlatformService, processService: IProcessService) =
    interface ILinkOpeningService with
        member this.OpenUrl(url: string) =
            match platformService.GetPlatform() with
            | Windows -> processService.Run("cmd", $"/c start {url}")
            | Linux -> processService.Run("xdg-open", url)
            | MacOS -> processService.Run("open", url)
            | _ -> ()

type IExportImportService =
    abstract member Export: string -> unit
    abstract member Import: string -> unit

type ExportImportService(channelGroups: IChannelGroups, channels: IChannels) =
    interface IExportImportService with
        member this.Export(filePath: string) =
            if String.IsNullOrEmpty(filePath) || Path.GetExtension(filePath) <> ".opml" then
                ()
            else
                let getOutline (channel: Channel) =
                    new XElement(
                        "outline",
                        new XAttribute("type", "rss"),
                        new XAttribute("text", channel.Title),
                        new XAttribute("title", channel.Title),
                        new XAttribute("xmlUrl", channel.Url),
                        new XAttribute("htmlUrl", defaultArg channel.Link "")
                    )

                let getGroupOutlines (groupId: int) =
                    channels.GetByGroupId(Some(groupId)) |> List.map (fun c -> getOutline c)

                let getOutlines = channels.GetByGroupId(None) |> List.map (fun c -> getOutline c)

                let getOutlineGroups =
                    channelGroups.GetAll()
                    |> List.map (fun g ->
                        new XElement(
                            "outline",
                            new XAttribute("text", g.Name),
                            new XAttribute("title", g.Name),
                            getGroupOutlines g.Id
                        ))

                let head = new XElement("head", new XElement("title", "FeedViewer"))
                let body = new XElement("body", getOutlineGroups, getOutlines)

                let doc =
                    new XDocument(new XElement("opml", new XAttribute("version", "1.0"), head, body))

                doc.Save(filePath)

        member this.Import(filePath: string) =
            if
                String.IsNullOrEmpty(filePath)
                || Path.GetExtension(filePath) <> ".opml"
                || not (File.Exists(filePath))
            then
                ()
            else
                let doc = XDocument.Load(filePath)

                let importChannel (groupId: int option, channel: XElement) =
                    channels.Create(
                        new Channel(
                            id = 0,
                            groupId = groupId,
                            title = channel.Attribute(XName.Get("text")).Value,
                            description = Some(channel.Attribute(XName.Get("text")).Value),
                            link = Some(channel.Attribute(XName.Get("htmlUrl")).Value),
                            url = channel.Attribute(XName.Get("xmlUrl")).Value,
                            imageUrl = None,
                            language = None
                        )
                    )

                doc
                    .Element(XName.Get("opml"))
                    .Element(XName.Get("body"))
                    .Elements(XName.Get("outline"))
                |> Seq.filter (fun e -> e.Attributes() |> Seq.exists (fun a -> a.Name.LocalName = "xmlUrl") |> not)
                |> Seq.iter (fun group ->
                    let groupId =
                        channelGroups.Create(new ChannelGroup(id = 0, name = group.Attribute(XName.Get("text")).Value))

                    group.Elements(XName.Get("outline"))
                    |> Seq.filter (fun e -> e.Attribute(XName.Get("type")).Value = "rss")
                    |> Seq.iter (fun channel -> importChannel (Some(groupId), channel) |> ignore))

                doc
                    .Element(XName.Get("opml"))
                    .Element(XName.Get("body"))
                    .Elements(XName.Get("outline"))
                |> Seq.filter (fun e -> e.Attributes() |> Seq.exists (fun a -> a.Name.LocalName = "xmlUrl"))
                |> Seq.iter (fun channel -> importChannel (None, channel) |> ignore)

type IOpenDialogService =
    abstract member OpenFile:
        ?title: string * ?defaultPath: string * ?multiSelect: bool * ?filters: (struct (string * string array)) array ->
            string array

    abstract member OpenFolder: ?title: string * ?defaultPath: string * ?multiSelect: bool -> string array

type OpenDialogService(app: Photino.Blazor.PhotinoBlazorApp) =
    interface IOpenDialogService with
        member this.OpenFile(title, defaultPath, multiSelect, filters) =
            app.MainWindow.ShowOpenFile(
                Option.toObj title,
                Option.toObj defaultPath,
                Option.defaultValue false multiSelect,
                Option.toObj filters
            )

        member this.OpenFolder(title, defaultPath, multiSelect) =
            app.MainWindow.ShowOpenFolder(
                Option.toObj title,
                Option.toObj defaultPath,
                Option.defaultValue false multiSelect
            )

type IHttpHandler =
    abstract member GetStringAsync: string -> Task<string>
    abstract member GetByteArrayAsync: string -> Task<byte[]>
    abstract member GetFeedUrlsFromUrlAsync: string -> Task<string[]>
    abstract member LoadFromWebAsync: string -> Task<HtmlDocument>
    abstract member GetFeedAsync: string -> Task<Feed>

type HttpHandler() =
    [<Literal>]
    let USER_AGENT_HEADER_NAME = "User-Agent"

    [<Literal>]
    let USER_AGENT =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"

    interface IHttpHandler with
        member this.GetByteArrayAsync(url: string) : Task<byte[]> =
            async {
                let! response =
                    Http.AsyncRequestStream(
                        url = url,
                        headers = [| (USER_AGENT_HEADER_NAME, USER_AGENT) |],
                        httpMethod = "GET"
                    )

                use ms = new MemoryStream()
                do! response.ResponseStream.CopyToAsync(ms) |> Async.AwaitTask
                return ms.ToArray()
            }
            |> Async.StartAsTask

        member this.GetFeedUrlsFromUrlAsync(url: string) : Task<string[]> =
            async {
                let! feedUrls = FeedReader.GetFeedUrlsFromUrlAsync(url, true) |> Async.AwaitTask
                return feedUrls |> Seq.map (fun f -> f.Url) |> Seq.toArray
            }
            |> Async.StartAsTask

        member this.GetStringAsync(url: string) : Task<string> =
            async {
                let! response = Http.AsyncRequestString(url, headers = [| (USER_AGENT_HEADER_NAME, USER_AGENT) |])
                return response
            }
            |> Async.StartAsTask

        member this.LoadFromWebAsync(url: string) : Task<HtmlDocument> =
            async { return! HtmlDocument.AsyncLoad(url) } |> Async.StartAsTask

        member this.GetFeedAsync(arg1: string) : Task<Feed> =
            async {
                let! response = Http.AsyncRequestString(arg1, headers = [| (USER_AGENT_HEADER_NAME, USER_AGENT) |])
                let doc = XDocument.Parse(response)
                return FeedReader.ReadFromString(doc.ToString())
            }
            |> Async.StartAsTask

type IIconDownloader =
    abstract member DownloadIconAsync: Uri option * Uri option * string -> Task<unit>
    abstract member GetIconExtension: byte[] -> string
    abstract member SaveIconAsync: string * Uri * string -> Task<unit>

type IconDownloader(http: IHttpHandler, logger: ILogger<IconDownloader>) =
    interface IIconDownloader with
        member this.DownloadIconAsync
            (imageUri: Uri option, siteUri: Uri option, iconsDirectoryPath: string)
            : Task<unit> =
            async {
                if imageUri.IsNone && siteUri.IsNone then
                    return ()

                try
                    if not (Directory.Exists(iconsDirectoryPath)) then
                        Directory.CreateDirectory(iconsDirectoryPath) |> ignore

                    match siteUri with
                    | Some uri when Directory.GetFiles(iconsDirectoryPath, $"{uri.Host}.*").Length = 0 ->
                        if imageUri.IsNone then
                            let! doc = http.LoadFromWebAsync(siteUri.Value.ToString()) |> Async.AwaitTask

                            let links =
                                doc.Descendants [ "link" ]
                                |> Seq.filter (fun x ->
                                    x.HasAttribute("rel", "icon")
                                    || x.HasAttribute("rel", "shortcut icon")
                                    || x.HasAttribute("rel", "apple-touch-icon"))
                                |> Seq.toArray

                            if links.Length > 0 then
                                do!
                                    (this :> IIconDownloader)
                                        .SaveIconAsync(links[0].AttributeValue("href"), uri, iconsDirectoryPath)
                                    |> Async.AwaitTask
                        else
                            do!
                                (this :> IIconDownloader)
                                    .SaveIconAsync(imageUri.Value.ToString(), uri, iconsDirectoryPath)
                                |> Async.AwaitTask
                    | _ -> ()

                with ex ->
                    Debug.WriteLine(ex)

                    logger.LogError(
                        ex,
                        $"imageUri = {imageUri}, siteUri = {siteUri}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"
                    )
            }
            |> Async.StartAsTask

        member this.GetIconExtension(fileBytes: byte array) : string =
            let jpegMagic = [| 0xFFuy; 0xD8uy |]
            let pngMagic = [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |]
            let gifMagic = [| 0x47uy; 0x49uy; 0x46uy; 0x38uy |]
            let icoMagic = [| 0x00uy; 0x00uy; 0x01uy; 0x00uy |]
            let bmpMagic = [| 0x42uy; 0x4Duy |]
            let webpMagic = [| 0x57uy; 0x45uy; 0x42uy; 0x50uy |]

            let mutable retVal = ""

            if fileBytes.Length >= 4 then
                if fileBytes.[0] = jpegMagic.[0] && fileBytes.[1] = jpegMagic.[1] then
                    retVal <- ".jpg"
                elif fileBytes.[0] = pngMagic.[0] && fileBytes.[1] = pngMagic.[1] then
                    retVal <- ".png"
                elif fileBytes.[0] = gifMagic.[0] && fileBytes.[1] = gifMagic.[1] then
                    retVal <- ".gif"
                elif
                    fileBytes.[0] = icoMagic.[0]
                    && fileBytes.[1] = icoMagic.[1]
                    && fileBytes.[2] = icoMagic.[2]
                    && fileBytes.[3] = icoMagic.[3]
                then
                    retVal <- ".ico"
                elif fileBytes.[0] = bmpMagic.[0] && fileBytes.[1] = bmpMagic.[1] then
                    retVal <- ".bmp"
                elif
                    fileBytes.[0] = webpMagic.[0]
                    && fileBytes.[1] = webpMagic.[1]
                    && fileBytes.[2] = webpMagic.[2]
                    && fileBytes.[3] = webpMagic.[3]
                then
                    retVal <- ".webp"

            retVal

        member this.SaveIconAsync(url: string, host: Uri, iconsDirectoryPath: string) : Task<unit> =
            async {
                let urlToDownload = new Uri(host, url)
                let! data = http.GetByteArrayAsync(urlToDownload.AbsoluteUri) |> Async.AwaitTask

                if data.Length > 0 then
                    let ext = (this :> IIconDownloader).GetIconExtension data
                    let fileName = $"{host.Host}{ext}"
                    let filePath = Path.Combine(iconsDirectoryPath, fileName)
                    File.WriteAllBytes(filePath, data)
            }
            |> Async.StartAsTask

type IChannelReader =
    abstract member ReadChannelAsync: int * string -> Task<Channel>

type ChannelReader
    (
        http: IHttpHandler,
        iconDownloader: IIconDownloader,
        channels: IChannels,
        channelItems: IChannelItems,
        logger: ILogger<ChannelReader>
    ) =
    interface IChannelReader with
        member this.ReadChannelAsync(channelId: int, iconsDirectoryPath: string) : Task<Channel> =
            async {
                let channel = channels.Get(channelId)

                if channel.IsNone then
                    return failwith "Channel not found"

                try
                    Debug.WriteLine(
                        $"Start read url = {channel.Value.Url}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"
                    )

                    let mutable feed: Feed option = None

                    try
                        let! result = http.GetFeedAsync(channel.Value.Url) |> Async.AwaitTask
                        feed <- Some result
                    with ex ->
                        let! feedLinks = http.GetFeedUrlsFromUrlAsync(channel.Value.Url) |> Async.AwaitTask

                        if feedLinks.Length > 0 then
                            let! result = http.GetFeedAsync(feedLinks[0]) |> Async.AwaitTask
                            feed <- Some result
                            channel.Value.Url <- feedLinks[0]

                    if feed.IsSome then
                        let mutable imageUrl: Uri option = None

                        if not (String.IsNullOrEmpty(feed.Value.ImageUrl)) then
                            imageUrl <- Some(Uri(feed.Value.ImageUrl))

                        let mutable siteUri: Uri option = None

                        let siteLink =
                            if channel.Value.Link.IsSome then
                                Uri(channel.Value.Link.Value).GetLeftPart(UriPartial.Authority)
                            else
                                channel.Value.Link.Value

                        if not (String.IsNullOrEmpty(siteLink)) then
                            siteUri <- Some(Uri(siteLink))

                        do!
                            iconDownloader.DownloadIconAsync(imageUrl, siteUri, iconsDirectoryPath)
                            |> Async.AwaitTask

                        if not (String.IsNullOrEmpty(feed.Value.Title)) then
                            channel.Value.Title <- feed.Value.Title

                        if String.IsNullOrEmpty(feed.Value.Link) then
                            channel.Value.Link <- Some(feed.Value.Link)
                        else
                            channel.Value.Link <- Some(siteLink)

                        if not (String.IsNullOrEmpty(feed.Value.Description)) then
                            channel.Value.Description <- Some(feed.Value.Description)

                        if not (String.IsNullOrEmpty(feed.Value.Language)) then
                            channel.Value.Language <- Some(feed.Value.Language)

                        if not (String.IsNullOrEmpty(feed.Value.ImageUrl)) then
                            channel.Value.ImageUrl <- Some(feed.Value.ImageUrl)

                        channels.Update(channel.Value)

                        feed.Value.Items
                        |> Seq.map (fun x ->
                            let publishingDate =
                                if x.PublishingDate.HasValue then
                                    Some(x.PublishingDate.Value)
                                else
                                    None

                            let categories =
                                if x.Categories.Count > 0 then
                                    Some(x.Categories |> Seq.toList)
                                else
                                    None

                            ChannelItem(
                                0,
                                channel.Value.Id,
                                x.Id,
                                x.Title,
                                Some(x.Link),
                                Some(x.Description),
                                Some(x.Content),
                                publishingDate,
                                false,
                                false,
                                false,
                                false,
                                categories
                            ))
                        |> Seq.iter (fun x -> channelItems.Create(x) |> ignore)

                        Debug.WriteLine(
                            $"End read url = {channel.Value.Url}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"
                        )

                    else
                        Debug.WriteLine($"Can't load feed from url = {channel.Value.Url}")
                        logger.LogInformation($"Can't load feed from url = {channel.Value.Url}")

                with ex ->
                    Debug.WriteLine($"Url = {channel.Value.Url}")
                    Debug.WriteLine(ex)

                    logger.LogError(ex, $"Url = {channel.Value.Url}, ThreadId = {Thread.CurrentThread.ManagedThreadId}")

                return channel.Value
            }
            |> Async.StartAsTask
