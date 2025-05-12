namespace FeedViewer.Application

module ContentPage =
    open System.Linq
    open Microsoft.AspNetCore.Components.Web.Virtualization
    open Microsoft.FluentUI.AspNetCore.Components
    open Microsoft.JSInterop
    open FSharp.Data.Adaptive
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
        | ByCategoryId categoryId ->
            store.FeedItems.Publish(
                ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetByCategoryId(categoryId))
            )
        | BySearchString searchString ->
            store.FeedItems.Publish(
                ChannelItems.LoadedFeedItemsList(dataAccess.ChannelItems.GetBySearchString(searchString))
            )

    let update (id: ChannelId, reader: IChannelReader) =
        match id with
        | All -> reader.ReadAllChannelsAsync() |> Async.StartAsTask |> Async.AwaitTask
        | ByGroupId groupId ->
            reader.ReadGroupAsync(groupId, AppSettings.IconsDirectoryPath)
            |> Async.StartAsTask
            |> Async.AwaitTask
        | ByChannelId channelId ->
            let readChannel (id: int) =
                reader.ReadChannelAsync(id, AppSettings.IconsDirectoryPath)

            [ channelId ]
            |> List.map readChannel
            |> Async.Parallel
            |> Async.StartAsTask
            |> Async.AwaitTask
        | ByCategoryId categoryId -> async { return [||] }
        | BySearchString searchString -> async { return [||] }
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

    let getUnreadCount (id: ChannelId, dataAccess: IDataAccess) =
        match id with
        | All -> dataAccess.Channels.GetAllUnreadCount()
        | ReadLater -> dataAccess.Channels.GetReadLaterCount()
        | Starred -> dataAccess.Channels.GetStarredCount()
        | ByGroupId groupId -> dataAccess.ChannelsGroups.GetGroupUnreadCount(groupId)
        | ByChannelId channelId -> dataAccess.Channels.GetChannelUnreadCount(channelId)
        | ByCategoryId categoryId -> dataAccess.Categories.GetByCategoryCount(categoryId)
        | BySearchString searchString -> dataAccess.Channels.GetSearchCount(searchString)

    let main (id: ChannelId) =
        html.inject (fun (store: IShareStore, dataAccess: IDataAccess, services: IServices, jsRuntime: IJSRuntime) ->

            store.CurrentChannelId.Publish(id)
            load (id, store, dataAccess)

            match id with
            | BySearchString searchString -> ()
            | _ ->
                store.SearchString.Publish("")
                store.SearchEnabled.Publish(false)

            match store.FeedItems.Value with
            | ChannelItems.LoadedFeedItemsList items ->
                if items.Count() > 0 then
                    let first = items |> Seq.head |> SelectedChannelItem.Selected
                    store.SelectedChannelItem.Publish(first)
                else
                    store.SelectedChannelItem.Publish(SelectedChannelItem.NotSelected)
            | NotLoadedFeedItemsList -> store.SelectedChannelItem.Publish(SelectedChannelItem.NotSelected)

            let title: string =
                match id with
                | All -> string (services.Localizer["All"])
                | ReadLater -> string (services.Localizer["ReadLater"])
                | Starred -> string (services.Localizer["Favorites"])
                | ByGroupId groupId -> dataAccess.ChannelsGroups.GetById(groupId).Value.Name
                | ByChannelId channelId -> dataAccess.Channels.Get(channelId).Value.Title
                | ByCategoryId categoryId -> dataAccess.Categories.Get(categoryId).Value.Name
                | BySearchString _ -> string (services.Localizer["SearchResults"])

            store.UnreadCount.Publish(getUnreadCount (id, dataAccess))

            fragment {
                FluentStack'' {
                    style' "height: 80px;margin-top: 5px"
                    Orientation Orientation.Horizontal

                    adapt {
                        let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()
                        let! unreadCount, setUnreadCount = store.UnreadCount.WithSetter()
                        let! searchString, setSearchString = store.SearchString.WithSetter()
                        let! synchronizeButtonDisabled, setSynchronizeButtonDisabled = cval(false).WithSetter()
                        let! searchEnabled, setSearchEnabled = store.SearchEnabled.WithSetter()
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
                            | ByChannelId channelId -> dataAccess.Channels.Get(channelId) |> Navmenu.getChannelIcon20
                            | ByGroupId _ -> Icons.Regular.Size20.Folder()
                            | ByCategoryId _ -> Icons.Regular.Size20.Bookmark()
                            | BySearchString _ -> Icons.Regular.Size20.Search()

                        let searchStringChanged (str: string) =
                            setSearchString str
                            setSearchEnabled (str.Trim().Length > 2 && str.Trim().Length < 36)

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
                                            services.LinkOpeningService
                                        ))

                                    $"{title} ({unreadCount})"
                                }
                            }

                            FluentStack'' {
                                Orientation Orientation.Horizontal
                                HorizontalGap 2

                                FluentTextField'' {
                                    placeholder (string (services.Localizer["Search"]))
                                    style' "min-width: 150px;width: 100%;"
                                    minlength 3
                                    maxlength 35

                                    onkeydown (fun e ->
                                        if e.Key = "Enter" && searchEnabled then
                                            services.Navigation.NavigateTo($"/search/{searchString}"))

                                    Immediate true
                                    Value searchString
                                    ValueChanged(fun s -> searchStringChanged s)
                                }

                                FluentButton'' {
                                    IconStart(Icons.Regular.Size20.Search())
                                    disabled (not searchEnabled)
                                    Title(string (services.Localizer["Search"]))
                                    OnClick(fun _ -> services.Navigation.NavigateTo($"/search/{searchString}"))
                                }

                                FluentButton'' {
                                    IconStart(Icons.Regular.Size20.CheckmarkCircle())
                                    Title(string (services.Localizer["MarkAllRead"]))

                                    OnClick(fun _ ->
                                        match store.CurrentChannelId.Value with
                                        | ByChannelId channelId ->
                                            dataAccess.ChannelItems.SetReadByChannelId(channelId, true)
                                        | ByGroupId groupId -> dataAccess.ChannelItems.SetReadByGroupId(groupId, true)
                                        | All -> dataAccess.ChannelItems.SetReadAll(true)
                                        | _ -> ()

                                        load (store.CurrentChannelId.Value, store, dataAccess))
                                }

                                FluentButton'' {
                                    IconStart(Icons.Regular.Size20.ArrowSyncCircle())
                                    Title(string (services.Localizer["Synchronize"]))
                                    disabled synchronizeButtonDisabled

                                    OnClick(fun _ ->
                                        task {
                                            setSynchronizeButtonDisabled true
                                            let! chnls = update (store.CurrentChannelId.Value, services.ChannelReader)
                                            load (store.CurrentChannelId.Value, store, dataAccess)
                                            setSynchronizeButtonDisabled false
                                        })
                                }
                            }
                        }
                    }

                    adapt {
                        let! feedItems, setFeedItems = store.FeedItems.WithSetter()
                        let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()
                        let! leftPaneWidth, setLeftPaneWidth = store.LeftPaneWidth.WithSetter()
                        let! currentChannelId, setCurrentChannelId = store.CurrentChannelId.WithSetter()

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
                                    chItem.IsRead <- read
                                    dataAccess.ChannelItems.SetRead(chItem.Id, read)
                                    store.CurrentIsRead.Publish(read)
                                    store.UnreadCount.Publish(getUnreadCount (currentChannelId, dataAccess))
                                    setSelectedItem (SelectedChannelItem.Selected chItem)

                                setRead (selItem, true)
                                store.CurrentIsReadLater.Publish(selItem.IsReadLater)
                                store.CurrentIsFavorite.Publish(selItem.IsFavorite)

                                a {
                                    class' "channel-item-title"
                                    style' "font-size: 18px;"
                                    title' selItem.Title
                                    href link
                                    onclick "OpenLink()"
                                    selItem.Title
                                }

                                FluentStack'' {
                                    FluentIcon'' { value (Navmenu.getChannelIcon20 channel) }

                                    match channel with
                                    | None -> ""
                                    | Some channel ->
                                        span {
                                            class' "channel-name"

                                            FluentAnchor'' {
                                                Appearance Appearance.Hypertext
                                                style' "font-size: 16px;"
                                                class' "channel-name"
                                                href "#"

                                                title' (
                                                    match channel.Description with
                                                    | Some description -> description
                                                    | None -> channel.Title
                                                )

                                                OnClick(fun _ ->
                                                    match channel.Link with
                                                    | None -> services.LinkOpeningService.OpenUrl(channel.Url)
                                                    | Some link -> services.LinkOpeningService.OpenUrl(link))

                                                channel.Title
                                            }
                                        }

                                    match selItem.PublishingDate with
                                    | Some date ->
                                        span {
                                            style' "font-style: italic;font-size: 12px;white-space: nowrap;"
                                            date.ToLongDateString()
                                        }
                                    | None -> ""
                                }

                                FluentStack'' {
                                    Orientation Orientation.Horizontal
                                    HorizontalGap 2

                                    MyCheckBox.Create(
                                        selItem.IsRead,
                                        string (services.Localizer["SetAsRead"]),
                                        Icons.Filled.Size16.CheckboxChecked(),
                                        Icons.Regular.Size16.CheckboxUnchecked(),
                                        (fun b -> setRead (selItem, b))
                                    )

                                    MyCheckBox.Create(
                                        selItem.IsReadLater,
                                        string (services.Localizer["SetReadLater"]),
                                        Icons.Filled.Size16.Flag(),
                                        Icons.Regular.Size16.Flag(),
                                        (fun b ->
                                            let mutable item = selItem
                                            item.IsReadLater <- b
                                            dataAccess.ChannelItems.SetReadLater(item.Id, b)
                                            store.CurrentIsReadLater.Publish(b)
                                            setSelectedItem (SelectedChannelItem.Selected item))
                                    )

                                    MyCheckBox.Create(
                                        selItem.IsFavorite,
                                        string (services.Localizer["SetFavorite"]),
                                        Icons.Filled.Size16.Star(),
                                        Icons.Regular.Size16.Star(),
                                        (fun b ->
                                            let mutable item = selItem
                                            item.IsFavorite <- b
                                            dataAccess.ChannelItems.SetFavorite(item.Id, b)
                                            store.CurrentIsFavorite.Publish(b)
                                            setSelectedItem (SelectedChannelItem.Selected item))
                                    )

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size16.Delete())
                                        style' "height: 20px;"
                                        title' (string (services.Localizer["Delete"]))

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
                                                            setSelectedItem (SelectedChannelItem.Selected prevItem)
                                                        | None -> setSelectedItem NotSelected
                                                    else
                                                        setSelectedItem NotSelected
                                            | None -> setSelectedItem NotSelected

                                        )
                                    }

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size16.Bookmark())
                                        style' "height: 20px;"
                                        title' (string (services.Localizer["Categories"]))

                                        OnClick(fun _ ->
                                            task {
                                                let data = dataAccess.Categories.GetByChannelItemId selItem.Id

                                                let parameters = DialogParameters<list<Category>>()
                                                parameters.Content <- data
                                                parameters.Title <- string (services.Localizer["Categories"])
                                                parameters.Alignment <- HorizontalAlignment.Right
                                                parameters.Width <- "260px"
                                                parameters.Modal <- true
                                                parameters.PrimaryAction <- null
                                                parameters.SecondaryAction <- null

                                                let! dialog =
                                                    services.DialogService.ShowPanelAsync<CategoriesPanel>(
                                                        data,
                                                        parameters
                                                    )
                                                    |> Async.AwaitTask

                                                ()
                                            })
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
                                let! currentIsRead, setCurrentIsRead = store.CurrentIsRead.WithSetter()

                                let! currentIsReadLater, setCurrentIsReadLater = store.CurrentIsReadLater.WithSetter()

                                let! currentIsFavorite, setCurrentIsFavorite = store.CurrentIsFavorite.WithSetter()

                                let items =
                                    match feedItems with
                                    | NotLoadedFeedItemsList -> []
                                    | LoadedFeedItemsList lst -> lst

                                let! selectedItem, setSelectedItem = store.SelectedChannelItem.WithSetter()

                                let channel (curr: ChannelItem) = dataAccess.Channels.Get(curr.ChannelId)

                                let channelName (curr: ChannelItem) =
                                    match channel curr with
                                    | Some c -> c.Title
                                    | None -> ""

                                let isCurrent (curr: ChannelItem) =
                                    match selectedItem with
                                    | NotSelected -> false
                                    | Selected item -> curr.Id = item.Id

                                let getSelectedClass (curr: ChannelItem) =
                                    if isCurrent curr then
                                        "channel-item selected-channel-item"
                                    else
                                        "channel-item"

                                let getIsCheckedIcon (item: ChannelItem) : Icon =
                                    let isRead = if isCurrent item then currentIsRead else item.IsRead

                                    if isRead then
                                        Icons.Filled.Size16.CheckboxChecked()
                                    else
                                        Icons.Filled.Size16.CheckboxUnchecked()

                                let getIsReadLaterIcon (item: ChannelItem) : Icon =
                                    let isReadLater =
                                        if isCurrent item then
                                            currentIsReadLater
                                        else
                                            item.IsReadLater

                                    if isReadLater then
                                        Icons.Filled.Size16.Flag()
                                    else
                                        Icons.Regular.Size16.Flag()

                                let getIsFavoriteIcon (item: ChannelItem) : Icon =
                                    let isFavorite =
                                        if isCurrent item then
                                            currentIsFavorite
                                        else
                                            item.IsFavorite

                                    if isFavorite then
                                        Icons.Filled.Size16.Star()
                                    else
                                        Icons.Regular.Size16.Star()

                                Virtualize'' {
                                    Items(items.ToList<ChannelItem>())

                                    ItemContent(fun item ->
                                        div {
                                            class' (getSelectedClass item)

                                            style' (
                                                if item.IsRead then
                                                    "color: var(--neutral-foreground-hover);"
                                                else
                                                    "color: var(--neutral-foreground-rest);"
                                            )

                                            onclick (fun _ ->
                                                let selected = SelectedChannelItem.Selected(item)
                                                store.SelectedChannelItem.Publish(selected))

                                            div {
                                                class' "channel-item-title"
                                                title' item.Title
                                                item.Title
                                            }

                                            FluentStack'' {
                                                Orientation Orientation.Horizontal
                                                HorizontalGap 2

                                                FluentIcon'' {
                                                    value (Navmenu.getChannelIcon (channel item, IconSize.Size16))
                                                }

                                                span {
                                                    class' "channel-name"

                                                    channelName item
                                                }

                                                FluentSpacer''

                                                match item.PublishingDate with
                                                | Some date ->
                                                    span {
                                                        style' "font-style: italic;font-size: 12px;"
                                                        date.ToShortDateString()
                                                    }
                                                | None -> ""

                                                FluentIcon'' { Value(getIsCheckedIcon item) }
                                                FluentIcon'' { Value(getIsReadLaterIcon item) }
                                                FluentIcon'' { Value(getIsFavoriteIcon item) }
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
                                            string (services.Localizer["NoItems"])
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

                                let isDiasbledNextButton (chItems: Types.ChannelItems, chItem: SelectedChannelItem) =
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
                                        style' "height: 100%;overflow: auto;padding: 0 10px 0 15px;margin-right: 15px;"

                                        childContentRaw (contentHtml)
                                    }
                                }
                            }
                        }
                    )
                }
            })
