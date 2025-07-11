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
                 services: IServices,
                 dialogs: IDialogService) ->
                hook.AddInitializedTask(fun () ->
                    task {
                        store.FeedGroups.Publish(LoadedChannelGroupList(dataAccess.ChannelsGroups.GetAll()))

                        store.FeedChannels.Publish(LoadedChannelsList(dataAccess.Channels.GetByGroupId None))
                    })

                fragment {
                    h3 { string (services.Localizer["OrganizeFeeds"]) }

                    adapt {
                        let! feedGroups, setFeedGroups = store.FeedGroups.WithSetter()
                        let! selectedFeedGroup, setSelectedFeedGroup = store.SelectedFeedGroup.WithSetter()
                        let! feedChannels, setFeedChannels = store.FeedChannels.WithSetter()

                        let updateNavigation () =
                            store.IsMenuOpen.Publish false
                            store.IsMenuOpen.Publish true

                        let disabledFeedGroupButtons =
                            match selectedFeedGroup with
                            | SelectedGroup sg -> sg.Id = 0
                            | NotSelectedGroup -> true

                        let folders =
                            match feedGroups with
                            | LoadedChannelGroupList foldersList -> ChannelGroup(0, "") :: foldersList
                            | _ -> []

                        let feeds =
                            match feedChannels with
                            | LoadedChannelsList feedsList -> feedsList.AsQueryable<Channel>()
                            | _ -> [].AsQueryable<Channel>()


                        FluentGrid'' {
                            Spacing(1)
                            AdaptiveRendering true
                            Justify JustifyContent.FlexStart

                            FluentGridItem'' {
                                xs 12

                                FluentSelect'' {
                                    Width "200px"
                                    Height "300px"
                                    Label(string (services.Localizer["Folders"]))
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
                                            LoadedChannelsList(dataAccess.Channels.GetByGroupId(id))
                                        )

                                        store.SelectedFeedGroup.Publish(SelectedGroup x))
                                }
                            }

                            FluentGridItem'' {
                                xs 12

                                FluentStack'' {
                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.FolderAdd())
                                        title' (string (services.Localizer["AddNewFolder"]))

                                        OnClick(fun _ ->
                                            task {
                                                let dialogParams = DialogParameters()
                                                dialogParams.Title <- string (services.Localizer["AddNewFolder"])
                                                dialogParams.Height <- "250px"
                                                dialogParams.PreventDismissOnOverlayClick <- true
                                                dialogParams.PreventScroll <- true

                                                let! dialog =
                                                    dialogs.ShowDialogAsync<FeedGroupDialog, DialogData<ChannelGroup>>(
                                                        DialogData<ChannelGroup>(
                                                            ChannelGroup(0, ""),
                                                            services.Localizer
                                                        ),
                                                        dialogParams
                                                    )
                                                    |> Async.AwaitTask

                                                let! result = dialog.Result |> Async.AwaitTask

                                                if not result.Cancelled && not (isNull result.Data) then
                                                    let data = result.Data :?> DialogData<ChannelGroup>
                                                    let group = data.Data

                                                    if not (dataAccess.ChannelsGroups.Exists group.Name) then
                                                        dataAccess.ChannelsGroups.Create group |> ignore

                                                        store.FeedGroups.Publish(
                                                            LoadedChannelGroupList(dataAccess.ChannelsGroups.GetAll())
                                                        )

                                                        updateNavigation ()
                                            })

                                        string (services.Localizer["AddFolder"])
                                    }

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.Edit())
                                        title' (string (services.Localizer["EditCurrentFolder"]))
                                        Disabled disabledFeedGroupButtons

                                        OnClick(fun _ ->
                                            task {
                                                let dialogParams = DialogParameters()
                                                dialogParams.Title <- string (services.Localizer["EditFolder"])
                                                dialogParams.Height <- "250px"
                                                dialogParams.PreventDismissOnOverlayClick <- true
                                                dialogParams.PreventScroll <- true

                                                let group =
                                                    match selectedFeedGroup with
                                                    | SelectedGroup sg ->
                                                        (dataAccess.ChannelsGroups.GetById sg.Id).Value
                                                    | NotSelectedGroup -> ChannelGroup(0, "")

                                                let data = DialogData<ChannelGroup>(group, services.Localizer)

                                                if group.Id <> 0 then
                                                    let! dialog =
                                                        dialogs.ShowDialogAsync<
                                                            FeedGroupDialog,
                                                            DialogData<ChannelGroup>
                                                         >(
                                                            data,
                                                            dialogParams
                                                        )
                                                        |> Async.AwaitTask

                                                    let! result = dialog.Result |> Async.AwaitTask

                                                    if not result.Cancelled && not (isNull result.Data) then
                                                        let resultData = result.Data :?> DialogData<ChannelGroup>

                                                        let folder = resultData.Data

                                                        if not (dataAccess.ChannelsGroups.Exists(folder.Name)) then
                                                            dataAccess.ChannelsGroups.Update folder |> ignore

                                                            store.FeedGroups.Publish(
                                                                LoadedChannelGroupList(
                                                                    dataAccess.ChannelsGroups.GetAll()
                                                                )
                                                            )

                                                            updateNavigation ()
                                            })

                                        string (services.Localizer["EditFolder"])
                                    }

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.Delete())
                                        title' (string (services.Localizer["RemoveCurrentFolder"]))
                                        Disabled disabledFeedGroupButtons

                                        OnClick(fun _ ->
                                            task {
                                                let group =
                                                    match selectedFeedGroup with
                                                    | SelectedGroup sg -> sg
                                                    | NotSelectedGroup -> ChannelGroup(0, "")

                                                if group.Id <> 0 then
                                                    let! dialog =
                                                        dialogs.ShowConfirmationAsync(
                                                            string (services.Localizer["Folder"])
                                                            + $" \"{group.Name}\" "
                                                            + string (services.Localizer["WillBeRemoved"])
                                                            + ". "
                                                            + string (services.Localizer["AreYouSure"]),
                                                            string (services.Localizer["Delete"]),
                                                            string (services.Localizer["Cancel"]),
                                                            string (services.Localizer["RemoveCurrentFolder"])
                                                        )
                                                        |> Async.AwaitTask

                                                    let! result = dialog.Result |> Async.AwaitTask

                                                    if not result.Cancelled then
                                                        dataAccess.ChannelsGroups.Delete group.Id |> ignore
                                                        setSelectedFeedGroup NotSelectedGroup

                                                        store.FeedGroups.Publish(
                                                            LoadedChannelGroupList(dataAccess.ChannelsGroups.GetAll())
                                                        )

                                                        updateNavigation ()
                                            })

                                        string (services.Localizer["RemoveFolder"])
                                    }

                                    FluentButton'' {
                                        IconStart(Icons.Regular.Size20.Rss())
                                        title' (string (services.Localizer["AddFeedToCurrentFolder"]))

                                        OnClick(fun _ ->
                                            task {
                                                let dialogParams = DialogParameters()

                                                dialogParams.Title <-
                                                    string (services.Localizer["AddFeedToCurrentFolder"])

                                                dialogParams.Height <- "250px"
                                                dialogParams.PreventDismissOnOverlayClick <- true
                                                dialogParams.PreventScroll <- true

                                                let groupId =
                                                    match selectedFeedGroup with
                                                    | SelectedGroup sg -> Some sg.Id
                                                    | NotSelectedGroup -> None


                                                let! dialog =
                                                    dialogs.ShowDialogAsync<AddFeedDialog, DialogData<string>>(
                                                        DialogData(String.Empty, services.Localizer),
                                                        dialogParams
                                                    )
                                                    |> Async.AwaitTask

                                                let! result = dialog.Result |> Async.AwaitTask

                                                if not result.Cancelled && not (isNull result.Data) then
                                                    let data = result.Data :?> DialogData<string>
                                                    let url = data.Data

                                                    if
                                                        not (String.IsNullOrWhiteSpace url)
                                                        && not (dataAccess.Channels.Exists url)
                                                    then
                                                        let channelId =
                                                            dataAccess.Channels.Create(
                                                                Channel(
                                                                    0,
                                                                    groupId,
                                                                    String.Empty,
                                                                    None,
                                                                    None,
                                                                    url,
                                                                    None,
                                                                    None
                                                                )
                                                            )

                                                        let! channel =
                                                            services.ChannelReader.ReadChannelAsync(
                                                                channelId,
                                                                AppSettings.IconsDirectoryPath
                                                            )
                                                            |> Async.StartAsTask
                                                            |> Async.AwaitTask

                                                        store.FeedChannels.Publish(
                                                            LoadedChannelsList(
                                                                dataAccess.Channels.GetByGroupId groupId
                                                            )
                                                        )

                                                        updateNavigation ()
                                            })

                                        string (services.Localizer["AddFeed"])
                                    }
                                }
                            }

                            FluentGridItem'' {
                                xs 12

                                div {
                                    class' "card"

                                    FluentDataGrid'' {
                                        style' "margin-right: 10px;"
                                        Items feeds
                                        GenerateHeader GenerateHeaderOption.None
                                        EmptyContent(string (services.Localizer["NoFeeds"]))

                                        TemplateColumn'' {
                                            width "50px"

                                            ChildContent(fun (c: Channel) ->
                                                img {
                                                    src (getIconPath (Some c))
                                                    style' "width: 100%; height: 100%;cursor: pointer;"
                                                    title' (string (services.Localizer["OpenSiteInBrowser"]))

                                                    onclick (
                                                        "window.openLinkProvider.invokeMethodAsync('OpenLink', '"
                                                        + (if c.Link.IsSome then c.Link.Value else c.Url)
                                                        + "');"
                                                    )
                                                })
                                        }

                                        TemplateColumn'' {
                                            width "50px"

                                            ChildContent(fun (c: Channel) ->
                                                span { dataAccess.Channels.GetChannelUnreadCount(c.Id) })
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
                                            width "170px"

                                            ChildContent(fun (c: Channel) ->
                                                FluentButton'' {
                                                    IconStart(Icons.Regular.Size20.Edit())

                                                    OnClick(fun _ ->
                                                        task {
                                                            let dialogParams = DialogParameters()

                                                            dialogParams.Title <-
                                                                string (services.Localizer["EditFeedChannel"])

                                                            dialogParams.Height <- "350px"
                                                            dialogParams.PreventDismissOnOverlayClick <- true
                                                            dialogParams.PreventScroll <- true

                                                            let feed = dataAccess.Channels.Get(c.Id).Value

                                                            let data =
                                                                DialogData(
                                                                    ChannelEdit(feed, folders),
                                                                    services.Localizer
                                                                )

                                                            let! dialog =
                                                                dialogs.ShowDialogAsync<
                                                                    EditFeedDialog,
                                                                    DialogData<ChannelEdit>
                                                                 >(
                                                                    data,
                                                                    dialogParams
                                                                )
                                                                |> Async.AwaitTask

                                                            let! result = dialog.Result |> Async.AwaitTask

                                                            if not result.Cancelled && not (isNull result.Data) then
                                                                let resultData =
                                                                    result.Data :?> DialogData<ChannelEdit>

                                                                dataAccess.Channels.Update resultData.Data.Channel

                                                                store.FeedChannels.Publish(
                                                                    LoadedChannelsList(
                                                                        dataAccess.Channels.GetByGroupId
                                                                            resultData.Data.Channel.GroupId
                                                                    )
                                                                )

                                                                updateNavigation ()
                                                        })

                                                    string (services.Localizer["Edit"])
                                                })
                                        }

                                        TemplateColumn'' {
                                            width "140px"

                                            ChildContent(fun (c: Channel) ->
                                                FluentButton'' {
                                                    IconStart(Icons.Regular.Size20.Delete())

                                                    OnClick(fun _ ->
                                                        task {
                                                            let! dialog =
                                                                dialogs.ShowConfirmationAsync(
                                                                    string (services.Localizer["Feed"])
                                                                    + $" \"{c.Title}\" "
                                                                    + string (services.Localizer["WillBeRemoved"])
                                                                    + ". "
                                                                    + string (services.Localizer["AreYouSure"]),
                                                                    string (services.Localizer["Delete"]),
                                                                    string (services.Localizer["Cancel"]),
                                                                    string (services.Localizer["RemoveCurrentFeed"])
                                                                )
                                                                |> Async.AwaitTask

                                                            let! result = dialog.Result |> Async.AwaitTask

                                                            if not result.Cancelled then
                                                                dataAccess.Channels.Delete c.Id |> ignore

                                                                store.FeedChannels.Publish(
                                                                    LoadedChannelsList(
                                                                        dataAccess.Channels.GetByGroupId c.GroupId
                                                                    )
                                                                )

                                                                updateNavigation ()
                                                        })

                                                    string (services.Localizer["Delete"])
                                                })
                                        }
                                    }
                                }

                            }
                        }
                    }
                })
