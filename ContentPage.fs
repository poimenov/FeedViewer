namespace FeedViewer.Application

module ContentPage =
    open System.Linq
    open Microsoft.AspNetCore.Components.Web.Virtualization
    open Microsoft.FluentUI.AspNetCore.Components
    open Fun.Blazor
    open FeedViewer

    let main (id: channelId) =
        html.inject
            (fun
                (store: IShareStore,
                 dataAccess: IDataAccess,
                 reader: IChannelReader,
                 linkOpeningService: ILinkOpeningService) ->

                let load, title, update =
                    match id with
                    | All ->
                        store.FeedItems.Publish(
                            ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByRead(false))
                        ),
                        "All",
                        reader.ReadAllChannelsAsync() |> Async.StartAsTask |> Async.AwaitTask |> ignore
                    | ReadLater ->
                        store.FeedItems.Publish(
                            ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByReadLater(true))
                        ),
                        "Read Later",
                        ignore ()
                    | Starred ->
                        store.FeedItems.Publish(
                            ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByFavorite(true))
                        ),
                        "Starred",
                        ignore ()
                    | ByGroupId groupId ->
                        store.FeedItems.Publish(
                            ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByGroupId(groupId))
                        ),
                        dataAccess.ChannelsGroups.GetById(groupId).Value.Name,
                        reader.ReadGroupAsync(groupId, iconsDirectoryPath)
                        |> Async.StartAsTask
                        |> Async.AwaitTask
                        |> ignore
                    | ByChannelId channelId ->
                        store.FeedItems.Publish(
                            ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByChannelId(channelId))
                        ),
                        dataAccess.Channels.Get(channelId).Value.Title,
                        reader.ReadChannelAsync(channelId, iconsDirectoryPath)
                        |> Async.StartAsTask
                        |> Async.AwaitTask
                        |> ignore

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

                            FluentStack'' {
                                Orientation Orientation.Vertical
                                width $"{leftPaneWidth}px;"

                                FluentLabel'' {
                                    Typo Typography.H4
                                    Color Color.Accent
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
                                                update
                                                load
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
                                    let addOpenLink (txt: string) =
                                        txt.Replace("<a ", "<a onclick='OpenLink()' ")

                                    let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()

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
