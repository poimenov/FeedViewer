[<AutoOpen>]
module FeedViewer.Models

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open System.Web

type Category(id: int, name: string) =
    member val Id = id with get, set
    member val Name = name with get, set

type ItemCategory(categoryId: int, itemId: int) =
    member val CategoryId = categoryId with get, set
    member val ItemId = itemId with get, set

type ChannelGroup(id: int, name: string) =
    member val Id = id with get, set
    member val Name = name with get, set

type Channel
    (
        id: int,
        groupId: int option,
        title: string,
        description: string option,
        link: string option,
        url: string,
        imageUrl: string option,
        language: string option
    ) =
    member val Id = id with get, set
    member val GroupId = groupId with get, set
    member val Title = title with get, set
    member val Description = description with get, set
    member val Link = link with get, set
    member val Url = url with get, set
    member val ImageUrl = imageUrl with get, set
    member val Language = language with get, set

type ChannelItem
    (
        id: int64,
        channelId: int,
        itemId: string,
        title: string,
        link: string option,
        thumbnailUrl: string option,
        description: string option,
        content: string option,
        publishingDate: DateTime option,
        isRead: bool,
        isDeleted: bool,
        isFavorite: bool,
        isReadLater: bool,
        categories: string list option
    ) =
    member val Id = id with get, set
    member val ChannelId = channelId with get, set
    member val ItemId = itemId with get, set
    member val Title = title with get, set
    member val Link = link with get, set
    member val ThumbnailUrl = thumbnailUrl with get, set
    member val Description = description with get, set
    member val Content = content with get, set
    member val PublishingDate = publishingDate with get, set
    member val IsRead = isRead with get, set
    member val IsDeleted = isDeleted with get, set
    member val IsFavorite = isFavorite with get, set
    member val IsReadLater = isReadLater with get, set
    member val Categories = categories with get, set

[<Extension>]
type ChannelItemExtensions =
    [<Extension>]
    static member DescriptionText(this: ChannelItem) =
        let text =
            HttpUtility.HtmlDecode(this.Description |> Option.defaultValue this.Title)

        let mutable prevHtml = ""
        let mutable currentHtml = text

        while currentHtml <> prevHtml do
            prevHtml <- currentHtml
            currentHtml <- Regex.Replace(currentHtml, @"<[^>]+>|&nbsp;", "").Trim()

        currentHtml.Substring(0, min currentHtml.Length 200)
