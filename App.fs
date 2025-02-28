[<AutoOpen>]
module FeedViewer.App

open System
open System.Linq
open Microsoft.AspNetCore.Components.Web
open Microsoft.AspNetCore.Components.Routing
open Microsoft.FluentUI.AspNetCore.Components
open FSharp.Data
open Fun.Blazor
open Fun.Blazor.Router
open System.IO
open Microsoft.AspNetCore.Components.Web.Virtualization
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop

type public OpenLinkProvider(los: ILinkOpeningService) =
    [<JSInvokable>]
    member public this.OpenLink(link: string) = los.OpenUrl link

type channelId =
    | All
    | ReadLater
    | Starred
    | ByGroupId of int
    | ByChannelId of int

type SelectedChannelItem =
    | NotSelected
    | Selected of ChannelItem

type IShareStore with
    member store.Count = store.CreateCVal(nameof store.Count, 0)
    member store.IsMenuOpen = store.CreateCVal(nameof store.IsMenuOpen, true)
    member store.IsSettingsOpen = store.CreateCVal(nameof store.IsSettingsOpen, false)
    member store.Theme = store.CreateCVal(nameof store.Theme, DesignThemeModes.Light)
    member store.LeftPaneWidth = store.CreateCVal(nameof store.LeftPaneWidth, 350)

    member store.SelectedChannelItem =
        store.CreateCVal(nameof store.SelectedChannelItem, SelectedChannelItem.NotSelected)

let appHeader =
    html.inject (fun (store: IShareStore, openService: IOpenDialogService, exportImport: IExportImportService) ->
        FluentHeader'' {
            FluentStack'' {
                Orientation Orientation.Horizontal

                img {
                    src "favicon.ico"
                    style { height "28px" }
                }

                FluentLabel'' {
                    Typo Typography.H2
                    Color Color.Fill
                    "FeedViewer"
                }

                FluentSpacer''

                adapt {
                    let! isOpen = store.IsSettingsOpen.WithSetter()

                    FluentDesignTheme'' {
                        StorageName "theme"
                        Mode store.Theme.Value

                        OnLoaded(fun args ->
                            if args.IsDark then
                                store.Theme.Publish(DesignThemeModes.Dark))
                    }

                    FluentButton'' {
                        Id "SettingsMenuButton"
                        Appearance Appearance.Accent
                        IconStart(Icons.Regular.Size20.Settings())
                        OnClick(fun _ -> store.IsSettingsOpen.Publish(not))
                    }

                    FluentMenu'' {
                        Anchor "SettingsMenuButton"
                        Open' isOpen
                        UseMenuService false

                        FluentMenuItem'' {
                            OnClick(fun _ ->
                                let folder = openService.OpenFolder(title = "Select folder", multiSelect = false)

                                if folder.Any() then
                                    Path.Combine(folder.First(), "FeedViewer.opml") |> exportImport.Export)

                            "Export"

                            span {
                                slot' "start"

                                FluentIcon'' {
                                    slot' "start"
                                    color Color.Neutral
                                    Value(Icons.Regular.Size20.ArrowExport())
                                }
                            }
                        }

                        FluentMenuItem'' {
                            OnClick(fun _ ->
                                let file =
                                    openService.OpenFile(
                                        title = "Select opml file",
                                        multiSelect = false,
                                        filters = [| ("Opml file", [| "opml" |]) |]
                                    )

                                if file.Any() then
                                    file.First() |> exportImport.Import)

                            "Import"

                            span {
                                slot' "start"

                                FluentIcon'' {
                                    slot' "start"
                                    color Color.Neutral
                                    Value(Icons.Regular.Size20.ArrowImport())
                                }
                            }
                        }

                        FluentMenuItem'' {
                            OnClick(fun _ ->
                                store.Theme.Publish(
                                    if store.Theme.Value = DesignThemeModes.Dark then
                                        DesignThemeModes.Light
                                    else
                                        DesignThemeModes.Dark
                                ))

                            if store.Theme.Value = DesignThemeModes.Dark then
                                "Switch to Light Mode"
                            else
                                "Switch to Dark Mode"

                            span {
                                slot' "start"

                                FluentIcon'' {
                                    slot' "start"
                                    color Color.Neutral
                                    Value(Icons.Regular.Size20.DarkTheme())
                                }
                            }
                        }
                    }
                }
            }
        })

