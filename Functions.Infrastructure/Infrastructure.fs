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
    Response : HttpResponseMessage option
    ClaimsPrincipal : ClaimsPrincipal option }
    with member this.ToFuncResult response = Some { this with Response = Some response }

type HttpFuncResult = Async<HttpFunctionContext option>

type HttpFunc = HttpFunctionContext -> HttpFuncResult

type HttpHandler = HttpFunc -> HttpFunc

type ErrorHandler = ILogger -> HttpRequestMessage -> exn -> HttpResponseMessage

[<AutoOpen>]
module Core =

    let private bootstrapContext logger request = {
        Logger = logger
        Request = request
        Response = None
        ClaimsPrincipal = None }

    let private defaultErrorHandler : ErrorHandler =
        fun logger request ex ->
            logger.LogError(ex, ex.Message)
            request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex)

    let handleContext (contextMap : HttpFunctionContext -> HttpFuncResult) : HttpHandler =
        fun (next : HttpFunc) (context : HttpFunctionContext) ->
            async {
                match! contextMap context with
                | Some c ->
                    match c.Response with
                    | Some _ -> return Some c
                    | None -> return! next c
                | None -> return  None
            }

    let compose (handler1 : HttpHandler) (handler2 : HttpHandler) : HttpHandler =
        fun (final : HttpFunc) ->
            let func = final |> handler2 |> handler1
            fun (context : HttpFunctionContext) ->
                match context.Response with
                | Some _ -> final context
                | None -> func context

    let (>=>) = compose

    /// Handle HTTP request with a custom error handler.
    let handleRequestWith (errorHandler : ErrorHandler) (handler : HttpHandler) (logger : ILogger) (request : HttpRequestMessage) =
        async {
            let context = bootstrapContext logger request
            try
                let func : HttpFunc = handler (Some >> Async.singleton)                
                let! result = func context
                match result with
                | None -> return failwith "HTTP function context not available"
                | Some ctx ->
                    match ctx.Response with
                    | None -> return failwith "HTTP handler did not yield a response"
                    | Some response -> return response 
            with 
            | ex -> return errorHandler context.Logger context.Request ex
        }

    /// Handle HTTP request.
    let handleRequest handler logger request =
        handleRequestWith defaultErrorHandler handler logger request

[<AutoOpen>]        
module HttpHandlers =

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

    /// Handle HTTP OPTIONS request by setting all CORS headers.
    /// 
    /// When HTTP request is not OPTIONS, enrich response with CORS origin.
    let cors : HttpHandler =
        fun next context ->
            async {
                match handleOptionsRequest context.Request with
                | Some response -> return context.ToFuncResult response
                | None -> 
                    let! context = next context
                    return context |> Option.map (fun context -> { context with Response = context.Response |> Option.map enrichWithCorsOrigin })
            }

    /// Set HTTP function context's claims principal.
    let security (getClaimsPrincipal : GetClaimsPrincipal) : HttpHandler =
        fun next context ->
            async {
                let! claimsPrincipal = getClaimsPrincipal context.Logger context.Request
                return! next { context with ClaimsPrincipal = claimsPrincipal }
            }
        