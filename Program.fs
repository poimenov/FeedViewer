module Program

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.FluentUI.AspNetCore.Components
open log4net.Config
open Photino.Blazor
open FeedViewer.Application
open FeedViewer.Services
open FeedViewer.DataAccess

[<EntryPoint>]
let main args =
    let DATA_DIRECTORY = "DATA_DIRECTORY"
    let builder = PhotinoBlazorAppBuilder.CreateDefault(args)

    let logConfigPath =
        let assemblyFolderPath =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

        Path.Combine(assemblyFolderPath, "log4net.config")

    builder.Services.AddFunBlazorWasm() |> ignore
    builder.Services.AddFluentUIComponents() |> ignore

    builder.Services.AddLogging(fun logging -> logging.ClearProviders().AddLog4Net() |> ignore<ILoggingBuilder>)
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
    builder.Services.AddScoped<IDataAccess, DataAccess>() |> ignore
    builder.Services.AddTransient<IOpenDialogService, OpenDialogService>() |> ignore
    builder.Services.AddTransient<IHttpHandler, HttpHandler>() |> ignore
    builder.Services.AddTransient<IIconDownloader, IconDownloader>() |> ignore
    builder.Services.AddTransient<IChannelReader, ChannelReader>() |> ignore

    builder.Services.AddSingleton<IExportImportService, ExportImportService>()
    |> ignore

    let application = builder.Build()

    application.Services.GetRequiredService<IDataBase>().CreateDatabaseIfNotExists()

    application.RootComponents.AddFunBlazor("#app", App.main) |> ignore
    AppDomain.CurrentDomain.SetData("DataDirectory", AppDataPath)
    Environment.SetEnvironmentVariable(DATA_DIRECTORY, AppDataPath)
    FileInfo logConfigPath |> XmlConfigurator.Configure |> ignore

    let logger = application.Services.GetRequiredService<ILogger<_>>()
    logger.LogInformation("Starting application")

    // customize window
    application.MainWindow
        .SetSize(1024, 768)
        .SetIconFile(Path.Combine("wwwroot", "favicon.ico"))
        .SetTitle("FeedViewer")
    |> ignore

    AppDomain.CurrentDomain.UnhandledException.Add(fun e ->
        let ex = e.ExceptionObject :?> Exception
        application.Services.GetRequiredService<ILogger<_>>().LogError(ex, ex.Message)
        application.MainWindow.ShowMessage(ex.Message, "Error") |> ignore)

    application.Run()
    0
