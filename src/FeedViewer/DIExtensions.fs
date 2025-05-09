namespace Microsoft.Extensions.DependencyInjection

open System.Runtime.CompilerServices
open Photino.Blazor
open Fun.Blazor

[<Extension>]
type FunBlazorWasmExtensions =

    [<Extension>]
    static member AddFunBlazor(this: BlazorWindowRootComponents, selector: string, node: NodeRenderFragment) =
        let parameters = dict [ "Fragment", box node ]
        this.Add(typeof<FunFragmentComponent>, selector, parameters)
        this
