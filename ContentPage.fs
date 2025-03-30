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
