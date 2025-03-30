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

                                // OnClick(fun _ -> async {
                                //     let mutable newFolderName = ""
                                //     dialogParams = DialogParameters {
                                //         Title "New Folder"
                                //         Height 250
                                //         PreventDismissOnOverlayClick = true
                                //         PreventScroll = true
                                //     }
                                //     let! dialogInstance = dialogs.ShowDialogAsync<SimpleCustomizedDialog>(newFolderName, dialogParams)
                                //     let! result = dialog.Result
                                //     if not result.Cancelled && result.Data <> null then
                                //         newFolderName <- result.Data :?> string
                                // })


                                // OnClick(fun _ ->
                                //     let folder = ChannelGroup(0, "New Folder")
                                //     groups.Create folder |> ignore
                                //     store.FeedGroups.Publish(FeedGroups.LoadedChannelGroupList(groups.GetAll())))

                                "Add Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Edit())
                                Disabled(disabledFeedGroupButtons)

                                // OnClick(fun _ ->
                                //     let folder = ChannelGroup(0, "New Folder")
                                //     groups.Create folder |> ignore
                                //     store.FeedGroups.Publish(FeedGroups.LoadedChannelGroupList(groups.GetAll())))

                                "Edit Folder"
                            }

                            FluentButton'' {
                                IconStart(Icons.Regular.Size20.Delete())
                                Disabled(disabledFeedGroupButtons)

                                // OnClick(fun _ ->
                                //     store.FeedGroups.Publish(FeedGroups.LoadedChannelGroupList(groups.GetAll())))

                                "Remove Folder"
                            }
                        }
                    }
                })
