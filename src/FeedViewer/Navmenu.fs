namespace FeedViewer.Application

module Navmenu =
    open System
    open System.IO
    open Microsoft.AspNetCore.Components
    open Microsoft.AspNetCore.Components.Routing
    open Microsoft.FluentUI.AspNetCore.Components
    open Fun.Blazor
    open FeedViewer

    let getIconPath (channel: Channel option) =
        match channel with
        | None -> "icons/rss-button-orange.32.png"
        | Some channel ->
            let host = Uri(channel.Url).Host
            let files = Directory.GetFiles(iconsDirectoryPath, $"{host}.*")

            if files.Length > 0 then
                $"icons/{Path.GetFileName(files.[0])}"
            else
                "icons/rss-button-orange.32.png"

    let getIcon (src: String, size: IconSize) =
        Icon(String.Empty, IconVariant.Regular, size, $"<img src=\"{src}\"  style=\"width: 100%%;\" />")

    let getChannelIcon (channel: Channel option, size: IconSize) = getIcon (getIconPath (channel), size)

    let getChannelIcon20 (channel: Channel option) =
        getIcon (getIconPath (channel), IconSize.Size20)

    let getNavLinks (channels: list<Channel>) =
        channels
        |> Seq.map (fun c ->
            FluentNavLink'' {
                Href $"/channel/{c.Id}"
                Tooltip c.Title
                Match NavLinkMatch.Prefix
                Icon(getChannelIcon20 (Some(c)))
                c.Title
            })

    let main =
        html.injectWithNoKey
            (fun
                (store: IShareStore,
                 hook: IComponentHook,
                 navigation: NavigationManager,
                 dataAccess: IDataAccess,
                 channelReader: IChannelReader) ->
                hook.AddInitializedTask(fun () ->
                    task {
                        Async.StartWithContinuations(
                            channelReader.ReadAllChannelsAsync(),
                            (fun _ ->
                                match store.CurrentChannelId.Value with
                                | All -> navigation.NavigateTo("/channel/all")
                                | ReadLater -> navigation.NavigateTo("/channel/readlater")
                                | Starred -> navigation.NavigateTo("/channel/starred")
                                | ByGroupId groupId -> navigation.NavigateTo($"/group/{groupId}")
                                | ByChannelId channelId -> navigation.NavigateTo($"/channel/{channelId}")
                                | ByCategoryId categoryId -> navigation.NavigateTo($"/category/{categoryId}")
                                | BySearchString txt -> navigation.NavigateTo($"/search/{txt}")),
                            (fun ex -> printfn "%A" ex),
                            (fun _ -> ())
                        )

                    //printfn "navmenus"
                    })

                adaptiview () {
                    let! binding = store.IsMenuOpen.WithSetter()
                    let! expGroupsCount, setExpGroupsCount = store.ExpandedNavGroupCount.WithSetter()

                    let styleWidth =
                        if expGroupsCount > 0 then
                            "width: 270px;"
                        else
                            "width: 230px;"

                    div {
                        class' "navmenu"

                        style' (
                            if store.IsMenuOpen.Value then
                                styleWidth
                            else
                                "width: 40px;"
                        )

                        FluentNavMenu'' {
                            Width 200
                            Collapsible true
                            Expanded' binding

                            FluentNavLink'' {
                                Href "/channel/all"
                                Match NavLinkMatch.All
                                Icon(Icons.Regular.Size20.Document())
                                "All"
                            }

                            FluentNavLink'' {
                                Href "/channel/starred"
                                Match NavLinkMatch.Prefix
                                Icon(Icons.Regular.Size20.Star())
                                "Starred"
                            }

                            FluentNavLink'' {
                                Href "/channel/readlater"
                                Match NavLinkMatch.Prefix
                                Icon(Icons.Regular.Size20.Flag())
                                "Read Later"
                            }

                            yield!
                                dataAccess.ChannelsGroups.GetAll()
                                |> Seq.map (fun g ->
                                    FluentNavGroup'' {
                                        title' g.Name
                                        Tooltip g.Name
                                        href $"/group/{g.Id}"
                                        Icon(Icons.Regular.Size20.Folder())

                                        ExpandedChanged(fun b ->
                                            if b then
                                                setExpGroupsCount (expGroupsCount + 1)
                                            else
                                                setExpGroupsCount (expGroupsCount - 1))

                                        yield! dataAccess.Channels.GetByGroupId(Some(g.Id)) |> getNavLinks
                                    })

                            yield! dataAccess.Channels.GetByGroupId(None) |> getNavLinks
                        }
                    }
                })
