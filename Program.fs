module Program

open Photino.Blazor
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.FluentUI.AspNetCore.Components
open FeedViewer
open System
open log4net.Config
open System.IO

[<EntryPoint>]
let main args =
    let builder = PhotinoBlazorAppBuilder.CreateDefault(args)
    let configPath = Path.Combine("wwwroot", "log4net.config")
    builder.Services.AddFunBlazorWasm() |> ignore
    builder.Services.AddFluentUIComponents() |> ignore

    builder.Services.AddLogging(fun logging -> logging.ClearProviders().AddLog4Net(configPath) |> ignore)
    |> ignore

    builder.Services.AddSingleton<IPlatformService, PlatformService>() |> ignore
    builder.Services.AddSingleton<IProcessService, ProcessService>() |> ignore

    builder.Services.AddSingleton<ILinkOpeningService, LinkOpeningService>()
    |> ignore

    builder.Services.AddScoped<IConnectionService, ConnectionService>() |> ignore
    builder.Services.AddScoped<IDataBase, DataBase>() |> ignore
    builder.Services.AddScoped<IChannelGroups, ChannelGroups>() |> ignore
    builder.Services.AddScoped<IChannels, Channels>() |> ignore
    builder.Services.AddScoped<IChannelItems, ChannelItems>() |> ignore
    builder.Services.AddScoped<ICategories, Categories>() |> ignore
    builder.Services.AddTransient<IOpenDialogService, OpenDialogService>() |> ignore
    builder.Services.AddTransient<IIconDownloader, IconDownloader>() |> ignore
    builder.Services.AddTransient<IChannelReader, ChannelReader>() |> ignore

    builder.Services.AddSingleton<IExportImportService, ExportImportService>()
    |> ignore

    let application = builder.Build()

    application.Services.GetRequiredService<IDataBase>().CreateDatabaseIfNotExists()

    application.RootComponents.AddFunBlazor("#app", app) |> ignore
    AppDomain.CurrentDomain.SetData("DataDirectory", FeedViewer.DataAccess.AppDataPath)
    XmlConfigurator.Configure(new FileInfo(configPath)) |> ignore

    // customize window
    application.MainWindow
        .SetSize(1024, 768)
        .SetIconFile(Path.Combine("wwwroot", "favicon.ico"))
        .SetTitle("FeedViewer")
    |> ignore

    AppDomain.CurrentDomain.UnhandledException.Add(fun e ->
        let ex = e.ExceptionObject :?> Exception
        application.MainWindow.ShowMessage(ex.Message, "Error") |> ignore)

    application.Run()
    0
