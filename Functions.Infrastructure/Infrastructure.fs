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

type HttpMiddleware = {
    Next : HttpMiddleware option
    Invoke : HttpMiddleware option -> HttpFunctionContext -> Async<HttpFunctionContext> }

type HttpRequestInvoker = HttpMiddleware option -> HttpFunctionContext -> Async<HttpFunctionContext>

type HttpRequestHandler = HttpMiddleware list -> HttpFunctionContext -> Async<HttpResponseMessage>

[<AutoOpen>]
module Core =

    let bootstrapHttpContext logger request = {
        Logger = logger
        Request = request
        Response = None
        ClaimsPrincipal = None }

    let handleHttpRequest : HttpRequestHandler =
        fun middlewares context ->
            let pipeline = ResizeArray()

            let register (middleware : HttpMiddleware) =
                let count = pipeline.Count
                if count > 0 then
                    let item = pipeline.[count - 1]
                    let updated = { item with Next = Some middleware } 
                    pipeline.[count - 1] <- updated

                pipeline.Add(middleware)

            let errorResponse context (ex : exn) =
                context.Logger.LogError(ex, ex.Message)
                context.Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError, ex)

            async {
                try
                    middlewares |> List.iter register

                    if pipeline.Count = 0 then failwith "Pipeline has no middlewares"

                    let middleware = pipeline.[0]
                    let! context = middleware.Invoke middleware.Next context

                    match context.Response with
                    | Some response -> return response
                    | None -> return failwith "HTTP middleware did not yield a response"
                with
                | ex -> return errorResponse context ex
            }

[<RequireQualifiedAccess>]        
module Middlewares =

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

    let cors : HttpMiddleware =
        
        let invoke : HttpRequestInvoker =
            fun next context ->
                async {
                    match handleOptionsRequest context.Request with
                    | Some response -> return { context with Response = Some response }
                    | None -> 
                        match next with
                        | None -> return failwith "No HTTP middleware found"
                        | Some next ->
                            let! context = next.Invoke next.Next context
                            return { context with Response = context.Response |> Option.map enrichWithCorsOrigin }
                }
        
        { Next = None; Invoke = invoke }

    let security (getClaimsPrincipal : GetClaimsPrincipal) : HttpMiddleware =
        
        let invoke : HttpRequestInvoker =
            fun next context ->
                async {
                    let! claimsPrincipal = getClaimsPrincipal context.Logger context.Request
                    let context = { context with ClaimsPrincipal = claimsPrincipal }
                    match next with
                    | None -> return context
                    | Some next ->
                        return! next.Invoke next.Next context
                }
        
        { Next = None; Invoke = invoke }
        