namespace FSharp.Functions.Infrastructure

open System
open System.Net
open System.Net.Http
open System.Security.Claims
open Microsoft.Extensions.Logging

type GetClaimsPrincipal = ILogger -> HttpRequestMessage -> Async<ClaimsPrincipal option>

type HttpFunctionContext = {
    Logger : ILogger
    Request : HttpRequestMessage
    GetClaimsPrincipal : GetClaimsPrincipal }

type HttpHandler = HttpFunctionContext -> Async<HttpResponseMessage option>

type ErrorHandler = HttpFunctionContext -> exn -> HttpResponseMessage

[<RequireQualifiedAccess>]
module HttpFunctionContext =

    let bootstrapWith logger request getClaimsPrincipal = {
        Logger = logger
        Request = request
        GetClaimsPrincipal = getClaimsPrincipal }

    let bootstrap logger request = 
        bootstrapWith logger request (fun _ _ -> Async.singleton None)

[<RequireQualifiedAccess>]        
module HttpHandler =

    let private handleOptionsRequest (request : HttpRequestMessage) =
        if String.Equals(request.Method.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase) then
            let response = request.CreateResponse(HttpStatusCode.OK, "Hello from the other side")
            if request.Headers.Contains("Origin") then
                response.Headers.Add("Access-Control-Allow-Credentials", "true")
                response.Headers.Add("Access-Control-Allow-Origin", "*")
                response.Headers.Add("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS, PUT, PATCH, POST, DELETE")
                response.Headers.Add("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept")               
            Some response
        else None

    let private enrichWithCorsOrigin (response : HttpResponseMessage) =
        response.Headers.Add("Access-Control-Allow-Origin", "*"); response

    let private errorResponse context =
        context.Request.CreateErrorResponse(
            HttpStatusCode.InternalServerError, "HTTP handler did not yield a response")

    let handleWith handleError (handle : HttpHandler) context =
        async {
            try
                match handleOptionsRequest context.Request with
                | Some response -> return response
                | None -> 
                    let! handlerResponse = handle context
                    match handlerResponse with
                    | Some response -> return enrichWithCorsOrigin response
                    | None -> return errorResponse context
            with
            | ex -> return handleError context ex
        }

    let handle =
        let handleError context (ex : exn) =
            context.Logger.LogError(ex.Message)
            context.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex)

        handleWith handleError
        