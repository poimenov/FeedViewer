[<AutoOpen>]
module FeedViewer.Services

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Xml.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open FSharp.Data
open CodeHollow.FeedReader
open Microsoft.FluentUI.AspNetCore.Components
open Microsoft.AspNetCore.Components
open Microsoft.Extensions.Localization

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
    abstract member GetFeedAsync: string -> Task<Feed option>

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
            async { return! HtmlDocument.AsyncLoad url } |> Async.StartAsTask

        member this.GetFeedAsync(url: string) : Task<Feed option> =
            async {
                let! response = Http.AsyncRequestString(url, headers = [| (USER_AGENT_HEADER_NAME, USER_AGENT) |])

                try
                    let doc = XDocument.Parse response
                    return Some(FeedReader.ReadFromString(doc.ToString()))
                with ex ->
                    Debug.WriteLine($"Error parsing feed from {url}: {ex.Message}")
                    return None
            }
            |> Async.StartAsTask

type IIconDownloader =
    abstract member DownloadIconAsync: string * Uri option * Uri option * string -> Async<unit>
    abstract member GetIconExtension: byte[] -> string option
    abstract member SaveIconAsync: string * string * Uri option * string -> Async<unit>

type IconDownloader(http: IHttpHandler, logger: ILogger<IconDownloader>) =
    interface IIconDownloader with
        member this.DownloadIconAsync
            (iconName: string, imageUri: Uri option, siteUri: Uri option, iconsDirectoryPath: string)
            : Async<unit> =
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

                            let allowedHref (href: string) =
                                let extensions = [| ".png"; ".jpg"; ".jpeg"; ".gif"; ".webp"; ".bmp"; ".ico" |]

                                let hrefFilePath =
                                    if href.Contains("?") then
                                        href.Substring(0, href.IndexOf("?"))
                                    else
                                        href

                                extensions |> Array.exists (fun ext -> hrefFilePath.EndsWith(ext))

                            let links =
                                doc.Descendants [ "link" ]
                                |> Seq.filter (fun x ->
                                    (x.HasAttribute("rel", "icon")
                                     || x.HasAttribute("rel", "shortcut icon")
                                     || x.HasAttribute("rel", "apple-touch-icon"))
                                    && (x.AttributeValue("href") |> allowedHref))
                                |> Seq.toArray

                            if links.Length > 0 then
                                do!
                                    (this :> IIconDownloader)
                                        .SaveIconAsync(
                                            iconName,
                                            links[0].AttributeValue("href"),
                                            Some(uri),
                                            iconsDirectoryPath
                                        )
                        else
                            do!
                                (this :> IIconDownloader)
                                    .SaveIconAsync(iconName, imageUri.Value.ToString(), None, iconsDirectoryPath)

                    | _ -> ()

                with ex ->
                    Debug.WriteLine(ex)

                    logger.LogError(
                        ex,
                        $"imageUri = {imageUri}, siteUri = {siteUri}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"
                    )
            }

        member this.GetIconExtension(fileBytes: byte array) : string option =
            let jpegMagic = [| 0xFFuy; 0xD8uy |]
            let pngMagic = [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |]
            let gifMagic = [| 0x47uy; 0x49uy; 0x46uy; 0x38uy |]
            let icoMagic = [| 0x00uy; 0x00uy; 0x01uy; 0x00uy |]
            let bmpMagic = [| 0x42uy; 0x4Duy |]
            let webpMagic = [| 0x57uy; 0x45uy; 0x42uy; 0x50uy |]

            if fileBytes.Length >= 4 then
                if fileBytes.[0] = jpegMagic.[0] && fileBytes.[1] = jpegMagic.[1] then
                    Some(".jpg")
                elif fileBytes.[0] = pngMagic.[0] && fileBytes.[1] = pngMagic.[1] then
                    Some(".png")
                elif fileBytes.[0] = gifMagic.[0] && fileBytes.[1] = gifMagic.[1] then
                    Some(".gif")
                elif
                    fileBytes.[0] = icoMagic.[0]
                    && fileBytes.[1] = icoMagic.[1]
                    && fileBytes.[2] = icoMagic.[2]
                    && fileBytes.[3] = icoMagic.[3]
                then
                    Some(".ico")
                elif fileBytes.[0] = bmpMagic.[0] && fileBytes.[1] = bmpMagic.[1] then
                    Some(".bmp")
                elif
                    fileBytes.[0] = webpMagic.[0]
                    && fileBytes.[1] = webpMagic.[1]
                    && fileBytes.[2] = webpMagic.[2]
                    && fileBytes.[3] = webpMagic.[3]
                then
                    Some(".webp")
                else
                    None
            else
                None

        member this.SaveIconAsync
            (iconName: string, url: string, host: Uri option, iconsDirectoryPath: string)
            : Async<unit> =
            async {
                let uriToDownload =
                    match host with
                    | Some uri -> Uri(uri, url)
                    | None -> Uri(url)

                try
                    let! data = http.GetByteArrayAsync(uriToDownload.AbsoluteUri) |> Async.AwaitTask

                    if data.Length > 0 then
                        let ext = (this :> IIconDownloader).GetIconExtension data

                        if ext.IsSome then
                            let fileName = $"{iconName}{ext.Value}"
                            let filePath = Path.Combine(iconsDirectoryPath, fileName)
                            File.WriteAllBytes(filePath, data)
                with ex ->
                    Debug.WriteLine(ex)

                    logger.LogError(
                        ex,
                        $"uriToDownload = {uriToDownload.AbsoluteUri}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"
                    )
            }

