[<AutoOpen>]
module FeedViewer.DataAccess

open System
open System.Data
open System.Diagnostics
open System.IO
open Microsoft.Data.Sqlite
open Microsoft.Extensions.Logging
open Donald
open FeedViewer.AppSettings
open FeedViewer.Models

let getNullableString (value: string option) =
    match value with
    | Some value -> SqlType.String value
    | None -> SqlType.Null

type IConnectionService =
    abstract member GetConnection: unit -> IDbConnection

type ConnectionService() =
    interface IConnectionService with
        member this.GetConnection() =
            new SqliteConnection $"Data Source={AppSettings.DataBasePath}" :> IDbConnection

type IDataBase =
    abstract member CreateDatabaseIfNotExists: unit -> unit

type DataBase(connectionService: IConnectionService, logger: ILogger<DataBase>) =
    interface IDataBase with
        member this.CreateDatabaseIfNotExists() =
            if not (Directory.Exists AppSettings.AppDataPath) then
                Directory.CreateDirectory AppSettings.AppDataPath |> ignore

            if not (File.Exists AppSettings.DataBasePath) then
                use conn = connectionService.GetConnection()
                use tran = conn.TryBeginTransaction()

                try
                    tran |> Db.newCommandForTransaction AppSettings.CreateDatabaseScript |> Db.exec
                    tran.TryCommit()
                with ex ->
                    Debug.WriteLine(ex)
                    logger.LogError(ex, "Error creating database")
                    tran.TryRollback()

type IChannelGroups =
    abstract member Create: ChannelGroup -> int
    abstract member Update: ChannelGroup -> unit
    abstract member Delete: int -> unit
    abstract member Exists: string -> bool
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

        member this.Exists(groupName) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT EXISTS (SELECT 1 FROM ChannelsGroups WHERE Name = @Name);"
                |> Db.setParams [ "Name", SqlType.String groupName ]

            cmd |> Db.scalar Convert.ToBoolean

        member this.GetAll() =
            use conn = connectionService.GetConnection()

            use cmd = conn |> Db.newCommand "SELECT Id, Name FROM ChannelsGroups ORDER BY Name;"

            cmd
            |> Db.query (fun reader -> ChannelGroup(reader.ReadInt32 "Id", reader.ReadString "Name"))
            |> List.ofSeq

        member this.GetById(id) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM ChannelsGroups WHERE Id = @Id;"
                |> Db.setParams [ "Id", SqlType.Int id ]

            cmd
            |> Db.query (fun reader -> ChannelGroup(reader.ReadInt32 "Id", reader.ReadString "Name"))
            |> List.tryHead

        member this.GetByName(name) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM ChannelsGroups WHERE Name = @Name;"
                |> Db.setParams [ "Name", SqlType.String name ]

            cmd
            |> Db.query (fun reader -> ChannelGroup(reader.ReadInt32 "Id", reader.ReadString "Name"))
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
                        ON CI.ChannelId = C.Id WHERE C.ChannelsGroupId = @ChannelsGroupId AND CI.IsRead = 0;"
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
    abstract member GetAllCount: bool -> int
    abstract member GetStarredCount: unit -> int
    abstract member GetReadLaterCount: unit -> int
    abstract member GetSearchCount: string -> int

type Channels(connectionService: IConnectionService) =
    [<Literal>]
    let selectChannelSql =
        "SELECT Id, ChannelsGroupId, Title, Description, Link, Url, ImageUrl, Language FROM Channels"

    let getChannel (reader: IDataReader) =
        Channel(
            reader.ReadInt32 "Id",
            reader.ReadInt32Option "ChannelsGroupId",
            reader.ReadString "Title",
            reader.ReadStringOption "Description",
            reader.ReadStringOption "Link",
            reader.ReadString "Url",
            reader.ReadStringOption "ImageUrl",
            reader.ReadStringOption "Language"
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
                      "Description", getNullableString channel.Description
                      "Link", getNullableString channel.Link
                      "Url", SqlType.String channel.Url
                      "ImageUrl", getNullableString channel.ImageUrl
                      "Language", getNullableString channel.Language ]

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

        member this.GetAllCount(isRead) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT COUNT(*) FROM ChannelItems WHERE IsRead = @IsRead;"
                |> Db.setParams [ "IsRead", SqlType.Boolean isRead ]

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
                      "Description", getNullableString channel.Description
                      "Link", getNullableString channel.Link
                      "Url", SqlType.String channel.Url
                      "ImageUrl", getNullableString channel.ImageUrl
                      "Language", getNullableString channel.Language ]

            cmd |> Db.exec

        member this.GetSearchCount(searchString: string) : int =
            let txt = searchString.Replace("%", " ").Replace("_", " ").Trim()
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT COUNT(*) FROM ChannelItems WHERE Title LIKE @txt OR Content LIKE @txt OR Description LIKE @txt"
                |> Db.setParams [ "txt", SqlType.String $"%%{txt}%%" ]

            cmd |> Db.scalar Convert.ToInt32

