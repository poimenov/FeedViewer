namespace CodeHollow.FeedReader

open System
open System.Runtime.CompilerServices
open System.Xml.Linq
open Microsoft.FSharp.Core
open CodeHollow.FeedReader.Feeds.MediaRSS
open FSharp.Data

[<Extension>]
type FeedExtensions =
    [<Extension>]
    static member Namespaces(this: Feed) =
        let doc = XDocument.Load this.OriginalDocument

        doc.Root.Attributes()
        |> Seq.cast<XAttribute>
        |> Seq.filter (fun x -> x.IsNamespaceDeclaration)
        |> Seq.map (fun x -> x.ToString())

[<Extension>]
type MediaGroupExtensions() =
    [<Literal>]
    static let mrss = "http://search.yahoo.com/mrss/"

    [<Extension>]
    static member GetDescription(this: MediaGroup) =
        let nameTitle = XName.Get("description", mrss)

        let description =
            this.Element.Elements() |> Seq.tryFind (fun x -> x.Name = nameTitle)

        match description with
        | Some x -> Some x.Value
        | None -> None

    [<Extension>]
    static member GetThumbnailUrl(this: MediaGroup) =
        let nameThumbnail = XName.Get("thumbnail", mrss)

        let thumbnail =
            this.Element.Elements() |> Seq.tryFind (fun x -> x.Name = nameThumbnail)

        if thumbnail.IsSome then
            let urlAtt =
                thumbnail.Value.Attributes()
                |> Seq.cast<XAttribute>
                |> Seq.tryFind (fun x -> x.Name.LocalName = "url")

            if urlAtt.IsSome then Some urlAtt.Value.Value else None
        else
            None

[<Extension>]
type FeeItemExtensions =
    [<Literal>]
    static let mrss = "http://search.yahoo.com/mrss/"

    [<Extension>]
    static member GetPublishingDate(this: FeedItem) =
        if this.PublishingDate.HasValue then
            Some this.PublishingDate.Value
        else
            None

    [<Extension>]
    static member GetCategories(this: FeedItem) =
        if this.Categories.Count > 0 then
            Some(this.Categories |> Seq.toList)
        else
            None

    [<Extension>]
    static member GetElements(this: FeedItem) =
        this.SpecificItem.Element.Elements() |> Seq.cast<XElement>

    [<Extension>]
    static member GetMediaGroup(this: FeedItem) =
        let nameGroup = XName.Get("group", mrss)

        this.GetElements()
        |> Seq.tryFind (fun x -> x.Name = nameGroup)
        |> Option.map (fun x -> MediaGroup(x))

    [<Extension>]
    static member GetThumbnailUrl(this: FeedItem) =
        let nameThumbnail = XName.Get("thumbnail", mrss)

        let thumbnail =
            this.GetElements()
            |> Seq.tryFind (fun x -> x.Name = nameThumbnail)
            |> Option.map (fun x -> x.Attribute(XName.Get "url").Value)

        let nameImage = XName.Get("content", mrss)

        let image =
            this.GetElements()
            |> Seq.tryFind (fun x -> x.Name = nameImage && x.Attribute(XName.Get "medium").Value = "image")
            |> Option.map (fun x -> x.Attribute(XName.Get "url").Value)

        let thumbFromMediaGroup =
            let mediaGroup = this.GetMediaGroup()

            match mediaGroup with
            | Some g -> g.GetThumbnailUrl()
            | None -> None

        let imageFromDescription =
            if String.IsNullOrWhiteSpace this.Description then
                None
            else
                try
                    let doc = HtmlDocument.Parse this.Description
                    let img = doc.Descendants "img" |> Seq.tryHead

                    if img.IsSome then
                        Some(img.Value.AttributeValue "src")
                    else
                        None
                with _ ->
                    None

        match thumbnail, image, thumbFromMediaGroup, imageFromDescription with
        | Some t, _, _, _ -> Some t
        | _, Some i, _, _ -> Some i
        | _, _, Some g, _ -> Some g
        | _, _, _, Some i -> Some i
        | _ -> None

    [<Extension>]
    static member GetMedia(this: FeedItem) =
        let nameMedia = XName.Get("content", mrss)

        let media = this.GetElements() |> Seq.filter (fun x -> x.Name = nameMedia)

        if media |> Seq.length > 0 then
            media |> Seq.map (fun x -> Media(x)) |> Some
        else
            None

    [<Extension>]
    static member GetYoutubeVideoId(this: FeedItem) =
        let nameVideoId = XName.Get("videoId", "http://www.youtube.com/xml/schemas/2015")

        this.GetElements()
        |> Seq.tryFind (fun x -> x.Name = nameVideoId)
        |> Option.map (fun x -> x.Value)

    [<Extension>]
    static member GetTurboContent(this: FeedItem) =
        let nameTurboContent = XName.Get("content", "http://turbo.yandex.ru")

        this.GetElements()
        |> Seq.tryFind (fun x -> x.Name = nameTurboContent)
        |> Option.map (fun x -> x.Value)

    [<Extension>]
    static member GetContent(this: FeedItem) =
        let media = this.GetMedia()

        if media.IsSome && media.Value |> Seq.exists (fun m -> m.Medium = Medium.Image) then
            let img = media.Value |> Seq.find (fun m -> m.Medium = Medium.Image)

            let sContent =
                if String.IsNullOrWhiteSpace this.Content then
                    this.Description
                else
                    this.Content

            $"<div><img src=\"{img.Url}\"></div><div>{sContent}</div>"
        else
            let id = this.GetYoutubeVideoId()

            if id.IsSome then
                let group = this.GetMediaGroup()

                let description =
                    if group.IsSome then
                        group.Value.GetDescription() |> Option.defaultValue this.Description
                    else
                        this.Description

                let frameStyle =
                    let defaultStyle = "width:100%;aspect-ratio:16 / 9;"

                    if
                        media.IsSome
                        && media.Value |> Seq.exists (fun m -> m.Type = "application/x-shockwave-flash")
                    then
                        let video =
                            media.Value |> Seq.find (fun m -> m.Type = "application/x-shockwave-flash")

                        if video.Width.HasValue && video.Height.HasValue then
                            $"width:{video.Width.Value}px;height:{video.Height.Value}px;"
                        else
                            defaultStyle
                    else
                        defaultStyle

                $"<div><iframe data-clean=\"yes\" style=\"border: 0px;{frameStyle}\"
                src=\"https://www.youtube.com/embed/{id.Value}\"title=\"{this.Title}\" 
                allowfullscreen=\"\"></iframe></div><div class=\"pre\">{description}</div>"
            else
                let tContent = this.GetTurboContent()
                if tContent.IsSome then tContent.Value else this.Content
