namespace FeedViewer.Application

module ContentPage =
    open System.Linq
    open Microsoft.AspNetCore.Components.Web.Virtualization
    open Microsoft.FluentUI.AspNetCore.Components
    open Microsoft.JSInterop
    open Fun.Blazor
    open FeedViewer

    let load (id: ChannelId, store: IShareStore, dataAccess: IDataAccess) =
        match id with
        | All -> store.FeedItems.Publish(ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByRead(false)))
        | ReadLater ->
            store.FeedItems.Publish(ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByReadLater(true)))
        | Starred ->
            store.FeedItems.Publish(ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByFavorite(true)))
        | ByGroupId groupId ->
            store.FeedItems.Publish(ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByGroupId(groupId)))
        | ByChannelId channelId ->
            store.FeedItems.Publish(ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByChannelId(channelId)))

    let update (id: ChannelId, reader: IChannelReader) =
        match id with
        | All -> reader.ReadAllChannelsAsync() |> Async.StartAsTask |> Async.AwaitTask
        | ByGroupId groupId ->
            reader.ReadGroupAsync(groupId, iconsDirectoryPath)
            |> Async.StartAsTask
            |> Async.AwaitTask
        | ByChannelId channelId ->
            let readChannel (id: int) =
                reader.ReadChannelAsync(id, iconsDirectoryPath)

            [ channelId ]
            |> List.map readChannel
            |> Async.Parallel
            |> Async.StartAsTask
            |> Async.AwaitTask
        | ReadLater -> async { return [||] }
        | Starred -> async { return [||] }

    let openChannelLink (id: ChannelId, dataAccess: IDataAccess, linkOpeningService: ILinkOpeningService) =
        match id with
        | ByChannelId channelId ->
            match dataAccess.Channels.Get(channelId) with
            | Some channel ->
                let url =
                    match channel.Link with
                    | Some link -> link
                    | None -> channel.Url

                linkOpeningService.OpenUrl(url) |> ignore
            | _ -> ignore ()
        | _ -> ignore ()

    let main (id: ChannelId) =
        html.inject
            (fun
                (store: IShareStore,
                 dataAccess: IDataAccess,
                 reader: IChannelReader,
                 jsRuntime: IJSRuntime,
                 linkOpeningService: ILinkOpeningService) ->

                store.CurrentChannelId.Publish(id)
                load (id, store, dataAccess)

                match store.FeedItems.Value with
                | ChannelItems.LoadedFeedItemsList items ->
                    if items.Count() > 0 then
                        let first = items |> Seq.head |> SelectedChannelItem.Selected
                        store.SelectedChannelItem.Publish(first)
                    else
                        store.SelectedChannelItem.Publish(SelectedChannelItem.NotSelected)
                | NotLoadedFeedItemsList -> store.SelectedChannelItem.Publish(SelectedChannelItem.NotSelected)

                let title =
                    match id with
                    | All -> "All"
                    | ReadLater -> "Read Later"
                    | Starred -> "Starred"
                    | ByGroupId groupId -> dataAccess.ChannelsGroups.GetById(groupId).Value.Name
                    | ByChannelId channelId -> dataAccess.Channels.Get(channelId).Value.Title

                let count =
                    match store.FeedItems.Value with
                    | ChannelItems.LoadedFeedItemsList items -> items.Count()
                    | _ -> 0

                fragment {
                    FluentStack'' {
                        style' "height: 80px;margin-top: 5px"
                        Orientation Orientation.Horizontal

                        adapt {
                            let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()
                            let h4width = leftPaneWidth - 30

                            let cursorStyle =
                                match store.CurrentChannelId.Value with
                                | ByChannelId _ -> "cursor: pointer"
                                | _ -> ""

                            let getIcon: Icon =
                                match store.CurrentChannelId.Value with
                                | All -> Icons.Regular.Size20.Document()
                                | ReadLater -> Icons.Regular.Size20.Flag()
                                | Starred -> Icons.Regular.Size20.Star()
                                | ByChannelId channelId -> dataAccess.Channels.Get(channelId) |> Navmenu.getChannelIcon
                                | ByGroupId _ -> Icons.Regular.Size20.Folder()

                            FluentStack'' {
                                Orientation Orientation.Vertical
                                width $"{leftPaneWidth}px;"

                                FluentStack'' {
                                    Orientation Orientation.Horizontal

                                    FluentIcon'' { value getIcon }

                                    FluentLabel'' {
                                        Typo Typography.H5
                                        Color Color.Accent

                                        style'
                                            $"width:{h4width}px;{cursorStyle};white-space: nowrap;overflow: hidden;text-overflow: ellipsis;font-weight: bold;"

                                        onclick (fun _ ->
                                            openChannelLink (
                                                store.CurrentChannelId.Value,
                                                dataAccess,
                                                linkOpeningService
                                            ))

                                        $"{title}({count})"
                                    }
                                }

                                FluentStack'' {
                                    Orientation Orientation.Horizontal

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.CheckmarkCircle())
                                        "Mark All Read"
                                    }

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.ArrowSyncCircle())

                                        OnClick(fun _ ->
                                            task {
                                                let! chnls = update (store.CurrentChannelId.Value, reader)
                                                load (store.CurrentChannelId.Value, store, dataAccess)
                                            })

                                        "Synchronize"
                                    }
                                }
                            }
                        }

                        adapt {
                            let! feedItems, setFeedItems = store.FeedItems.WithSetter()
                            let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()
                            let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()

                            div {
                                style' ("width: calc(100% - " + leftPaneWidth.ToString() + "px);overflow: hidden;")

                                match selectedItem with
                                | NotSelected -> ()
                                | SelectedChannelItem.Selected selItem ->
                                    let link =
                                        match selItem.Link with
                                        | Some link -> link
                                        | None -> "#"

                                    let channel = dataAccess.Channels.Get(selItem.ChannelId)

                                    let setRead (chItem: ChannelItem, read: bool) =
                                        let mutable item = chItem
                                        item.IsRead <- read
                                        dataAccess.ChannelItems.SetRead(item.Id, read)
                                        setSelectedItem (SelectedChannelItem.Selected item)

                                    setRead (selItem, true)

                                    a {
                                        class' "channel-item-title"
                                        style' "font-size: 18px;"
                                        title' selItem.Title
                                        href link
                                        onclick "OpenLink()"
                                        selItem.Title
                                    }

                                    FluentStack'' {
                                        FluentIcon'' { value (Navmenu.getChannelIcon channel) }

                                        match channel with
                                        | None -> ""
                                        | Some channel ->
                                            FluentAnchor'' {
                                                Appearance Appearance.Hypertext
                                                style' "font-size: 16px;"
                                                href "#"

                                                OnClick(fun _ ->
                                                    match channel.Link with
                                                    | None -> linkOpeningService.OpenUrl(channel.Url)
                                                    | Some link -> linkOpeningService.OpenUrl(link))

                                                channel.Title
                                            }

                                        match selItem.PublishingDate with
                                        | Some date ->
                                            span {
                                                style' "font-style: italic;font-size: 12px;"
                                                date.ToLongDateString()
                                            }
                                        | None -> ""
                                    }

                                    FluentStack'' {
                                        MyCheckBox.Create(
                                            selItem.IsRead,
                                            "Set As Read",
                                            Icons.Filled.Size16.CheckboxChecked(),
                                            Icons.Regular.Size16.CheckboxUnchecked(),
                                            (fun b -> setRead (selItem, b))
                                        )

                                        MyCheckBox.Create(
                                            selItem.IsReadLater,
                                            "Set Read Later",
                                            Icons.Filled.Size16.Flag(),
                                            Icons.Regular.Size16.Flag(),
                                            (fun b ->
                                                let mutable item = selItem
                                                item.IsReadLater <- b
                                                dataAccess.ChannelItems.SetReadLater(item.Id, b)
                                                setSelectedItem (SelectedChannelItem.Selected item))
                                        )

                                        MyCheckBox.Create(
                                            selItem.IsFavorite,
                                            "Set Favorite",
                                            Icons.Filled.Size16.Star(),
                                            Icons.Regular.Size16.Star(),
                                            (fun b ->
                                                let mutable item = selItem
                                                item.IsFavorite <- b
                                                dataAccess.ChannelItems.SetFavorite(item.Id, b)
                                                setSelectedItem (SelectedChannelItem.Selected item))
                                        )

                                        FluentButton'' {
                                            IconStart(Icons.Regular.Size16.Delete())
                                            style' "height: 20px;"
                                            title' "Delete"

                                            OnClick(fun _ ->
                                                let mutable item = selItem
                                                item.IsDeleted <- true
                                                item.IsRead <- true
                                                dataAccess.ChannelItems.SetDeleted(item.Id, true)

                                                let items =
                                                    match feedItems with
                                                    | NotLoadedFeedItemsList -> []
                                                    | LoadedFeedItemsList lst -> lst

                                                match items |> List.tryFindIndex ((=) selItem) with
                                                | Some index ->
                                                    let updatedItems = items |> List.removeAt index
                                                    setFeedItems (LoadedFeedItemsList updatedItems)

                                                    match updatedItems |> List.tryItem index with
                                                    | Some nextItem ->
                                                        setSelectedItem (SelectedChannelItem.Selected nextItem)
                                                    | None ->
                                                        if updatedItems.Length > 0 && index > 0 then
                                                            match updatedItems |> List.tryItem (index - 1) with
                                                            | Some prevItem ->
                                                                setSelectedItem (
                                                                    SelectedChannelItem.Selected prevItem
                                                                )
                                                            | None -> setSelectedItem NotSelected
                                                        else
                                                            setSelectedItem NotSelected
                                                | None -> setSelectedItem NotSelected

                                            )
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
                        style' "height: calc(100% - 85px);"

                        OnResized(fun args -> store.LeftPaneWidth.Publish(args.Panel1Size))

                        Panel1(
                            div {
                                adapt {
                                    let! feedItems, setFeedItems = store.FeedItems.WithSetter()

                                    let items =
                                        match feedItems with
                                        | NotLoadedFeedItemsList -> []
                                        | LoadedFeedItemsList lst -> lst

                                    let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()

                                    let getSelectedClass (curr: ChannelItem) =
                                        let defaultClassName = "channel-item"
                                        let selectedClassName = "channel-item selected-channel-item"

                                        match selectedItem with
                                        | NotSelected -> defaultClassName
                                        | Selected item ->
                                            if curr.Id = item.Id then
                                                selectedClassName
                                            else
                                                defaultClassName

                                    Virtualize'' {
                                        Items(items.ToList<ChannelItem>())

                                        ItemContent(fun item ->
                                            div {
                                                class' (getSelectedClass item)

                                                onclick (fun _ ->
                                                    let selected = SelectedChannelItem.Selected(item)
                                                    store.SelectedChannelItem.Publish(selected))

                                                div {
                                                    class' "channel-item-title"
                                                    item.Title
                                                }

                                                div {
                                                    class' "channel-item-description"

                                                    match item.ThumbnailUrl with
                                                    | Some url ->
                                                        img {
                                                            src url
                                                            loadingExperimental true
                                                        }
                                                    | None -> ()

                                                    item.DescriptionText()
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

                                    let tryGetNextElement (element: ChannelItem, list: ChannelItem list) =
                                        list
                                        |> List.tryFindIndex ((=) element)
                                        |> Option.bind (fun i ->
                                            if i < List.length list - 1 then Some list.[i + 1] else None)

                                    let tryGetPreviousElement (element: ChannelItem, list: ChannelItem list) =
                                        list
                                        |> List.tryFindIndex ((=) element)
                                        |> Option.bind (fun i -> if 0 < i then Some list.[i - 1] else None)

                                    let moveToNextItem (chItems: Types.ChannelItems, chItem: SelectedChannelItem) =
                                        match chItems with
                                        | NotLoadedFeedItemsList -> ()
                                        | LoadedFeedItemsList items ->
                                            match chItem with
                                            | NotSelected -> ()
                                            | Selected item ->
                                                match tryGetNextElement (item, items) with
                                                | None -> ()
                                                | Some next ->
                                                    setSelectedItem (SelectedChannelItem.Selected next)
                                                    jsRuntime.InvokeAsync("ScrollToSelectedItem") |> ignore

                                    let moveToPreviousItem (chItems: Types.ChannelItems, chItem: SelectedChannelItem) =
                                        match chItems with
                                        | NotLoadedFeedItemsList -> ()
                                        | LoadedFeedItemsList items ->
                                            match chItem with
                                            | NotSelected -> ()
                                            | Selected item ->
                                                match tryGetPreviousElement (item, items) with
                                                | None -> ()
                                                | Some next ->
                                                    setSelectedItem (SelectedChannelItem.Selected next)
                                                    jsRuntime.InvokeAsync("ScrollToSelectedItem") |> ignore

                                    let isDiasbledNextButton
                                        (chItems: Types.ChannelItems, chItem: SelectedChannelItem)
                                        =
                                        match chItems with
                                        | NotLoadedFeedItemsList -> true
                                        | LoadedFeedItemsList items ->
                                            match chItem with
                                            | NotSelected -> true
                                            | Selected item ->
                                                match tryGetNextElement (item, items) with
                                                | None -> true
                                                | Some next -> false

                                    let isDisabledPreviousButton
                                        (chItems: Types.ChannelItems, chItem: SelectedChannelItem)
                                        =
                                        match chItems with
                                        | NotLoadedFeedItemsList -> true
                                        | LoadedFeedItemsList items ->
                                            match chItem with
                                            | NotSelected -> true
                                            | Selected item ->
                                                match tryGetPreviousElement (item, items) with
                                                | None -> true
                                                | Some next -> false

                                    let contentHtml =
                                        let txt =
                                            match selectedItem with
                                            | NotSelected -> ""
                                            | SelectedChannelItem.Selected selItem ->
                                                match selItem.Content with
                                                | None ->
                                                    match selItem.Description with
                                                    | None -> ""
                                                    | Some description -> description
                                                | Some htmlText -> htmlText

                                        txt.Replace("<a ", "<a onclick='OpenLink()' ")

                                    FluentCard'' {
                                        id "contentCard"
                                        style' "height: 100%;overflow: auto;"

                                        FluentFlipper'' {
                                            style' "position: absolute; top: 50%;left:10px;"
                                            Direction FlipperDirection.Previous
                                            disabled (isDisabledPreviousButton (store.FeedItems.Value, selectedItem))
                                            onclick (fun _ -> moveToPreviousItem (store.FeedItems.Value, selectedItem))
                                        }

                                        FluentFlipper'' {
                                            style' "position: absolute; top: 50%;right:10px;"
                                            Direction FlipperDirection.Next
                                            disabled (isDiasbledNextButton (store.FeedItems.Value, selectedItem))
                                            onclick (fun _ -> moveToNextItem (store.FeedItems.Value, selectedItem))
                                        }

                                        div {
                                            style'
                                                "height: 100%;overflow: auto;padding: 0 10px 0 15px;margin-right: 15px;"

                                            childContentRaw (contentHtml)
                                        }
                                    }
                                }
                            }
                        )
                    }
                })
