namespace FeedViewer.Application

[<AutoOpen>]
module Types =
    open Microsoft.AspNetCore.Components
    open Microsoft.AspNetCore.Components.Routing
    open Microsoft.Extensions.Localization
    open Microsoft.FluentUI.AspNetCore.Components
    open Microsoft.JSInterop
    open Fun.Blazor
    open FeedViewer
    open System

    type public OpenLinkProvider(los: ILinkOpeningService) =
        [<JSInvokable>]
        member public this.OpenLink(link: string) = los.OpenUrl link

    type ChannelId =
        | All
        | ReadLater
        | Starred
        | ByGroupId of int
        | ByChannelId of int
        | ByCategoryId of int
        | BySearchString of string

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

    type ChannelItems =
        | NotLoadedFeedItemsList
        | LoadedFeedItemsList of ChannelItem list

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

        member store.FeedItems =
            store.CreateCVal(nameof store.FeedItems, ChannelItems.NotLoadedFeedItemsList)

        member store.CurrentChannelId =
            store.CreateCVal(nameof store.CurrentChannelId, ChannelId.All)

        member store.CurrentIsRead = store.CreateCVal(nameof store.CurrentIsRead, false)

        member store.CurrentIsReadLater =
            store.CreateCVal(nameof store.CurrentIsReadLater, false)

        member store.CurrentIsFavorite =
            store.CreateCVal(nameof store.CurrentIsFavorite, false)

        member store.UnreadCount = store.CreateCVal(nameof store.UnreadCount, 0)
        member store.SearchString = store.CreateCVal(nameof store.SearchString, "")
        member store.SearchEnabled = store.CreateCVal(nameof store.SearchEnabled, false)

    type DialogData<'T>(data: 'T, localizer: IStringLocalizer<SharedResources>) =
        member val Data = data with get, set
        member val Localizer = localizer with get, set

    type FeedGroupDialog() =
        inherit FunComponent()

        [<Parameter>]
        member val Content = Unchecked.defaultof<DialogData<ChannelGroup>> with get, set

        interface IDialogContentComponent<DialogData<ChannelGroup>> with
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
                        label' (string (this.Content.Localizer["FolderName"]))
                        value this.Content.Data.Name
                        Immediate true

                        ValueChanged(fun (x: string) ->
                            this.Content.Data.Name <- x
                            this.StateHasChanged())
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
                        disabled (String.IsNullOrWhiteSpace this.Content.Data.Name)
                        OnClick(fun _ -> task { this.Dialog.CloseAsync(this.Content) |> Async.AwaitTask |> ignore })
                        string (this.Content.Localizer["Save"])
                    }

                    FluentButton'' {
                        Appearance Appearance.Neutral
                        OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                        string (this.Content.Localizer["Cancel"])
                    }
                }
            }

    type ChannelEdit(channnel: Channel, groups: list<ChannelGroup>) =
        member val Channel = channnel with get, set
        member val Groups = groups with get, set

    type EditFeedDialog() =
        inherit FunComponent()

        [<Parameter>]
        member val Content = Unchecked.defaultof<DialogData<ChannelEdit>> with get, set

        interface IDialogContentComponent<DialogData<ChannelEdit>> with
            member this.Content = this.Content

            member this.Content
                with set (value) = this.Content <- value

        [<CascadingParameter>]
        member val Dialog = Unchecked.defaultof<FluentDialog> with get, set

        override this.Render() =
            adapt {
                let groupId =
                    match this.Content.Data.Channel.GroupId with
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
                            label' (string (this.Content.Localizer["Title"]))
                            value this.Content.Data.Channel.Title
                            Immediate true

                            ValueChanged(fun (x: string) ->
                                this.Content.Data.Channel.Title <- x
                                this.StateHasChanged())
                        }

                        FluentTextField'' {
                            label' "Url"
                            style' "width: 450px;"
                            value this.Content.Data.Channel.Url
                            ReadOnly true
                        }

                        FluentSelect'' {
                            label' (string (this.Content.Localizer["Folder"]))
                            type' typeof<ChannelGroup>
                            Items this.Content.Data.Groups
                            OptionValue(fun (x: ChannelGroup) -> Convert.ToString x.Id)
                            OptionText(fun (x: ChannelGroup) -> x.Name)
                            SelectedOption(this.Content.Data.Groups |> List.find (fun x -> x.Id = groupId))

                            SelectedOptionChanged(fun (x: ChannelGroup) ->
                                let id =
                                    match x.Id with
                                    | 0 -> None
                                    | _ -> Some x.Id

                                this.Content.Data.Channel.GroupId <- id)
                        }
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
                        disabled (String.IsNullOrWhiteSpace this.Content.Data.Channel.Title)
                        OnClick(fun _ -> task { this.Dialog.CloseAsync(this.Content) |> Async.AwaitTask |> ignore })
                        string (this.Content.Localizer["Save"])
                    }

                    FluentButton'' {
                        Appearance Appearance.Neutral
                        OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                        string (this.Content.Localizer["Cancel"])
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
        member val Content = Unchecked.defaultof<DialogData<string>> with get, set

        interface IDialogContentComponent<DialogData<string>> with
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
                            value this.Content.Data
                            Immediate true
                            TextFieldType TextFieldType.Url

                            ValueChanged(fun (x: string) ->
                                this.IsValid <- isUrlValid x
                                this.Content.Data <- x
                                this.StateHasChanged())
                        }
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
                        disabled (not this.IsValid)
                        OnClick(fun _ -> task { this.Dialog.CloseAsync(this.Content) |> Async.AwaitTask |> ignore })
                        string (this.Content.Localizer["Save"])
                    }

                    FluentButton'' {
                        Appearance Appearance.Neutral
                        OnClick(fun _ -> task { this.Dialog.CancelAsync() |> Async.AwaitTask |> ignore })
                        string (this.Content.Localizer["Cancel"])
                    }
                }
            }

    type MyCheckBox() =
        inherit FunBlazorComponent()

        [<Parameter>]
        member val Value = false with get, set

        [<Parameter>]
        member val Title = "" with get, set

        [<Parameter>]
        member val CheckedIcon = Unchecked.defaultof<Icon> with get, set

        [<Parameter>]
        member val UnCheckedIcon = Unchecked.defaultof<Icon> with get, set

        [<Parameter>]
        member val OnValueChanged = Unchecked.defaultof<EventCallback<bool>> with get, set

        override this.Render() =
            adapt {
                FluentButton'' {
                    IconStart(if this.Value then this.CheckedIcon else this.UnCheckedIcon)
                    style' "height: 20px;"
                    title' this.Title

                    OnClick(fun _ ->
                        let newValue = not this.Value
                        this.Value <- newValue
                        this.StateHasChanged()
                        this.OnValueChanged.InvokeAsync(newValue) |> ignore)
                }
            }

    type MyCheckBox with
        static member Create
            (value: bool, title: string, checkedIcon: Icon, unCheckedIcon: Icon, onValueChanged: (bool -> unit)) =
            html.blazor<MyCheckBox> (
                ComponentAttrBuilder<MyCheckBox>()
                    .Add((fun x -> x.Value), value)
                    .Add((fun x -> x.Title), title)
                    .Add((fun x -> x.CheckedIcon), checkedIcon)
                    .Add((fun x -> x.UnCheckedIcon), unCheckedIcon)
                    .Add((fun x -> x.OnValueChanged), EventCallback<bool>(null, Action<bool> onValueChanged))
            )

    type CategoriesPanel() =
        inherit FunComponent()

        [<Parameter>]
        member val Content = Unchecked.defaultof<list<Category>> with get, set

        interface IDialogContentComponent<list<Category>> with
            member this.Content = this.Content

            member this.Content
                with set (value) = this.Content <- value

        [<CascadingParameter>]
        member val Dialog = Unchecked.defaultof<FluentDialog> with get, set

        override this.Render() : NodeRenderFragment =
            adapt {
                FluentDialogHeader'' {
                    title' this.Dialog.Instance.Parameters.Title
                    ShowDismiss true
                }

                FluentDialogBody'' {
                    FluentNavMenu'' {
                        Width 200

                        this.Content
                        |> List.map (fun c ->
                            FluentNavLink'' {
                                Href $"/category/{c.Id}"
                                Match NavLinkMatch.Prefix
                                Icon(Icons.Regular.Size20.Bookmark())
                                Tooltip c.Name
                                c.Name
                            })

                    }
                }
            }

    type Github() =
        inherit
            Icon(
                "GitHub",
                IconVariant.Regular,
                IconSize.Size20,
                @"<path fill-rule=""evenodd"" clip-rule=""evenodd"" d=""M10.178 0C4.55 0 0 4.583 0 10.254c0 4.533 2.915 8.369 6.959 9.727 0.506 0.102 0.691 -0.221 0.691 -0.492 0 -0.238 -0.017 -1.053 -0.017 -1.901 -2.831 0.611 -3.421 -1.222 -3.421 -1.222 -0.455 -1.188 -1.129 -1.494 -1.129 -1.494 -0.927 -0.628 0.068 -0.628 0.068 -0.628 1.028 0.068 1.567 1.053 1.567 1.053 0.91 1.562 2.376 1.12 2.966 0.849 0.084 -0.662 0.354 -1.12 0.64 -1.375 -2.258 -0.238 -4.634 -1.12 -4.634 -5.059 0 -1.12 0.404 -2.037 1.045 -2.75 -0.101 -0.255 -0.455 -1.307 0.101 -2.716 0 0 0.859 -0.272 2.797 1.053a9.786 9.786 0 0 1 2.545 -0.34c0.859 0 1.735 0.119 2.544 0.34 1.938 -1.324 2.797 -1.053 2.797 -1.053 0.556 1.409 0.202 2.462 0.101 2.716 0.657 0.713 1.045 1.63 1.045 2.75 0 3.939 -2.376 4.804 -4.651 5.059 0.371 0.323 0.691 0.934 0.691 1.901 0 1.375 -0.017 2.479 -0.017 2.818 0 0.272 0.185 0.594 0.691 0.493 4.044 -1.358 6.959 -5.195 6.959 -9.727C20.356 4.583 15.789 0 10.178 0z""/>"
            )
