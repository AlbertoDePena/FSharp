namespace FSharp.Functions.Infrastructure

open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open System.Web.Http
open System

type HttpHandler = ILogger -> HttpRequest -> Async<IActionResult>

type ErrorHandler = ILogger -> exn -> IActionResult

[<AutoOpen>]
module Core =

    let private defaultErrorHandler : ErrorHandler =
        fun logger ex ->
            logger.LogError(ex, ex.Message)
            InternalServerErrorResult() :> IActionResult
    
    /// Handle HTTP request with a custom error handler.
    let handleWith (errorHandler : ErrorHandler) (httpHandler : HttpHandler) (logger : ILogger) (request : HttpRequest) =

        let handleChoice choice =
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
            |> Async.map handleChoice

    /// Handle HTTP request.
    let handle httpHandler =
        handleWith defaultErrorHandler httpHandler
        