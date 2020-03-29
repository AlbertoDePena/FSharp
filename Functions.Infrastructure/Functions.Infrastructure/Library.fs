namespace Numaka.FSharp.Functions.Infrastructure

open System
open System.Net
open System.Net.Http
open System.Security.Claims
open Microsoft.Extensions.Logging

type HttpFunctionContext = {
    Logger : ILogger
    Request : HttpRequestMessage
    GetClaimsPrincipal : HttpFunctionContext -> Async<ClaimsPrincipal option> } 

type GetClaimsPrincipal = HttpFunctionContext -> Async<ClaimsPrincipal option>

type HttpHandler = HttpFunctionContext -> Async<HttpResponseMessage option>

type ErrorHandler = HttpFunctionContext -> exn -> HttpResponseMessage

type HandleRequest = ErrorHandler -> HttpHandler -> HttpFunctionContext -> Async<HttpResponseMessage>

[<RequireQualifiedAccess>]
module Async =
    
    let bind f x = async.Bind(x, f)

    let singleton x = async.Return x

    let map f x = x |> bind (f >> singleton)

[<RequireQualifiedAccess>]
module HttpFunctionContext =

    /// **Description**
    /// 
    /// Bootstrap HttpFunctionContext without GetClaimsPrincipal
    let bootstrap logger request = {
        Logger = logger
        Request = request
        GetClaimsPrincipal = fun _ -> Async.singleton None }

    // **Description**
    /// 
    /// Bootstrap HttpFunctionContext with GetClaimsPrincipal
    let bootstrapWithSecurity logger request getClaimsPrincipal = {
        Logger = logger
        Request = request
        GetClaimsPrincipal = getClaimsPrincipal }

[<RequireQualifiedAccess>]        
module HttpHandler =

    let private getCorsResponse (request : HttpRequestMessage) =
        if String.Equals(request.Method.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase) then
            let response = request.CreateResponse(HttpStatusCode.OK, "Hello from the other side")
            if request.Headers.Contains("Origin") then
                response.Headers.Add("Access-Control-Allow-Credentials", "true")
                response.Headers.Add("Access-Control-Allow-Origin", "*")
                response.Headers.Add("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS, PUT, PATCH, POST, DELETE")
                response.Headers.Add("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept")               
            Some response
        else None

    /// **Description**
    /// 
    /// CORS: handle HTTP OPTIONS requests by setting appropriate access headers
    let cors : HttpHandler =
        fun context ->
            getCorsResponse context.Request
            |> Async.singleton

    /// **Description**
    /// 
    /// Handle HTTP request resulting in a HttpResponseMessage.
    /// 
    /// **Parameters**
    /// 
    /// handleError: ErrorHandler
    /// 
    /// handle: HttpHandler
    /// 
    /// context: HttpFunctionContext
    let handle : HandleRequest = 
        fun handleError handle context ->
            async {
                try
                    let enrichWithCorsOrigin (response : HttpResponseMessage) =
                        response.Headers.Add("Access-Control-Allow-Origin", "*"); response

                    let errorResponse =
                        context.Request.CreateErrorResponse(
                            HttpStatusCode.InternalServerError, "HTTP handler did not yield a response")

                    let! corsResponse = cors context
                    match corsResponse with
                    | Some response -> return response
                    | None -> 
                        let! handlerResponse = handle context
                        match handlerResponse with
                        | Some response -> return enrichWithCorsOrigin response
                        | None -> return errorResponse
                with
                | ex -> return handleError context ex
            }

[<AutoOpen>]
module Extensions =

    type HttpRequestMessage 
        /// Tries to get the Bearer token of the Authorization header
        with member this.TryGetBearerToken () =
                this.Headers 
                |> Seq.tryFind (fun q -> q.Key = "Authorization")
                |> Option.map (fun q -> if Seq.isEmpty q.Value then String.Empty else q.Value |> Seq.head)
                |> Option.map (fun h -> h.Substring("Bearer ".Length).Trim())