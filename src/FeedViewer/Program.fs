module Program

open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FluentUI.AspNetCore.Components
open log4net.Config
open Photino.Blazor
open FeedViewer.Application
open FeedViewer.Services
open FeedViewer.DataAccess
open FeedViewer.AppSettings

[<STAThread>]
[<EntryPoint>]
let main args =
    let DATA_DIRECTORY = "DATA_DIRECTORY"
    let builder = PhotinoBlazorAppBuilder.CreateDefault args

    let configuration =
        ConfigurationBuilder()
            .SetBasePath(AppSettings.AssemblyFolderPath)
            .AddJsonFile(Path.Combine(AppSettings.AssemblyFolderPath, AppSettings.AppConfigFileName), true, true)
            .Build()

    builder.Services.AddFunBlazorWasm() |> ignore
    builder.Services.AddFluentUIComponents() |> ignore

    builder.Services.AddLogging(fun logging -> logging.ClearProviders().AddLog4Net() |> ignore<ILoggingBuilder>)
    |> ignore

    builder.Services.AddLocalization(fun options -> options.ResourcesPath <- "Resources")
    |> ignore

    builder.Services.AddSingleton<IConfiguration> configuration |> ignore
    builder.Services.Configure<AppSettings> configuration |> ignore
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
    builder.Services.AddTransient<IServices, Services>() |> ignore

    builder.Services.AddSingleton<IExportImportService, ExportImportService>()
    |> ignore

    let application = builder.Build()

    application.Services.GetRequiredService<IDataBase>().CreateDatabaseIfNotExists()

    application.RootComponents.AddFunBlazor("#app", App.main) |> ignore
    AppDomain.CurrentDomain.SetData("DataDirectory", AppSettings.AppDataPath)
    Environment.SetEnvironmentVariable(DATA_DIRECTORY, AppSettings.AppDataPath)
    FileInfo AppSettings.LogConfigPath |> XmlConfigurator.Configure |> ignore

    let logger = application.Services.GetRequiredService<ILogger<_>>()
    logger.LogInformation "Starting application"
    let settings = application.Services.GetRequiredService<IOptions<AppSettings>>()
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.GetCultureInfo settings.Value.CultureName
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.GetCultureInfo settings.Value.CultureName

    // customize window
    application.MainWindow
        .SetSize(settings.Value.WindowWidth, settings.Value.WindowHeight)
        .SetIconFile(Path.Combine(AppSettings.WwwRootFolderName, AppSettings.FavIconFileName))
        .SetTitle(AppSettings.ApplicationName)
        .RegisterWindowClosingHandler(fun _ _ ->
            application.Services.GetRequiredService<IChannelItems>().Delete()
            false)
    |> ignore

    AppDomain.CurrentDomain.UnhandledException.Add(fun e ->
        let ex = e.ExceptionObject :?> Exception
        application.Services.GetRequiredService<ILogger<_>>().LogError(ex, ex.Message)
        application.MainWindow.ShowMessage(ex.Message, "Error") |> ignore)

    application.Run()
    0
