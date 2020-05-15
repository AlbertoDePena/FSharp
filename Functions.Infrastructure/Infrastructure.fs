namespace FSharp.Functions.Infrastructure

open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open System.Web.Http
open System

type HttpHandler = ILogger -> HttpRequest -> Async<IActionResult>

type ErrorHandler = ILogger -> exn -> IActionResult

[<RequireQualifiedAccess>]
module Async =
    
    let bind f x = async.Bind(x, f)

    let singleton x = async.Return x

    let map f x = x |> bind (f >> singleton)

[<AutoOpen>]
module Core =

    type HttpRequest with

        /// Try to get the Bearer token from the Authorization header
        member this.TryGetBearerToken () =
            this.Headers 
            |> Seq.tryFind (fun q -> q.Key = "Authorization")
            |> Option.map (fun q -> if Seq.isEmpty q.Value then String.Empty else q.Value |> Seq.head)
            |> Option.map (fun h -> h.Substring("Bearer ".Length).Trim())

        member this.TryGetQueryStringValue (name : string) =
            let hasValue, values = this.Query.TryGetValue(name)
            if hasValue
            then values |> Seq.tryHead
            else None

        member this.TryGetHeaderValue (name : string) =
            let hasHeader, values = this.Headers.TryGetValue(name)
            if hasHeader
            then values |> Seq.tryHead
            else None

    let private errorHandler : ErrorHandler =
        fun logger ex ->
            logger.LogError(ex, ex.Message)
            InternalServerErrorResult() :> IActionResult
    
    /// Handle HTTP request with a custom error handler.
    let handleHttpRequestWith (errorHandler : ErrorHandler) (httpHandler : HttpHandler) (logger : ILogger) (request : HttpRequest) =

        let toActionResult choice =
            match choice with
            | Choice1Of2 actionResult -> actionResult
            | Choice2Of2 error -> errorHandler logger error

        if String.Equals(request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase) then
            OkObjectResult("Hello from the other side")
            :> IActionResult
            |> Async.singleton
        else
            httpHandler logger request
            |> Async.Catch
            |> Async.map toActionResult

    /// Handle HTTP request.
    let handleHttpRequest httpHandler =
        handleHttpRequestWith errorHandler httpHandler
        