let appFooter =
    html.inject (fun (los: ILinkOpeningService, hook: IComponentHook, jsRuntime: IJSRuntime) ->
        hook.AddFirstAfterRenderTask(fun _ ->
            task {
                let losObjRef = DotNetObjectReference.Create(OpenLinkProvider(los))
                jsRuntime.InvokeAsync("SetOpenLinkProvider", losObjRef) |> ignore
            })

        FluentFooter'' {
            a {
                href "https://slaveoftime.github.io/Fun.Blazor.Docs/"
                onclick "OpenLink()"
                "Fun.Blazor"
            }

            FluentSpacer''

            FluentAnchor'' {
                Appearance Appearance.Hypertext
                href "#"
                OnClick(fun _ -> los.OpenUrl "https://www.tryphotino.io")
                "Photino"
            }
        })

let contentPage (id: channelId) =
    html.inject
        (fun
            (store: IShareStore,
             groups: IChannelGroups,
             channels: IChannels,
             channelItems: IChannelItems,
             linkOpeningService: ILinkOpeningService) ->

            let title, items, count =
                match id with
                | All -> "All", channelItems.GetByRead(false), channels.GetAllUnreadCount()
                | ReadLater -> "Read Later", channelItems.GetByReadLater(true), channels.GetReadLaterCount()
                | Starred -> "Starred", channelItems.GetByFavorite(true), channels.GetStarredCount()
                | ByGroupId groupId ->
                    groups.GetById(groupId).Value.Name,
                    channelItems.GetByGroupId groupId,
                    groups.GetGroupUnreadCount groupId
                | ByChannelId channelId ->
                    channels.Get(channelId).Value.Title,
                    channelItems.GetByChannelId channelId,
                    channels.GetChannelUnreadCount channelId

            fragment {
                div {
                    style' "height: 80px;"

                    FluentStack'' {
                        Orientation Orientation.Horizontal

                        adapt {
                            let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()

                            div {
                                style' $"width: {leftPaneWidth}px;"

                                FluentLabel'' {
                                    Typo Typography.H4
                                    Color Color.Accent
                                    $"{title}({count})"
                                }
                            }
                        }

                        adapt {
                            let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()
                            let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()

                            div {
                                style' ("width: calc(100%-" + leftPaneWidth.ToString() + "px) ;overflow: hidden;")

                                match selectedItem with
                                | NotSelected -> ()
                                | SelectedChannelItem.Selected selItem ->
                                    let link =
                                        match selItem.Link with
                                        | Some link -> link
                                        | None -> "#"

                                    div {
                                        a {
                                            style' "font-size: 16px;font-weight: bold;"
                                            href link
                                            onclick "OpenLink()"
                                            selItem.Title
                                        }
                                    }

                                    div {
                                        style' "font-style: italic;font-size: 12px;"

                                        match selItem.PublishingDate with
                                        | Some date -> date.ToLongDateString()
                                        | None -> ""
                                    }
                            }
                        }
                    }
                }

                FluentSplitter'' {
                    Orientation Orientation.Horizontal
                    Panel1MinSize "200px"
                    Panel1Size "350px"
                    Panel2MinSize "200px"
                    style' "height: calc(100% - 80px);"

                    OnResized(fun args -> store.LeftPaneWidth.Publish(args.Panel1Size))

                    Panel1(
                        div {
                            adapt {
                                let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()

                                let getStyle (curr: ChannelItem) =
                                    let defaultStyle = "border: 1px solid var(--neutral-stroke-rest);"
                                    let selectedStyle = "border: 1px solid var(--accent-fill-rest);"

                                    match selectedItem with
                                    | NotSelected -> defaultStyle
                                    | Selected item -> if curr.Id = item.Id then selectedStyle else defaultStyle

                                Virtualize'' {
                                    Items(items.ToList<ChannelItem>())

                                    ItemContent(fun item ->
                                        div {
                                            class' "channel-item"
                                            style' (getStyle item)

                                            a {
                                                onclick (fun _ ->
                                                    let selected = SelectedChannelItem.Selected(item)
                                                    store.SelectedChannelItem.Publish(selected))

                                                div {
                                                    class' "channel-item-title"
                                                    item.Title
                                                }

                                                div {
                                                    class' "channel-item-description"

                                                    match item.DescriptionImgSrc() with
                                                    | Some url ->
                                                        img {
                                                            src url
                                                            loadingExperimental true
                                                        }
                                                    | None -> ()

                                                    item.DescriptionText()
                                                }
                                            }
                                        })

                                    EmptyContent(
                                        div {
                                            style' "font-size: 12px; font-weight: bold;"
                                            "No items"
                                        }
                                    )
                                }
                            }
                        }
                    )

                    Panel2(
                        div {
                            class' "channel-item-content"

                            adapt {
                                let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()

                                let addOpenLink (txt: string) =
                                    txt.Replace("<a ", "<a onclick='OpenLink()' ")

                                match selectedItem with
                                | NotSelected -> ()
                                | SelectedChannelItem.Selected selItem ->
                                    match selItem.Content with
                                    | None ->
                                        match selItem.Description with
                                        | None -> ()
                                        | Some description -> div { childContentRaw (addOpenLink (description)) }
                                    | Some contentHtml -> div { childContentRaw (addOpenLink (contentHtml)) }
                            }
                        }
                    )
                }
            })

