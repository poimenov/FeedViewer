[<AutoOpen>]
module FeedViewer.DataAccess

open System
open System.IO
open Microsoft.Data.Sqlite
open System.Reflection
open System.Data
open Donald
open FeedViewer.Models
open System.Diagnostics

[<Literal>]
let ApplicationName = "FeedViewer"

[<Literal>]
let private DataBaseFileName = "FeedViewer.db"

let private CreateDatabaseScript =
    use stream =
        new StreamReader(
            Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("FeedViewer.CreateDatabase.sql")
        )

    stream.ReadToEnd()

let AppDataPath =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName)

let private DataBasePath = Path.Combine(AppDataPath, DataBaseFileName)

type IConnectionService =
    abstract member GetConnection: unit -> IDbConnection

type ConnectionService() =
    interface IConnectionService with
        member this.GetConnection() =
            new SqliteConnection($"Data Source={DataBasePath}") :> IDbConnection

type IDataBase =
    abstract member CreateDatabaseIfNotExists: unit -> unit

type DataBase(connectionService: IConnectionService) =
    interface IDataBase with
        member this.CreateDatabaseIfNotExists() =
            if not (Directory.Exists(AppDataPath)) then
                Directory.CreateDirectory(AppDataPath) |> ignore

            if not (File.Exists(DataBasePath)) then
                use conn = connectionService.GetConnection()
                use tran = conn.TryBeginTransaction()

                try
                    tran |> Db.newCommandForTransaction CreateDatabaseScript |> Db.exec
                    tran.TryCommit()
                with ex ->
                    Debug.WriteLine(ex)
                    tran.TryRollback()

type IChannelGroups =
    abstract member Create: ChannelGroup -> int
    abstract member Update: ChannelGroup -> unit
    abstract member Delete: int -> unit
    abstract member GetById: int -> ChannelGroup option
    abstract member GetByName: string -> ChannelGroup option
    abstract member GetAll: unit -> ChannelGroup list
    abstract member GetGroupUnreadCount: int -> int

