[<AutoOpen>]
module FeedViewer.AppSettings

open System
open System.IO
open System.Reflection
open Microsoft.FluentUI.AspNetCore.Components

type public AppSettings() =
    static member ApplicationName = "FeedViewer"
    static member FavIconFileName = "favicon.ico"
    static member DataBaseFileName = $"{AppSettings.ApplicationName}.db"
    static member CreateDatabaseScriptName = $"{AppSettings.ApplicationName}.CreateDatabase.sql"
    static member LogConfigFileName = "log4net.config"
    static member AppConfigFileName = "appsettings.json"
    static member WwwRootFolderName = "wwwroot"

    static member AppDataPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppSettings.ApplicationName
        )

    static member AssemblyFolderPath =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

    static member DataBasePath =
        Path.Combine(AppSettings.AppDataPath, AppSettings.DataBaseFileName)

    static member LogConfigPath =
        Path.Combine(AppSettings.AssemblyFolderPath, AppSettings.LogConfigFileName)

    static member WwwRootFolderPath =
        Path.Combine(AppSettings.AssemblyFolderPath, AppSettings.WwwRootFolderName)

    static member IconsDirectoryPath = Path.Combine(AppSettings.WwwRootFolderPath, "icons")

    static member CreateDatabaseScript =
        use stream =
            new StreamReader(
                Assembly
                    .GetExecutingAssembly()
                    .GetManifestResourceStream(AppSettings.CreateDatabaseScriptName)
            )

        stream.ReadToEnd()

    member val WindowWidth: int = 1024 with get, set
    member val WindowHeight: int = 768 with get, set
    member val AccentColor: OfficeColor = OfficeColor.Windows with get, set
    member val CultureName: string = "en-US" with get, set
