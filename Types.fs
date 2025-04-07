namespace FeedViewer.Application

[<AutoOpen>]
module Types =
    open Microsoft.AspNetCore.Components
    open Microsoft.FluentUI.AspNetCore.Components
    open Microsoft.JSInterop
    open Fun.Blazor
    open FeedViewer
    open System

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

    type FeedGroups =
        | NotLoadedChannelGroupList
        | LoadedChannelGroupList of ChannelGroup list

    type SelectedFeedGroup =
        | NotSelectedGroup
        | SelectedGroup of ChannelGroup

    type FeedChannels =
        | NotLoadedChannelsList
        | LoadedChannelsList of Channel list

    type SelectedFeedChannel =
        | NotSelectedChannel
        | SelectedChannel of Channel

    type IShareStore with
        member store.Count = store.CreateCVal(nameof store.Count, 0)
        member store.IsMenuOpen = store.CreateCVal(nameof store.IsMenuOpen, true)
        member store.IsSettingsOpen = store.CreateCVal(nameof store.IsSettingsOpen, false)
        member store.Theme = store.CreateCVal(nameof store.Theme, DesignThemeModes.Light)
        member store.LeftPaneWidth = store.CreateCVal(nameof store.LeftPaneWidth, 350)

        member store.ExpandedNavGroupCount =
            store.CreateCVal(nameof store.ExpandedNavGroupCount, 0)

        member store.SelectedChannelItem =
            store.CreateCVal(nameof store.SelectedChannelItem, SelectedChannelItem.NotSelected)

        member store.FeedGroups =
            store.CreateCVal(nameof store.FeedGroups, FeedGroups.NotLoadedChannelGroupList)

        member store.SelectedFeedGroup =
            store.CreateCVal(nameof store.SelectedFeedGroup, SelectedFeedGroup.NotSelectedGroup)

        member store.FeedChannels =
            store.CreateCVal(nameof store.FeedChannels, FeedChannels.NotLoadedChannelsList)

        member store.SelectedFeedChannel =
            store.CreateCVal(nameof store.SelectedFeedChannel, SelectedFeedChannel.NotSelectedChannel)

    type FeedGroupDialog() =
        inherit FunComponent()

        [<Parameter>]
        member val Content = Unchecked.defaultof<ChannelGroup> with get, set

        interface IDialogContentComponent<ChannelGroup> with
            member this.Content = this.Content

            member this.Content
                with set (value) = this.Content <- value

        [<CascadingParameter>]
        member val Dialog = Unchecked.defaultof<FluentDialog> with get, set

        override this.Render() =
            adapt {
                FluentDialogHeader'' {
                    title' this.Dialog.Instance.Parameters.Title
                    ShowDismiss true
                }

                FluentDialogBody'' {
                    FluentTextField'' {
                        label' "Group Name"
                        value this.Content.Name
                        Immediate true

                        ValueChanged(fun (x: string) ->
                            this.Content.Name <- x
                            this.StateHasChanged())
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
                        disabled (String.IsNullOrWhiteSpace this.Content.Name)
                        OnClick(fun _ -> task { this.Dialog.CloseAsync(this.Content) |> Async.AwaitTask |> ignore })
                        "Save"
                    }

                    FluentButton'' {
                        Appearance Appearance.Neutral
                        OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                        "Cancel"
                    }
                }
            }

    type ChannelEdit(channnel: Channel, groups: list<ChannelGroup>) =
        member val Channel = channnel with get, set
        member val Groups = groups with get, set

    type EditFeedDialog() =
        inherit FunComponent()

        [<Parameter>]
        member val Content = Unchecked.defaultof<ChannelEdit> with get, set

        interface IDialogContentComponent<ChannelEdit> with
            member this.Content = this.Content

            member this.Content
                with set (value) = this.Content <- value

        [<CascadingParameter>]
        member val Dialog = Unchecked.defaultof<FluentDialog> with get, set

        override this.Render() =
            adapt {
                let groupId =
                    match this.Content.Channel.GroupId with
                    | Some id -> id
                    | None -> 0

                FluentDialogHeader'' {
                    title' this.Dialog.Instance.Parameters.Title
                    ShowDismiss true
                }

                FluentDialogBody'' {
                    FluentStack'' {
                        Orientation Orientation.Vertical

                        FluentTextField'' {
                            label' "Title"
                            value this.Content.Channel.Title
                            Immediate true

                            ValueChanged(fun (x: string) ->
                                this.Content.Channel.Title <- x
                                this.StateHasChanged())
                        }

                        FluentTextField'' {
                            label' "Url"
                            style' "width: 450px;"
                            value this.Content.Channel.Url
                            ReadOnly true
                        }

                        FluentSelect'' {
                            label' "Folder"
                            type' typeof<ChannelGroup>
                            Items this.Content.Groups
                            OptionValue(fun (x: ChannelGroup) -> Convert.ToString x.Id)
                            OptionText(fun (x: ChannelGroup) -> x.Name)
                            SelectedOption(this.Content.Groups |> List.find (fun x -> x.Id = groupId))

                            SelectedOptionChanged(fun (x: ChannelGroup) ->
                                let id =
                                    match x.Id with
                                    | 0 -> None
                                    | _ -> Some x.Id

                                this.Content.Channel.GroupId <- id)
                        }
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
                        disabled (String.IsNullOrWhiteSpace this.Content.Channel.Title)
                        OnClick(fun _ -> task { this.Dialog.CloseAsync(this.Content) |> Async.AwaitTask |> ignore })
                        "Save"
                    }

                    FluentButton'' {
                        Appearance Appearance.Neutral
                        OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                        "Cancel"
                    }
                }
            }

    type AddFeedDialog() =
        inherit FunComponent()

        let isUrlValid (input: string) =
            if String.IsNullOrWhiteSpace input then
                false
            else
                match System.Uri.TryCreate(input, System.UriKind.Absolute) with
                | true, uri -> (uri.Scheme = "http" || uri.Scheme = "https")
                | false, _ -> false

        member val IsValid = false with get, set

        [<Parameter>]
        member val Content = Unchecked.defaultof<string> with get, set

        interface IDialogContentComponent<string> with
            member this.Content = this.Content

            member this.Content
                with set (value) = this.Content <- value

        [<CascadingParameter>]
        member val Dialog = Unchecked.defaultof<FluentDialog> with get, set

        override this.Render() =
            adapt {
                FluentDialogHeader'' {
                    title' this.Dialog.Instance.Parameters.Title
                    ShowDismiss true
                }

                FluentDialogBody'' {
                    FluentStack'' {
                        Orientation Orientation.Vertical

                        FluentTextField'' {
                            label' "URL"
                            style' "width: 450px;"
                            value this.Content
                            Immediate true
                            TextFieldType TextFieldType.Url

                            ValueChanged(fun (x: string) ->
                                this.IsValid <- isUrlValid x
                                this.Content <- x
                                this.StateHasChanged())
                        }
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
                        disabled (not this.IsValid)
                        OnClick(fun _ -> task { this.Dialog.CloseAsync(this.Content) |> Async.AwaitTask |> ignore })
                        "Save"
                    }

                    FluentButton'' {
                        Appearance Appearance.Neutral
                        OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                        "Cancel"
                    }
                }
            }
