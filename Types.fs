namespace FeedViewer.Application

[<AutoOpen>]
module Types =
    open Microsoft.AspNetCore.Components
    open Microsoft.FluentUI.AspNetCore.Components
    open Microsoft.JSInterop
    open Fun.Blazor
    open FeedViewer

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
                        ValueChanged(fun (x: string) -> this.Content.Name <- x)
                    //OnChange(fun (x: string) -> this.Content.Name <- x)
                    }
                }

                FluentDialogFooter'' {
                    FluentButton'' {
                        Appearance Appearance.Accent
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
