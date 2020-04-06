namespace FSharp.Functions.Infrastructure

open System
open System.Net.Http

[<RequireQualifiedAccess>]
module Async =
    
    let bind f x = async.Bind(x, f)

    let singleton x = async.Return x

    let map f x = x |> bind (f >> singleton)

[<AutoOpen>]
module Extensions =

    type HttpRequestMessage with

        member this.TryGetBearerToken () =
            this.Headers 
            |> Seq.tryFind (fun q -> q.Key = "Authorization")
            |> Option.map (fun q -> if Seq.isEmpty q.Value then String.Empty else q.Value |> Seq.head)
            |> Option.map (fun h -> h.Substring("Bearer ".Length).Trim())

        member this.TryGetQueryStringValue (name : string) =
            let value = this.RequestUri.ParseQueryString().Get(name)
            if String.IsNullOrWhiteSpace(value)
            then None
            else Some value

        member this.TryGetHeaderValue (name : string) =
            let hasHeader, values = this.Headers.TryGetValues(name)
            if hasHeader
            then values |> Seq.tryHead
            else None