namespace FeedViewer.Application

module App =
    open System.IO
    open System.Linq
    open Microsoft.AspNetCore.Components.Web
    open Microsoft.Extensions.Options
    open Microsoft.FluentUI.AspNetCore.Components
    open Microsoft.JSInterop
    open Fun.Blazor
    open Fun.Blazor.Router
    open FeedViewer.Services
    open FeedViewer

    let appHeader =
        html.inject
            (fun
                (store: IShareStore,
                 hook: IComponentHook,
                 options: IOptions<AppSettings>,
                 exportImport: IExportImportService,
                 services: IServices) ->
                hook.AddInitializedTask(fun _ -> task { store.Limit.Publish options.Value.DefaultLimit })

                FluentHeader'' {
                    FluentStack'' {
                        Orientation Orientation.Horizontal
                        HorizontalGap 2

                        img {
                            src AppSettings.FavIconFileName
                            style { height "40px" }
                        }

                        FluentLabel'' {
                            Typo Typography.H2
                            Color Color.Fill
                            AppSettings.ApplicationName
                        }

                        FluentSpacer''

                        FluentButton'' {
                            Appearance Appearance.Accent
                            IconStart(Github())
                            title' (string (services.Localizer["GitHub"]))

                            OnClick(fun _ ->
                                services.LinkOpeningService.OpenUrl "https://github.com/poimenov/FeedViewer")
                        }

                        adapt {
                            let! isOpen = store.IsSettingsOpen.WithSetter()

                            FluentDesignTheme'' {
                                StorageName "theme"
                                Mode store.Theme.Value
                                OfficeColor options.Value.AccentColor

                                OnLoaded(fun args ->
                                    if args.IsDark then
                                        store.Theme.Publish DesignThemeModes.Dark)
                            }

                            FluentButton'' {
                                Id "SettingsMenuButton"
                                Appearance Appearance.Accent
                                IconStart(Icons.Regular.Size20.Settings())
                                title' (string (services.Localizer["Settings"]))
                                OnClick(fun _ -> store.IsSettingsOpen.Publish not)
                            }

                            FluentMenu'' {
                                Anchor "SettingsMenuButton"
                                Open' isOpen
                                UseMenuService false

                                FluentMenuItem'' {
                                    OnClick(fun _ ->
                                        let folder =
                                            services.OpenDialogService.OpenFolder(
                                                title = string (services.Localizer["SelectFolder"]),
                                                multiSelect = false
                                            )

                                        if folder.Any() then
                                            Path.Combine(folder.First(), "FeedViewer.opml") |> exportImport.Export)

                                    string (services.Localizer["Export"])

                                    span {
                                        slot' "start"

                                        FluentIcon'' {
                                            slot' "start"
                                            color Color.Neutral
                                            Value(Icons.Regular.Size20.ArrowExport())
                                        }
                                    }
                                }

                                FluentMenuItem'' {
                                    OnClick(fun _ ->
                                        let file =
                                            services.OpenDialogService.OpenFile(
                                                title = string (services.Localizer["SelectOpmlFile"]),
                                                multiSelect = false,
                                                filters = [| ("Opml file", [| "opml" |]) |]
                                            )

                                        if file.Any() then
                                            file.First() |> exportImport.Import

                                        Async.StartWithContinuations(
                                            services.ChannelReader.ReadAllChannelsAsync(),
                                            (fun _ ->
                                                //refresh navmenu
                                                store.IsMenuOpen.Publish false
                                                store.IsMenuOpen.Publish true
                                                //navigate to all channels
                                                services.Navigation.NavigateTo "/channel/all"),
                                            (fun ex -> printfn "%A" ex),
                                            (fun _ -> ())
                                        ))

                                    string (services.Localizer["Import"])

                                    span {
                                        slot' "start"

                                        FluentIcon'' {
                                            slot' "start"
                                            color Color.Neutral
                                            Value(Icons.Regular.Size20.ArrowImport())
                                        }
                                    }
                                }

                                FluentMenuItem'' {
                                    OnClick(fun _ ->
                                        store.Theme.Publish(
                                            if store.Theme.Value = DesignThemeModes.Dark then
                                                DesignThemeModes.Light
                                            else
                                                DesignThemeModes.Dark
                                        ))

                                    if store.Theme.Value = DesignThemeModes.Dark then
                                        string (services.Localizer["SwitchToLightMode"])
                                    else
                                        string (services.Localizer["SwitchToDarkMode"])

                                    span {
                                        slot' "start"

                                        FluentIcon'' {
                                            slot' "start"
                                            color Color.Neutral
                                            Value(Icons.Regular.Size20.DarkTheme())
                                        }
                                    }
                                }

                                FluentMenuItem'' {
                                    OnClick(fun _ -> services.Navigation.NavigateTo "/feeds")
                                    string (services.Localizer["OrganizeFeeds"])

                                    span {
                                        slot' "start"

                                        FluentIcon'' {
                                            slot' "start"
                                            color Color.Neutral
                                            Value(Icons.Regular.Size20.AppsListDetail())
                                        }
                                    }
                                }
                            }
                        }
                    }
                })

    let appFooter =
        html.inject (fun (los: ILinkOpeningService, hook: IComponentHook, jsRuntime: IJSRuntime) ->
            hook.AddFirstAfterRenderTask(fun _ ->
                task {

                    let losObjRef = DotNetObjectReference.Create(OpenLinkProvider los)
                    jsRuntime.InvokeAsync("SetOpenLinkProvider", losObjRef) |> ignore
                })

            FluentFooter'' {
                FluentAnchor'' {
                    Appearance Appearance.Hypertext
                    href "#"
                    OnClick(fun _ -> los.OpenUrl "https://slaveoftime.github.io/Fun.Blazor.Docs/")
                    "Fun.Blazor"
                }

                FluentSpacer''

                FluentAnchor'' {
                    Appearance Appearance.Hypertext
                    href "#"
                    OnClick(fun _ -> los.OpenUrl "https://www.tryphotino.io")
                    "Photino"
                }
            })

    let routes =
        html.route
            [| routeCi "/channel/all" (ContentPage.main AllUnread)
               routeCi "/channel/readlater" (ContentPage.main ReadLater)
               routeCi "/channel/starred" (ContentPage.main Starred)
               routeCi "/channel/recentlyread" (ContentPage.main RecentlyRead)
               routeCif "/channel/%i" (fun x -> ContentPage.main (ByChannelId x))
               routeCif "/group/%i" (fun x -> ContentPage.main (ByGroupId x))
               routeCif "/category/%i" (fun x -> ContentPage.main (ByCategoryId x))
               routeCif "/search/%s" (fun x -> ContentPage.main (BySearchString x))
               routeCi "/feeds" OrganizeFeeds.main
               routeAny (ContentPage.main AllUnread) |]

    let main =
        ErrorBoundary'' {
            ErrorContent(fun e ->
                FluentLabel'' {
                    Color Color.Error
                    string e
                })

            FluentToastProvider''
            FluentDialogProvider''
            FluentDesignTheme'' { StorageName "theme" }

            FluentLayout'' {
                appHeader

                FluentStack'' {
                    Width "100%"
                    class' "main"
                    Orientation Orientation.Horizontal

                    Navmenu.main

                    FluentBodyContent'' {
                        class' "body-content"
                        style { overflowHidden }

                        div {
                            class' "content"
                            routes
                        }
                    }
                }

                appFooter
            }
        }
