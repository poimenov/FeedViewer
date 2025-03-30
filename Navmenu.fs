namespace FeedViewer.Application

module Navmenu =
    open System
    open System.IO
    open Microsoft.AspNetCore.Components
    open Microsoft.AspNetCore.Components.Routing
    open Microsoft.Extensions.Logging
    open Microsoft.FluentUI.AspNetCore.Components
    open Fun.Blazor
    open FeedViewer

    let getChannelIcon (channel: Channel) =
        let host = Uri(channel.Url).Host
        let files = Directory.GetFiles(iconsDirectoryPath, $"{host}.*")

        let filePath =
            if files.Length > 0 then
                $"icons/{Path.GetFileName(files.[0])}"
            else
                "icons/rss-button-orange.32.png"

        Icon(String.Empty, IconVariant.Regular, IconSize.Size20, $"<img src=\"{filePath}\"  style=\"width: 100%%;\" />")

    let getNavLinks (channels: list<Channel>) =
        channels
        |> Seq.map (fun c ->
            FluentNavLink'' {
                Href $"/channel/{c.Id}"
                Tooltip c.Title
                Match NavLinkMatch.Prefix
                Icon(getChannelIcon c)
                c.Title
            })

    let main =
        html.injectWithNoKey
            (fun
                (store: IShareStore,
                 hook: IComponentHook,
                 navigation: NavigationManager,
                 logger: ILogger<_>,
                 groups: IChannelGroups,
                 channels: IChannels,
                 channelReader: IChannelReader) ->
                hook.AddInitializedTask(fun () ->
                    task {
                        // Async.StartWithContinuations(
                        //     channelReader.ReadAllChannelsAsync(),
                        //     (fun _ -> navigation.NavigateTo("/channel/all")),
                        //     (fun ex ->
                        //         printfn "%A" ex
                        //         logger.LogError(ex, "Error in App.navmenus")),
                        //     (fun _ -> ())
                        // )
                        printfn "navmenus"
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
                                groups.GetAll()
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

                                        yield! channels.GetByGroupId(Some(g.Id)) |> getNavLinks
                                    })

                            yield! channels.GetByGroupId(None) |> getNavLinks
                        }
                    }
                })