type IChannelReader =
    abstract member ReadChannelAsync: int * string -> Async<Channel>
    abstract member ReadGroupAsync: int * string -> Async<Channel array>
    abstract member ReadAllChannelsAsync: unit -> Async<Channel array>

type ChannelReader
    (
        http: IHttpHandler,
        iconDownloader: IIconDownloader,
        channels: IChannels,
        channelItems: IChannelItems,
        logger: ILogger<ChannelReader>
    ) =
    let locker = obj ()

    interface IChannelReader with
        member this.ReadChannelAsync(channelId: int, iconsDirectoryPath: string) : Async<Channel> =
            async {
                let _channel = channels.Get channelId

                if _channel.IsNone then
                    return failwith "Channel not found"

                let channel = _channel.Value

                let toStringOption (s: string) =
                    if String.IsNullOrWhiteSpace s then None else Some s

                try
                    Debug.WriteLine $"Start read url = {channel.Url}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"

                    let! _feed =
                        async {
                            let! result = http.GetFeedAsync channel.Url |> Async.AwaitTask

                            match result with
                            | Some _ -> return result
                            | None ->
                                let link =
                                    if channel.Link.IsSome then
                                        channel.Link.Value
                                    else
                                        channel.Url

                                try
                                    let! feedLinks = http.GetFeedUrlsFromUrlAsync link |> Async.AwaitTask

                                    if feedLinks.Length > 0 then
                                        let! feedResult = http.GetFeedAsync(feedLinks[0]) |> Async.AwaitTask

                                        match feedResult with
                                        | Some _ ->
                                            channel.Url <- feedLinks[0]
                                            return feedResult
                                        | None -> return None
                                    else
                                        return None
                                with ex1 ->
                                    Debug.WriteLine $"Can't load url = {channel.Url}"
                                    Debug.WriteLine $"Exception message: {ex1.Message}"
                                    return None
                        }

                    if _feed.IsSome then
                        let feed = _feed.Value

                        let imageUrl =
                            if String.IsNullOrEmpty feed.ImageUrl then
                                None
                            else
                                Some(Uri feed.ImageUrl)

                        let siteLink =
                            let sLink =
                                if channel.Link.IsSome then
                                    channel.Link.Value
                                else
                                    channel.Url

                            Uri(sLink).GetLeftPart UriPartial.Authority

                        let siteUri =
                            if String.IsNullOrEmpty siteLink then
                                None
                            else
                                Some(Uri siteLink)

                        do!
                            iconDownloader.DownloadIconAsync(
                                Uri(channel.Url).Host,
                                imageUrl,
                                siteUri,
                                iconsDirectoryPath
                            )

                        lock locker (fun () ->
                            if not (String.IsNullOrEmpty feed.Title) then
                                channel.Title <-
                                    if
                                        Uri(channel.Url).Host = channel.Title || String.IsNullOrEmpty(channel.Title)
                                    then
                                        feed.Title
                                    else
                                        channel.Title

                                channel.Link <-
                                    if String.IsNullOrWhiteSpace feed.Link then
                                        Some siteLink
                                    else
                                        Some feed.Link

                                channel.Description <- toStringOption feed.Description
                                channel.ImageUrl <- toStringOption feed.ImageUrl
                                channel.Language <- toStringOption feed.Language
                                channels.Update(channel) |> ignore

                                feed.Items
                                |> Seq.map (fun x ->
                                    ChannelItem(
                                        0,
                                        channel.Id,
                                        x.Id,
                                        x.Title,
                                        toStringOption x.Link,
                                        x.GetThumbnailUrl(),
                                        toStringOption x.Description,
                                        toStringOption (x.GetContent()),
                                        x.GetPublishingDate(),
                                        false,
                                        false,
                                        false,
                                        false,
                                        x.GetCategories()
                                    ))

                                |> Seq.toList
                                |> List.iter (fun item -> channelItems.Create(item) |> ignore))

                        Debug.WriteLine
                            $"End read url = {channel.Url}, ThreadId = {Thread.CurrentThread.ManagedThreadId}"

                    else
                        Debug.WriteLine $"Can't load feed from url = {channel.Url}"
                        logger.LogInformation $"Can't load feed from url = {channel.Url}"

                with ex ->
                    Debug.WriteLine $"Url = {channel.Url}"
                    Debug.WriteLine ex

                    logger.LogError(ex, $"Url = {channel.Url}, ThreadId = {Thread.CurrentThread.ManagedThreadId}")

                return channel
            }

        member this.ReadAllChannelsAsync() : Async<Channel array> =
            async {
                let readChannel (id: int) =
                    (this :> IChannelReader).ReadChannelAsync(id, AppSettings.IconsDirectoryPath)

                return!
                    channels.GetAll()
                    |> Seq.map (fun c -> c.Id)
                    |> Seq.map readChannel
                    |> Async.Parallel
                    |> Async.StartAsTask
                    |> Async.AwaitTask
            }

        member this.ReadGroupAsync(groupId: int, iconsDirectoryPath: string) : Async<Channel array> =
            async {
                let readChannel (id: int) =
                    (this :> IChannelReader).ReadChannelAsync(id, iconsDirectoryPath)

                return!
                    channels.GetByGroupId(Some(groupId))
                    |> Seq.map (fun c -> c.Id)
                    |> Seq.map readChannel
                    |> Async.Parallel
                    |> Async.StartAsTask
                    |> Async.AwaitTask
            }

type IServices =
    abstract member ChannelReader: IChannelReader
    abstract member LinkOpeningService: ILinkOpeningService
    abstract member DialogService: IDialogService
    abstract member Navigation: NavigationManager
    abstract member Localizer: IStringLocalizer<SharedResources>
    abstract member OpenDialogService: IOpenDialogService

type Services
    (
        channelReader: IChannelReader,
        linkOpeningService: ILinkOpeningService,
        dialogService: IDialogService,
        navigation: NavigationManager,
        localizer: IStringLocalizer<SharedResources>,
        openDialogService: IOpenDialogService
    ) =
    interface IServices with
        member this.ChannelReader = channelReader
        member this.LinkOpeningService = linkOpeningService
        member this.DialogService = dialogService
        member this.Navigation: NavigationManager = navigation
        member this.Localizer: IStringLocalizer<SharedResources> = localizer
        member this.OpenDialogService: IOpenDialogService = openDialogService
