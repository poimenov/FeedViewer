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

            let title, items =
                match id with
                | All -> "All", channelItems.GetByRead(false)
                | ReadLater -> "Read Later", channelItems.GetByReadLater(true)
                | Starred -> "Starred", channelItems.GetByFavorite(true)
                | ByGroupId groupId -> groups.GetById(groupId).Value.Name, channelItems.GetByGroupId groupId
                | ByChannelId channelId -> channels.Get(channelId).Value.Title, channelItems.GetByChannelId channelId

            fragment {
                FluentSplitter'' {
                    Orientation Orientation.Horizontal
                    Panel1MinSize "200px"
                    Panel1Size "350px"
                    Panel2MinSize "200px"
                    style' "height: 100%;"

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

                                FluentLabel'' {
                                    Typo Typography.H1
                                    Color Color.Accent
                                    title
                                }

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
                                    FluentStack'' {
                                        Orientation Orientation.Vertical

                                        FluentAnchor'' {
                                            Appearance Appearance.Hypertext
                                            href "#"
                                            class' "channel-items-title"

                                            OnClick(fun _ ->
                                                if selItem.Link.IsSome then
                                                    linkOpeningService.OpenUrl(selItem.Link.Value))

                                            selItem.Title
                                        }

                                        match selItem.Content with
                                        | None ->
                                            match selItem.Description with
                                            | None -> ()
                                            | Some description -> div { childContentRaw (addOpenLink (description)) }
                                        | Some contentHtml -> div { childContentRaw (addOpenLink (contentHtml)) }
                                    }
                            }
                        }
                    )
                }
            })

let getNavLinks (channels: list<Channel>) =
    channels
    |> Seq.map (fun c ->
        FluentNavLink'' {
            Href $"/channel/{c.Id}"
            Tooltip c.Title
            Match NavLinkMatch.Prefix
            Icon(Icons.Regular.Size20.Channel())

            c.Title
        })

let navmenus =
    html.injectWithNoKey (fun (store: IShareStore, groups: IChannelGroups, channels: IChannels) ->
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
