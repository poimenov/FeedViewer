namespace FeedViewer.Application

module ContentPage =
    open System.Linq
    open Microsoft.AspNetCore.Components.Web.Virtualization
    open Microsoft.FluentUI.AspNetCore.Components
    open Fun.Blazor
    open FeedViewer
    open System

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
                        style' "height: 80px;"
                        Orientation Orientation.Horizontal

                        adapt {
                            let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()
                            let h4width = leftPaneWidth - 30

                            let cursorStyle =
                                match store.CurrentChannelId.Value with
                                | ByChannelId _ -> "cursor: pointer"
                                | _ -> ""

                            FluentStack'' {
                                Orientation Orientation.Vertical
                                width $"{leftPaneWidth}px;"

                                FluentLabel'' {
                                    Typo Typography.H4
                                    Color Color.Accent

                                    style'
                                        $"width:{h4width}px;{cursorStyle};white-space: nowrap;overflow: hidden;text-overflow: ellipsis;"

                                    onclick (fun _ ->
                                        openChannelLink (store.CurrentChannelId.Value, dataAccess, linkOpeningService))

                                    $"{title}({count})"
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

                                    a {
                                        class' "channel-item-title"
                                        style' "font-size: 16px;"
                                        title' selItem.Title
                                        href link
                                        onclick "OpenLink()"
                                        selItem.Title
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
                                    let! feedItems, setFeedItems = store.FeedItems.WithSetter()

                                    let items =
                                        match feedItems with
                                        | NotLoadedFeedItemsList -> []
                                        | LoadedFeedItemsList lst -> lst

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

                                                        match item.ThumbnailUrl with
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

                                    let tryGetNextElement (element: ChannelItem, list: ChannelItem list) =
                                        list
                                        |> List.tryFindIndex ((=) element)
                                        |> Option.bind (fun i ->
                                            if i < List.length list - 1 then Some list.[i + 1] else None)

                                    let tryGetPreviousElement (element: ChannelItem, list: ChannelItem list) =
                                        list
                                        |> List.tryFindIndex ((=) element)
                                        |> Option.bind (fun i -> if 0 < i then Some list.[i - 1] else None)

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
                                        style' "height: 100%;overflow: auto;"

                                        FluentFlipper'' {
                                            style' "position: absolute; top: 50%;left:10px;"

                                            Direction FlipperDirection.Previous

                                            onclick (fun _ ->
                                                match store.FeedItems.Value with
                                                | NotLoadedFeedItemsList -> ()
                                                | LoadedFeedItemsList items ->
                                                    match selectedItem with
                                                    | NotSelected -> ()
                                                    | Selected item ->
                                                        match tryGetPreviousElement (item, items) with
                                                        | None -> ()
                                                        | Some next ->
                                                            setSelectedItem (SelectedChannelItem.Selected next))
                                        }

                                        FluentFlipper'' {
                                            style' "position: absolute; top: 50%;right:10px;"

                                            Direction FlipperDirection.Next

                                            onclick (fun _ ->
                                                match store.FeedItems.Value with
                                                | NotLoadedFeedItemsList -> ()
                                                | LoadedFeedItemsList items ->
                                                    match selectedItem with
                                                    | NotSelected -> ()
                                                    | Selected item ->
                                                        match tryGetNextElement (item, items) with
                                                        | None -> ()
                                                        | Some next ->
                                                            setSelectedItem (SelectedChannelItem.Selected next))
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
