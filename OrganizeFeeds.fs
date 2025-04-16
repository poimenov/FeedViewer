namespace FeedViewer.Application

module OrganizeFeeds =
    open System
    open System.Linq
    open Microsoft.FluentUI.AspNetCore.Components
    open Fun.Blazor
    open FeedViewer
    open Navmenu

    let main =
        html.inject
            (fun
                (store: IShareStore,
                 hook: IComponentHook,
                 dataAccess: IDataAccess,
                 reader: IChannelReader,
                 dialogs: IDialogService) ->
                hook.AddInitializedTask(fun () ->
                    task {
                        store.FeedGroups.Publish(
                            FeedGroups.LoadedChannelGroupList(dataAccess.ChannelsGroups.GetAll())
                        )

                        store.FeedChannels.Publish(
                            FeedChannels.LoadedChannelsList(dataAccess.Channels.GetByGroupId(None))
                        )
                    })

                fragment {
                    h3 { "Organize Feeds" }

                    adapt {
                        let! feedGroups, setFeedGroups = store.FeedGroups.WithSetter()
                        let! selectedFeedGroup, setSelectedFeedGroup = store.SelectedFeedGroup.WithSetter()
                        let! feedChannels, setFeedChannels = store.FeedChannels.WithSetter()

                        let updateNavigation () =
                            store.IsMenuOpen.Publish(false)
                            store.IsMenuOpen.Publish(true)

                        let disabledFeedGroupButtons =
                            match selectedFeedGroup with
                            | SelectedFeedGroup.SelectedGroup sg -> sg.Id = 0
                            | SelectedFeedGroup.NotSelectedGroup -> true

                        let folders =
                            match feedGroups with
                            | FeedGroups.LoadedChannelGroupList foldersList -> ChannelGroup(0, "") :: foldersList
                            | _ -> []

                        let feeds =
                            match feedChannels with
                            | FeedChannels.LoadedChannelsList feedsList -> feedsList.AsQueryable<Channel>()
                            | _ -> [].AsQueryable<Channel>()


                        FluentStack'' {
                            style' "margin-right: 10px;"

                            FluentSelect'' {
                                Width "200px"
                                Height "300px"
                                Label "Folders"
                                type' typeof<ChannelGroup>
                                Items folders
                                OptionValue(fun (x: ChannelGroup) -> Convert.ToString x.Id)
                                OptionText(fun (x: ChannelGroup) -> x.Name)

                                SelectedOptionChanged(fun (x: ChannelGroup) ->
                                    let id =
                                        match x.Id with
                                        | 0 -> None
                                        | _ -> Some x.Id

                                    store.FeedChannels.Publish(
                                        FeedChannels.LoadedChannelsList(dataAccess.Channels.GetByGroupId(id))
                                    )

                                    store.SelectedFeedGroup.Publish(SelectedFeedGroup.SelectedGroup x))
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.FolderAdd())
                                title' "Add new folder"

                                OnClick(fun _ ->
                                    task {
                                        let dialogParams = DialogParameters()
                                        dialogParams.Title <- "Add New Folder"
                                        dialogParams.Height <- "250px"
                                        dialogParams.PreventDismissOnOverlayClick <- true
                                        dialogParams.PreventScroll <- true

                                        let! dialog =
                                            dialogs.ShowDialogAsync<FeedGroupDialog>(
                                                ChannelGroup(0, ""),
                                                dialogParams
                                            )
                                            |> Async.AwaitTask

                                        let! result = dialog.Result |> Async.AwaitTask

                                        if (not result.Cancelled) && (not (isNull result.Data)) then
                                            let group = result.Data :?> ChannelGroup

                                            if not (dataAccess.ChannelsGroups.Exists(group.Name)) then
                                                dataAccess.ChannelsGroups.Create group |> ignore

                                                store.FeedGroups.Publish(
                                                    FeedGroups.LoadedChannelGroupList(
                                                        dataAccess.ChannelsGroups.GetAll()
                                                    )
                                                )

                                                updateNavigation ()
                                    })

                                "Add Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Edit())
                                title' "Edit current folder"
                                Disabled(disabledFeedGroupButtons)

                                OnClick(fun _ ->
                                    task {
                                        let dialogParams = DialogParameters()
                                        dialogParams.Title <- "Edit Folder"
                                        dialogParams.Height <- "250px"
                                        dialogParams.PreventDismissOnOverlayClick <- true
                                        dialogParams.PreventScroll <- true

                                        let group =
                                            match selectedFeedGroup with
                                            | SelectedFeedGroup.SelectedGroup sg ->
                                                (dataAccess.ChannelsGroups.GetById sg.Id).Value
                                            | SelectedFeedGroup.NotSelectedGroup -> ChannelGroup(0, "")

                                        if group.Id <> 0 then
                                            let! dialog =
                                                dialogs.ShowDialogAsync<FeedGroupDialog, ChannelGroup>(
                                                    group,
                                                    dialogParams
                                                )
                                                |> Async.AwaitTask

                                            let! result = dialog.Result |> Async.AwaitTask

                                            if (not result.Cancelled) && (not (isNull result.Data)) then
                                                let group = result.Data :?> ChannelGroup

                                                if not (dataAccess.ChannelsGroups.Exists(group.Name)) then
                                                    dataAccess.ChannelsGroups.Update group |> ignore

                                                    store.FeedGroups.Publish(
                                                        FeedGroups.LoadedChannelGroupList(
                                                            dataAccess.ChannelsGroups.GetAll()
                                                        )
                                                    )

                                                    updateNavigation ()
                                    })

                                "Edit Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Delete())
                                title' "Remove current folder"
                                Disabled(disabledFeedGroupButtons)

                                OnClick(fun _ ->
                                    task {
                                        let group =
                                            match selectedFeedGroup with
                                            | SelectedFeedGroup.SelectedGroup sg -> sg
                                            | SelectedFeedGroup.NotSelectedGroup -> ChannelGroup(0, "")

                                        if group.Id <> 0 then
                                            let! dialog =
                                                dialogs.ShowConfirmationAsync(
                                                    $"Folder \"{group.Name}\" will be removed",
                                                    "Are you sure?",
                                                    "Cancel",
                                                    "Remove current group"
                                                )
                                                |> Async.AwaitTask

                                            let! result = dialog.Result |> Async.AwaitTask

                                            if not result.Cancelled then
                                                dataAccess.ChannelsGroups.Delete group.Id |> ignore

                                                store.FeedGroups.Publish(
                                                    FeedGroups.LoadedChannelGroupList(
                                                        dataAccess.ChannelsGroups.GetAll()
                                                    )
                                                )

                                                updateNavigation ()
                                    })

                                "Remove Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Rss())
                                title' "Add feed to current folder"

                                OnClick(fun _ ->
                                    task {
                                        let dialogParams = DialogParameters()
                                        dialogParams.Title <- "Add feed to current folder"
                                        dialogParams.Height <- "250px"
                                        dialogParams.PreventDismissOnOverlayClick <- true
                                        dialogParams.PreventScroll <- true

                                        let groupId =
                                            match selectedFeedGroup with
                                            | SelectedFeedGroup.SelectedGroup sg -> Some sg.Id
                                            | SelectedFeedGroup.NotSelectedGroup -> None

                                        let! dialog =
                                            dialogs.ShowDialogAsync<AddFeedDialog, string>(String.Empty, dialogParams)
                                            |> Async.AwaitTask

                                        let! result = dialog.Result |> Async.AwaitTask

                                        if (not result.Cancelled) && (not (isNull result.Data)) then
                                            let url = result.Data :?> string

                                            if
                                                not (String.IsNullOrWhiteSpace url)
                                                && not (dataAccess.Channels.Exists url)
                                            then
                                                let channelId =
                                                    dataAccess.Channels.Create(
                                                        Channel(0, groupId, String.Empty, None, None, url, None, None)
                                                    )

                                                let! channel =
                                                    reader.ReadChannelAsync(channelId, iconsDirectoryPath)
                                                    |> Async.StartAsTask
                                                    |> Async.AwaitTask

                                                store.FeedChannels.Publish(
                                                    FeedChannels.LoadedChannelsList(
                                                        dataAccess.Channels.GetByGroupId(groupId)
                                                    )
                                                )

                                                updateNavigation ()

                                    })

                                "Add feed"
                            }
                        }

                        FluentDataGrid'' {
                            style' "margin-right: 10px;"
                            Items feeds
                            GenerateHeader GenerateHeaderOption.None

                            TemplateColumn'' {
                                width "50px"

                                ChildContent(fun (c: Channel) ->
                                    img {
                                        src (getIconPath(Some c))
                                        style' "width: 100%; height: 100%;cursor: pointer;"
                                        title' "Open site in browser"

                                        onclick (
                                            "window.openLinkProvider.invokeMethodAsync('OpenLink', '"
                                            + (if c.Link.IsSome then c.Link.Value else c.Url)
                                            + "');"
                                        )
                                    })
                            }

                            TemplateColumn'' {
                                ChildContent(fun (c: Channel) ->
                                    a {
                                        href c.Url
                                        onclick "OpenLink()"
                                        c.Title
                                    })
                            }

                            TemplateColumn'' {
                                width "110px"

                                ChildContent(fun (c: Channel) ->
                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.Edit())

                                        OnClick(fun _ ->
                                            task {
                                                let dialogParams = DialogParameters()
                                                dialogParams.Title <- "Edit Feed Channel"
                                                dialogParams.Height <- "350px"
                                                dialogParams.PreventDismissOnOverlayClick <- true
                                                dialogParams.PreventScroll <- true

                                                let feed = dataAccess.Channels.Get(c.Id).Value
                                                let data = ChannelEdit(feed, folders)

                                                let! dialog =
                                                    dialogs.ShowDialogAsync<EditFeedDialog, ChannelEdit>(
                                                        data,
                                                        dialogParams
                                                    )
                                                    |> Async.AwaitTask

                                                let! result = dialog.Result |> Async.AwaitTask

                                                if (not result.Cancelled) && (not (isNull result.Data)) then
                                                    let resultData = result.Data :?> ChannelEdit
                                                    dataAccess.Channels.Update(resultData.Channel)

                                                    store.FeedChannels.Publish(
                                                        FeedChannels.LoadedChannelsList(
                                                            dataAccess.Channels.GetByGroupId(
                                                                resultData.Channel.GroupId
                                                            )
                                                        )
                                                    )

                                                    updateNavigation ()
                                            })

                                        "Edit"
                                    })
                            }

                            TemplateColumn'' {
                                width "110px"

                                ChildContent(fun (c: Channel) ->
                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.Delete())

                                        OnClick(fun _ ->
                                            task {
                                                let! dialog =
                                                    dialogs.ShowConfirmationAsync(
                                                        $"Feed \"{c.Title}\" will be removed",
                                                        "Are you sure?",
                                                        "Cancel",
                                                        "Remove current group"
                                                    )
                                                    |> Async.AwaitTask

                                                let! result = dialog.Result |> Async.AwaitTask

                                                if not result.Cancelled then
                                                    dataAccess.Channels.Delete c.Id |> ignore

                                                    store.FeedChannels.Publish(
                                                        FeedChannels.LoadedChannelsList(
                                                            dataAccess.Channels.GetByGroupId(c.GroupId)
                                                        )
                                                    )

                                                    updateNavigation ()
                                            })

                                        "Delete"
                                    })
                            }
                        }
                    }
                })
