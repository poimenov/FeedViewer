namespace FeedViewer.Application

module OrganizeFeeds =
    open System
    open Microsoft.FluentUI.AspNetCore.Components
    open Fun.Blazor
    open FeedViewer

    let main =
        html.inject
            (fun
                (store: IShareStore,
                 hook: IComponentHook,
                 groups: IChannelGroups,
                 channels: IChannels,
                 dialogs: IDialogService) ->
                hook.AddInitializedTask(fun () ->
                    task { store.FeedGroups.Publish(FeedGroups.LoadedChannelGroupList(groups.GetAll())) })

                fragment {
                    h3 { "Organize Feeds" }

                    adapt {
                        let! feedGroups, setFeedGroups = store.FeedGroups.WithSetter()
                        let! selectedFeedGroup, setSelectedFeedGroup = store.SelectedFeedGroup.WithSetter()

                        let disabledFeedGroupButtons =
                            match selectedFeedGroup with
                            | SelectedFeedGroup.SelectedGroup sg -> sg.Id = 0
                            | SelectedFeedGroup.NotSelectedGroup -> true

                        let folders =
                            match feedGroups with
                            | FeedGroups.LoadedChannelGroupList foldersList -> ChannelGroup(0, "") :: foldersList
                            | _ -> []

                        FluentStack'' {
                            FluentSelect'' {
                                Width "200px"
                                Height "300px"
                                Label "Folders"
                                type' typeof<ChannelGroup>
                                Items folders
                                OptionValue(fun (x: ChannelGroup) -> Convert.ToString x.Id)
                                OptionText(fun (x: ChannelGroup) -> x.Name)

                                SelectedOptionChanged(fun (x: ChannelGroup) ->
                                    store.SelectedFeedGroup.Publish(SelectedFeedGroup.SelectedGroup x))
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.FolderAdd())

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
                                            groups.Create group |> ignore

                                            store.FeedGroups.Publish(
                                                FeedGroups.LoadedChannelGroupList(groups.GetAll())
                                            )
                                    })

                                "Add Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Edit())
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
                                            | SelectedFeedGroup.SelectedGroup sg -> sg
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
                                                let folder = result.Data :?> ChannelGroup
                                                groups.Update folder |> ignore

                                                store.FeedGroups.Publish(
                                                    FeedGroups.LoadedChannelGroupList(groups.GetAll())
                                                )
                                    })

                                "Edit Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Delete())
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
                                                groups.Delete group.Id |> ignore

                                                store.FeedGroups.Publish(
                                                    FeedGroups.LoadedChannelGroupList(groups.GetAll())
                                                )
                                    })

                                "Remove Folder"
                            }
                        }
                    }
                })