type ChannelGroups(connectionService: IConnectionService) =
    interface IChannelGroups with
        member this.Create(channelGroup) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "INSERT INTO ChannelsGroups (Name)
                    SELECT @Name
                    WHERE NOT EXISTS (SELECT 1 FROM ChannelsGroups WHERE Name = @Name);
                    SELECT Id FROM ChannelsGroups WHERE Name = @Name;"
                |> Db.setParams [ "Name", SqlType.String channelGroup.Name ]

            cmd |> Db.scalar Convert.ToInt32

        member this.Delete(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "DELETE FROM ChannelsGroups WHERE Id = @Id;"
                |> Db.setParams [ "Id", SqlType.Int id ]

            cmd |> Db.exec

        member this.GetAll() =
            use conn = connectionService.GetConnection()

            use cmd = conn |> Db.newCommand "SELECT Id, Name FROM ChannelsGroups ORDER BY Name;"

            cmd
            |> Db.query (fun reader -> ChannelGroup(reader.ReadInt32("Id"), reader.ReadString("Name")))
            |> List.ofSeq

        member this.GetById(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM ChannelsGroups WHERE Id = @Id;"
                |> Db.setParams [ "Id", SqlType.Int id ]

            cmd
            |> Db.query (fun reader -> ChannelGroup(reader.ReadInt32("Id"), reader.ReadString("Name")))
            |> List.tryHead

        member this.GetByName(name) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM ChannelsGroups WHERE Name = @Name;"
                |> Db.setParams [ "Name", SqlType.String name ]

            cmd
            |> Db.query (fun reader -> ChannelGroup(reader.ReadInt32("Id"), reader.ReadString("Name")))
            |> List.tryHead

        member this.Update(channelGroup) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelsGroups SET Name = @Name WHERE Id = @Id;"
                |> Db.setParams [ "Name", SqlType.String channelGroup.Name; "Id", SqlType.Int channelGroup.Id ]

            cmd |> Db.exec

        member this.GetGroupUnreadCount(groupId: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT COUNT(*) FROM ChannelItems CI INNER JOIN Channels C
                        ON CI.ChannelId = C.Id WHERE C.ChannelsGroupId = @ChannelsGroupId;"
                |> Db.setParams [ "ChannelsGroupId", SqlType.Int groupId ]

            cmd |> Db.scalar Convert.ToInt32

type IChannels =
    abstract member Create: Channel -> int
    abstract member Update: Channel -> unit
    abstract member Delete: int -> unit
    abstract member Get: int -> Channel option
    abstract member Exists: string -> bool
    abstract member GetAll: unit -> Channel list
    abstract member GetByGroupId: int option -> Channel list
    abstract member GetChannelUnreadCount: int -> int
    abstract member GetAllUnreadCount: unit -> int
    abstract member GetStarredCount: unit -> int
    abstract member GetReadLaterCount: unit -> int

type Channels(connectionService: IConnectionService) =
    [<Literal>]
    let selectChannelSql =
        "SELECT Id, ChannelsGroupId, Title, Description, Link, Url, ImageUrl, Language FROM Channels"

    let getChannel (reader: IDataReader) =
        Channel(
            reader.ReadInt32("Id"),
            reader.ReadInt32Option("ChannelsGroupId"),
            reader.ReadString("Title"),
            reader.ReadStringOption("Description"),
            reader.ReadStringOption("Link"),
            reader.ReadString("Url"),
            reader.ReadStringOption("ImageUrl"),
            reader.ReadStringOption("Language")
        )

    interface IChannels with
        member this.Create(channel) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "INSERT INTO Channels (ChannelsGroupId, Title, Description, Link, Url, ImageUrl, Language)
                    SELECT @GroupId, @Title, @Description, @Link, @Url, @ImageUrl, @Language
                    WHERE NOT EXISTS (SELECT 1 FROM Channels WHERE Url = @Url);
                    SELECT Id FROM Channels WHERE Url = @Url;"
                |> Db.setParams
                    [ "GroupId",
                      if channel.GroupId.IsNone then
                          SqlType.Null
                      else
                          SqlType.Int channel.GroupId.Value
                      "Title", SqlType.String channel.Title
                      "Description", SqlType.String(defaultArg channel.Description null)
                      "Link", SqlType.String(defaultArg channel.Link null)
                      "Url", SqlType.String channel.Url
                      "ImageUrl", SqlType.String(defaultArg channel.ImageUrl null)
                      "Language", SqlType.String(defaultArg channel.Language null) ]

            cmd |> Db.scalar Convert.ToInt32

        member this.Delete(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "DELETE FROM Channels WHERE Id = @Id;"
                |> Db.setParams [ "Id", SqlType.Int id ]

            cmd |> Db.exec

        member this.Exists(url) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT EXISTS (SELECT 1 FROM Channels WHERE Url = @Url);"
                |> Db.setParams [ "Url", SqlType.String url ]

            cmd |> Db.scalar Convert.ToBoolean

        member this.Get(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectChannelSql + " WHERE Id = @Id;")
                |> Db.setParams [ "Id", SqlType.Int id ]

            cmd |> Db.query (fun reader -> getChannel reader) |> List.tryHead

        member this.GetAll() =
            use conn = connectionService.GetConnection()

            use cmd = conn |> Db.newCommand selectChannelSql

            cmd
            |> Db.query (fun reader -> getChannel reader)
            |> List.sortBy (fun channel -> channel.Title)

        member this.GetAllUnreadCount() =
            use conn = connectionService.GetConnection()

            use cmd =
                conn |> Db.newCommand "SELECT COUNT(*) FROM ChannelItems WHERE IsRead = 0;"

            cmd |> Db.scalar Convert.ToInt32

        member this.GetByGroupId(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                if id.IsNone then
                    conn |> Db.newCommand (selectChannelSql + " WHERE ChannelsGroupId IS NULL;")
                else
                    conn
                    |> Db.newCommand (selectChannelSql + " WHERE ChannelsGroupId = @GroupId;")
                    |> Db.setParams [ "GroupId", SqlType.Int id.Value ]

            cmd
            |> Db.query (fun reader -> getChannel reader)
            |> List.sortBy (fun channel -> channel.Title)

        member this.GetChannelUnreadCount(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT COUNT(*) FROM ChannelItems WHERE ChannelId = @ChannelId AND IsRead = 0;"
                |> Db.setParams [ "ChannelId", SqlType.Int id ]

            cmd |> Db.scalar Convert.ToInt32

        member this.GetReadLaterCount() =
            use conn = connectionService.GetConnection()

            use cmd =
                conn |> Db.newCommand "SELECT COUNT(*) FROM ChannelItems WHERE IsReadLater = 1;"

            cmd |> Db.scalar Convert.ToInt32

        member this.GetStarredCount() =
            use conn = connectionService.GetConnection()

            use cmd =
                conn |> Db.newCommand "SELECT COUNT(*) FROM ChannelItems WHERE IsFavorite = 1;"

            cmd |> Db.scalar Convert.ToInt32

        member this.Update(channel) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "UPDATE Channels 
                    SET 
                        Title = @Title, 
                        ChannelsGroupId = @GroupId,
                        Description = @Description, 
                        Link = @Link, 
                        Url = @Url, 
                        ImageUrl = @ImageUrl, 
                        Language = @Language 
                    WHERE Id = @Id;"
                |> Db.setParams
                    [ "Id", SqlType.Int channel.Id
                      "GroupId",
                      if channel.GroupId.IsNone then
                          SqlType.Null
                      else
                          SqlType.Int channel.GroupId.Value
                      "Title", SqlType.String channel.Title
                      "Description", SqlType.String(defaultArg channel.Description null)
                      "Link", SqlType.String(defaultArg channel.Link null)
                      "Url", SqlType.String channel.Url
                      "ImageUrl", SqlType.String(defaultArg channel.ImageUrl null)
                      "Language", SqlType.String(defaultArg channel.Language null) ]

            cmd |> Db.exec

type IChannelItems =
    abstract member Create: ChannelItem -> int64
    abstract member SetRead: int64 * bool -> unit
    abstract member SetReadByGroupId: int * bool -> unit
    abstract member SetReadByChannelId: int * bool -> unit
    abstract member SetReadAll: bool -> unit
    abstract member SetFavorite: int64 * bool -> unit
    abstract member SetDeleted: int64 * bool -> unit
    abstract member SetReadLater: int64 * bool -> unit
    abstract member GetByChannelId: int -> ChannelItem list
    abstract member GetByGroupId: int -> ChannelItem list
    abstract member GetByReadLater: bool -> ChannelItem list
    abstract member GetByRead: bool -> ChannelItem list
    abstract member GetByFavorite: bool -> ChannelItem list
    abstract member GetByDeleted: bool -> ChannelItem list
    abstract member GetByCategory: int -> ChannelItem list

type ChannelItems(connectionService: IConnectionService) =
    let selectSql (where: string) =
        let sql =
            "SELECT  Id, ChannelId, ItemId, Title, Link, Description, Content, 
            PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted
            FROM ChannelItems
            WHERE {0}
            ORDER BY PublishingDate DESC"

        System.String.Format(sql, where)

    let getChannelItem (reader: IDataReader) =
        ChannelItem(
            reader.ReadInt64("Id"),
            reader.ReadInt32("ChannelId"),
            reader.ReadString("ItemId"),
            reader.ReadString("Title"),
            reader.ReadStringOption("Link"),
            reader.ReadStringOption("Description"),
            reader.ReadStringOption("Content"),
            reader.ReadDateTimeOption("PublishingDate"),
            reader.ReadBoolean("IsRead"),
            reader.ReadBoolean("IsDeleted"),
            reader.ReadBoolean("IsFavorite"),
            reader.ReadBoolean("IsReadLater"),
            None
        )

    interface IChannelItems with
        member this.Create(channelItem: ChannelItem) : int64 =
            let getCmdInsertChannelItems (tran: IDbTransaction) =
                tran
                |> Db.newCommandForTransaction
                    "INSERT INTO ChannelItems
                    (ChannelId, ItemId, Title, Description, Content, Link, PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted)
                    SELECT
                    @ChannelId, @ItemId, @Title, @Description, @Content, @Link, @PublishingDate, @IsRead, @IsReadLater, @IsFavorite, @IsDeleted
                    WHERE NOT EXISTS (SELECT 1 FROM ChannelItems WHERE ItemId = @ItemId);
                    SELECT Id FROM ChannelItems WHERE ItemId = @ItemId;"
                |> Db.setParams
                    [ "ChannelId", SqlType.Int channelItem.ChannelId
                      "ItemId",
                      SqlType.String(
                          if String.IsNullOrWhiteSpace(channelItem.ItemId) then
                              defaultArg channelItem.Link (System.Guid.NewGuid().ToString())
                          else
                              channelItem.ItemId
                      )
                      "Title", SqlType.String channelItem.Title
                      "Description", SqlType.String(defaultArg channelItem.Description null)
                      "Content", SqlType.String(defaultArg channelItem.Content null)
                      "Link", SqlType.String(defaultArg channelItem.Link null)
                      "PublishingDate",
                      if channelItem.PublishingDate.IsNone then
                          SqlType.Null
                      else
                          SqlType.DateTime channelItem.PublishingDate.Value
                      "IsRead", SqlType.Boolean channelItem.IsRead
                      "IsReadLater", SqlType.Boolean channelItem.IsReadLater
                      "IsFavorite", SqlType.Boolean channelItem.IsFavorite
                      "IsDeleted", SqlType.Boolean channelItem.IsDeleted ]

            let getCmdInsertCategories (tran: IDbTransaction) =
                tran
                |> Db.newCommandForTransaction
                    "INSERT INTO Categories (Name) 
                        SELECT @Name 
                        WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE Name = @Name);
                        SELECT Id FROM Categories WHERE Name = @Name;"

            let getCmdInsertItemCategories (tran: IDbTransaction) =
                tran
                |> Db.newCommandForTransaction
                    "INSERT INTO ItemCategories (ChannelItemId, CategoryId)
                        SELECT @ChannelItemId, @CategoryId
                        WHERE NOT EXISTS (SELECT 1 FROM ItemCategories WHERE ChannelItemId = @ChannelItemId AND CategoryId = @CategoryId);"

            use conn = connectionService.GetConnection()

            conn
            |> Db.batch (fun tran ->
                use cmdInsertChannelItems = getCmdInsertChannelItems tran
                use cmdInsertCategories = getCmdInsertCategories tran
                use cmdInsertItemCategories = getCmdInsertItemCategories tran
                let id = cmdInsertChannelItems |> Db.scalar Convert.ToInt64

                let getCategoryId (category: string) =
                    cmdInsertCategories
                    |> Db.setParams [ "Name", SqlType.String category ]
                    |> Db.scalar Convert.ToInt32

                let addCategoryToChannelItem (categoryId: int) =
                    cmdInsertItemCategories
                    |> Db.setParams [ "ChannelItemId", SqlType.Int64 id; "CategoryId", SqlType.Int categoryId ]
                    |> Db.exec

                if channelItem.Categories.IsSome then
                    channelItem.Categories.Value
                    |> Seq.map getCategoryId
                    |> Seq.iter addCategoryToChannelItem

                id)


        member this.GetByCategory(categoryId: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT  CI.Id, ChannelId, ItemId, Title, Link, Description, Content,
                    PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted
                    FROM ChannelItems CI
                    INNER JOIN ItemCategories IC 
                    ON CI.Id = IC.ChannelItemId
                    WHERE IC.CategoryId = @CategoryId
                    ORDER BY CI.PublishingDate DESC"
                |> Db.setParams [ "CategoryId", SqlType.Int categoryId ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByChannelId(channelId: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "ChannelId= @ChannelId AND IsRead = 0")
                |> Db.setParams [ "ChannelId", SqlType.Int channelId ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByDeleted(isDeleted: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsDeleted= @IsDeleted")
                |> Db.setParams [ "IsDeleted", SqlType.Boolean isDeleted ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByFavorite(isFavorite: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsFavorite= @IsFavorite")
                |> Db.setParams [ "IsFavorite", SqlType.Boolean isFavorite ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByGroupId(groupId: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT CI.Id, ChannelId, ItemId, CI.Title, CI.Link, CI.Description, 
                    CI.Content, PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted
                    FROM ChannelItems CI
                    INNER JOIN Channels C
                    ON CI.ChannelId = C.Id
                    WHERE C.ChannelsGroupId = @ChannelsGroupId  AND IsRead = 0
                    ORDER BY CI.PublishingDate DESC"
                |> Db.setParams [ "ChannelsGroupId", SqlType.Int groupId ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByRead(isRead: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsRead= @IsRead")
                |> Db.setParams [ "IsRead", SqlType.Boolean isRead ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByReadLater(isReadLater: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsReadLater= @IsReadLater")
                |> Db.setParams [ "IsReadLater", SqlType.Boolean isReadLater ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.SetDeleted(id: int64, isDeleted: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsDeleted = @IsDeleted WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int64 id; "IsDeleted", SqlType.Boolean isDeleted ]

            cmd |> Db.exec

        member this.SetFavorite(id: int64, isFavorite: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsFavorite = @IsFavorite WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int64 id; "IsFavorite", SqlType.Boolean isFavorite ]

            cmd |> Db.exec

        member this.SetRead(id: int64, isRead: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsRead = @IsRead WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int64 id; "IsRead", SqlType.Boolean isRead ]

            cmd |> Db.exec

        member this.SetReadAll(isRead: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsRead = @IsRead"
                |> Db.setParams [ "IsRead", SqlType.Boolean isRead ]

            cmd |> Db.exec

        member this.SetReadByChannelId(channelId: int, isRead: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsRead = @IsRead WHERE ChannelId = @ChannelId"
                |> Db.setParams [ "ChannelId", SqlType.Int channelId; "IsRead", SqlType.Boolean isRead ]

            cmd |> Db.exec

        member this.SetReadByGroupId(groupId: int, isRead: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "UPDATE ChannelItems SET IsRead = @IsRead
                    FROM ChannelItems CI
                    INNER JOIN Channels C
                    ON CI.ChannelId = C.Id
                    WHERE C.ChannelsGroupId = @ChannelsGroupId"
                |> Db.setParams [ "ChannelsGroupId", SqlType.Int groupId; "IsRead", SqlType.Boolean isRead ]

            cmd |> Db.exec

        member this.SetReadLater(id: int64, isReadLater: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsReadLater = @IsReadLater WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int64 id; "IsReadLater", SqlType.Boolean isReadLater ]

            cmd |> Db.exec

type ICategories =
    abstract member GetByChannelItem: int64 -> Category list
    abstract member GetByName: string -> Category list
    abstract member Get: int -> Category option

type Categories(connectionService: IConnectionService) =
    let getCategory (reader: IDataReader) =
        Category(reader.ReadInt32("Id"), reader.ReadString("Name"))

    interface ICategories with
        member this.Get(categoryId: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM Categories WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int categoryId ]

            cmd |> Db.query (fun reader -> getCategory reader) |> Seq.tryHead

        member this.GetByChannelItem(channelItemId: int64) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT C.Id, C.Name
                    FROM Categories C
                    INNER JOIN ItemCategories IC
                    ON C.Id = IC.CategoryId
                    WHERE IC.ChannelItemId = @ChannelItemId"
                |> Db.setParams [ "ChannelItemId", SqlType.Int64 channelItemId ]

            cmd |> Db.query (fun reader -> getCategory reader)

        member this.GetByName(categoryName: string) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM Categories WHERE Name = @Name"
                |> Db.setParams [ "Name", SqlType.String categoryName ]

            cmd |> Db.query (fun reader -> getCategory reader) |> Seq.toList