let iconsDirectoryPath =
    let assemblyFolderPath =
        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    Path.Combine(Path.Combine(assemblyFolderPath, "wwwroot"), "icons")

let getChannelIcon (channel: Channel) =
    let host = Uri(channel.Url).Host
    let files = Directory.GetFiles(iconsDirectoryPath, $"{host}.*")

    let filePath =
        if files.Length > 0 then
            $"icons/{Path.GetFileName(files.[0])}"
        else
            "icons/rss-button-orange.32.png"

    Icon(String.Empty, IconVariant.Regular, IconSize.Size20, $"<img src=\"{filePath}\"  style=\"width: 100%%;\" />")



let getNavLinks (channels: list<Channel>) =
    channels
    |> Seq.map (fun c ->
        FluentNavLink'' {
            Href $"/channel/{c.Id}"
            Tooltip c.Title
            Match NavLinkMatch.Prefix
            Icon(getChannelIcon c)
            c.Title
        })

let navmenus =
    html.injectWithNoKey
        (fun
            (store: IShareStore,
             hook: IComponentHook,
             groups: IChannelGroups,
             channels: IChannels,
             channelReader: IChannelReader) ->
            // hook.AddInitializedTask(fun () ->
            //     task {
            //         let readChannel (id: int) =
            //             channelReader.ReadChannelAsync(id, iconsDirectoryPath)

            //         channels.GetAll()
            //         |> Seq.map (fun c -> c.Id)
            //         |> Seq.map readChannel
            //         |> Seq.map Async.AwaitTask
            //         |> Async.Parallel
            //         |> Async.StartImmediateAsTask
            //         |> ignore

            //     })

            adaptiview () {
                let! binding = store.IsMenuOpen.WithSetter()

                FluentNavMenu'' {
                    Width 200
                    Collapsible true
                    Expanded' binding

                    FluentNavLink'' {
                        Href "/channel/all"
                        Match NavLinkMatch.All
                        Icon(Icons.Regular.Size20.Document())
                        "All"
                    }

                    FluentNavLink'' {
                        Href "/channel/starred"
                        Match NavLinkMatch.Prefix
                        Icon(Icons.Regular.Size20.Star())
                        "Starred"
                    }

                    FluentNavLink'' {
                        Href "/channel/readlater"
                        Match NavLinkMatch.Prefix
                        Icon(Icons.Regular.Size20.Flag())
                        "Read Later"
                    }

                    yield!
                        groups.GetAll()
                        |> Seq.map (fun g ->
                            FluentNavGroup'' {
                                title' g.Name
                                Tooltip g.Name
                                href $"/group/{g.Id}"
                                Icon(Icons.Regular.Size20.Folder())

                                yield! channels.GetByGroupId(Some(g.Id)) |> getNavLinks
                            })

                    yield! channels.GetByGroupId(None) |> getNavLinks
                }
            })

let routes =
    html.route
        [| routeCi "/channel/all" (contentPage All)
           routeCi "/channel/readlater" (contentPage ReadLater)
           routeCi "/channel/starred" (contentPage Starred)
           routeCif "/channel/%i" (fun x -> contentPage (ByChannelId x))
           routeCif "/group/%i" (fun x -> contentPage (ByGroupId x))
           routeAny (contentPage All) |]

let app =
    ErrorBoundary'' {
        ErrorContent(fun e ->
            FluentLabel'' {
                Color Color.Error
                string e
            })

        FluentToastProvider''
        FluentDesignTheme'' { StorageName "theme" }

        FluentLayout'' {
            appHeader

            FluentStack'' {
                Width "100%"
                class' "main"
                Orientation Orientation.Horizontal
                navmenus

                FluentBodyContent'' {
                    class' "body-content"
                    style { overflowHidden }

                    div {
                        class' "content"
                        routes
                    }
                }
            }

            appFooter
        }
    }