type IChannelItems =
    abstract member Create: ChannelItem -> int64
    abstract member Delete: unit -> unit
    abstract member SetRead: int64 * bool -> unit
    abstract member SetReadByGroupId: int * bool -> unit
    abstract member SetReadByChannelId: int * bool -> unit
    abstract member SetReadAll: bool -> unit
    abstract member SetFavorite: int64 * bool -> unit
    abstract member SetDeleted: int64 * bool -> unit
    abstract member SetReadLater: int64 * bool -> unit
    abstract member GetByChannelId: int * int * int -> ChannelItem list
    abstract member GetByGroupId: int * int * int -> ChannelItem list
    abstract member GetByReadLater: bool * int * int -> ChannelItem list
    abstract member GetByRead: bool * int * int -> ChannelItem list
    abstract member GetByFavorite: bool * int * int -> ChannelItem list
    abstract member GetByDeleted: bool * int * int -> ChannelItem list
    abstract member GetByCategoryId: int * int * int -> ChannelItem list
    abstract member GetBySearchString: string * int * int -> ChannelItem list

type ChannelItems(connectionService: IConnectionService) =

    let selectSql (where: string) =
        let sql =
            "SELECT  Id, ChannelId, ItemId, Title, Link, ThumbnailUrl, Description, Content, 
            PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted
            FROM ChannelItems
            WHERE {0}
            ORDER BY PublishingDate DESC
            LIMIT @limit OFFSET @offset"

        System.String.Format(sql, where)

    let getChannelItem (reader: IDataReader) =
        ChannelItem(
            reader.ReadInt64 "Id",
            reader.ReadInt32 "ChannelId",
            reader.ReadString "ItemId",
            reader.ReadString "Title",
            reader.ReadStringOption "Link",
            reader.ReadStringOption "ThumbnailUrl",
            reader.ReadStringOption "Description",
            reader.ReadStringOption "Content",
            reader.ReadDateTimeOption "PublishingDate",
            reader.ReadBoolean "IsRead",
            reader.ReadBoolean "IsDeleted",
            reader.ReadBoolean "IsFavorite",
            reader.ReadBoolean "IsReadLater",
            None
        )

    interface IChannelItems with
        member this.Create(channelItem: ChannelItem) : int64 =
            let getCmdInsertChannelItems (tran: IDbTransaction) =
                tran
                |> Db.newCommandForTransaction
                    "INSERT INTO ChannelItems
                    (ChannelId, ItemId, Title, Description, Content, Link, ThumbnailUrl, PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted)
                    SELECT
                    @ChannelId, @ItemId, @Title, @Description, @Content, @Link, @ThumbnailUrl, @PublishingDate, @IsRead, @IsReadLater, @IsFavorite, @IsDeleted
                    WHERE NOT EXISTS (SELECT 1 FROM ChannelItems WHERE ItemId = @ItemId);
                    SELECT Id FROM ChannelItems WHERE ItemId = @ItemId;"
                |> Db.setParams
                    [ "ChannelId", SqlType.Int channelItem.ChannelId
                      "ItemId",
                      SqlType.String(
                          if String.IsNullOrWhiteSpace channelItem.ItemId then
                              defaultArg channelItem.Link channelItem.Title
                          else
                              channelItem.ItemId
                      )
                      "Title", SqlType.String channelItem.Title
                      "Description", getNullableString channelItem.Description
                      "Content", getNullableString channelItem.Content
                      "Link", getNullableString channelItem.Link
                      "ThumbnailUrl", getNullableString channelItem.ThumbnailUrl
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

        member this.Delete() : unit =
            use conn = connectionService.GetConnection()

            use cmd = conn |> Db.newCommand "DELETE FROM ChannelItems WHERE IsDeleted = 1"

            cmd |> Db.exec

        member this.GetByCategoryId(categoryId: int, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT  CI.Id, ChannelId, ItemId, Title, Link, ThumbnailUrl, Description, Content,
                    PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted
                    FROM ChannelItems CI
                    INNER JOIN ItemCategories IC 
                    ON CI.Id = IC.ChannelItemId
                    WHERE IC.CategoryId = @CategoryId
                    ORDER BY CI.PublishingDate DESC
                    LIMIT @limit OFFSET @offset"
                |> Db.setParams
                    [ "CategoryId", SqlType.Int categoryId
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByChannelId(channelId: int, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "ChannelId= @ChannelId AND IsRead = 0")
                |> Db.setParams
                    [ "ChannelId", SqlType.Int channelId
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByDeleted(isDeleted: bool, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsDeleted= @IsDeleted")
                |> Db.setParams
                    [ "IsDeleted", SqlType.Boolean isDeleted
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByFavorite(isFavorite: bool, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsFavorite= @IsFavorite")
                |> Db.setParams
                    [ "IsFavorite", SqlType.Boolean isFavorite
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByGroupId(groupId: int, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT CI.Id, ChannelId, ItemId, CI.Title, CI.Link, CI.ThumbnailUrl, CI.Description, 
                    CI.Content, PublishingDate, IsRead, IsReadLater, IsFavorite, IsDeleted
                    FROM ChannelItems CI
                    INNER JOIN Channels C
                    ON CI.ChannelId = C.Id
                    WHERE C.ChannelsGroupId = @ChannelsGroupId  AND IsRead = 0
                    ORDER BY CI.PublishingDate DESC
                    LIMIT @limit OFFSET @offset"
                |> Db.setParams
                    [ "ChannelsGroupId", SqlType.Int groupId
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByRead(isRead: bool, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsRead= @IsRead")
                |> Db.setParams
                    [ "IsRead", SqlType.Boolean isRead
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

            cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

        member this.GetByReadLater(isReadLater: bool, offset: int, limit: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand (selectSql "IsReadLater= @IsReadLater")
                |> Db.setParams
                    [ "IsReadLater", SqlType.Boolean isReadLater
                      "limit", SqlType.Int limit
                      "offset", SqlType.Int offset ]

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
                    WHERE ChannelId IN (
                        SELECT C.Id FROM Channels C WHERE C.ChannelsGroupId = @ChannelsGroupId
                    )"
                |> Db.setParams [ "ChannelsGroupId", SqlType.Int groupId; "IsRead", SqlType.Boolean isRead ]

            cmd |> Db.exec

        member this.SetReadLater(id: int64, isReadLater: bool) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "UPDATE ChannelItems SET IsReadLater = @IsReadLater WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int64 id; "IsReadLater", SqlType.Boolean isReadLater ]

            cmd |> Db.exec

        member this.GetBySearchString(searchString: string, offset: int, limit: int) =
            if searchString.Length < 3 || searchString.Length > 35 then
                []
            else
                let txt = searchString.Replace("%", " ").Replace("_", " ").Trim()
                use conn = connectionService.GetConnection()

                use cmd =
                    conn
                    |> Db.newCommand (selectSql "Title LIKE @txt OR Content LIKE @txt OR Description LIKE @txt")
                    |> Db.setParams
                        [ "txt", SqlType.String $"%%{txt}%%"
                          "limit", SqlType.Int limit
                          "offset", SqlType.Int offset ]

                cmd |> Db.query (fun reader -> getChannelItem reader) |> Seq.toList

type ICategories =
    abstract member GetByChannelItemId: int64 -> Category list
    abstract member GetByCategoryCount: int -> int
    abstract member GetByName: string -> Category list
    abstract member Get: int -> Category option

type Categories(connectionService: IConnectionService) =
    let getCategory (reader: IDataReader) =
        Category(reader.ReadInt32 "Id", reader.ReadString "Name")

    interface ICategories with
        member this.Get(categoryId: int) =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand "SELECT Id, Name FROM Categories WHERE Id = @Id"
                |> Db.setParams [ "Id", SqlType.Int categoryId ]

            cmd |> Db.query (fun reader -> getCategory reader) |> Seq.tryHead

        member this.GetByChannelItemId(channelItemId: int64) =
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

        member this.GetByCategoryCount(categoryId: int) : int =
            use conn = connectionService.GetConnection()

            use cmd =
                conn
                |> Db.newCommand
                    "SELECT COUNT(*)
                    FROM ChannelItems CI
                    INNER JOIN ItemCategories IC 
                    ON CI.Id = IC.ChannelItemId
                    WHERE IC.CategoryId = @CategoryId
                    ORDER BY CI.PublishingDate DESC"
                |> Db.setParams [ "CategoryId", SqlType.Int categoryId ]

            cmd |> Db.scalar Convert.ToInt32

type IDataAccess =
    abstract member Channels: IChannels
    abstract member ChannelsGroups: IChannelGroups
    abstract member ChannelItems: IChannelItems
    abstract member Categories: ICategories

type DataAccess
    (channels: IChannels, channelsGroups: IChannelGroups, channelItems: IChannelItems, categories: ICategories) =
    interface IDataAccess with
        member this.Channels = channels
        member this.ChannelsGroups = channelsGroups
        member this.ChannelItems = channelItems
        member this.Categories = categories
