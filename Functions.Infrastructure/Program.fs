namespace FSharp.Functions.FunctionApp

open FSharp.Functions.Infrastructure
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open System.Net.Http
open System.Net
open System.Security.Claims

module Program =

    let errorHandler : ErrorHandler =
        fun logger request ex ->
            logger.LogError(ex, ex.Message)
            request.CreateErrorResponse(HttpStatusCode.BadRequest, "Testing custom error handler")

    let getClaimsPrincipal : GetClaimsPrincipal =
        fun logger request ->
            let bearerToken = 
                request.TryGetBearerToken ()
                |> Option.defaultWith (fun _ -> invalidOp "Bearer token is required")

            logger.LogInformation(sprintf "Bearer Token: %s" bearerToken)

            let claims = [Claim(ClaimTypes.Name, "Test User")]                
            let identity = ClaimsIdentity(claims, "Bearer")

            identity |> ClaimsPrincipal |> Some |> Async.singleton

    let helloWorldHandler : HttpHandler =
        handleContext (
            fun context ->
                context.Logger.LogInformation("Handling HelloWorld request...")
                context.Request.CreateResponse(HttpStatusCode.OK, "Hello World!")
                |> context.ToFuncResult |> Async.singleton)

    let helloLazHandler : HttpHandler =
        handleContext (
            fun context -> 
                context.Logger.LogInformation("Handling HelloLaz request...")
                context.Request.CreateResponse(HttpStatusCode.OK, "Hello Laz!")
                |> context.ToFuncResult |> Async.singleton)

    let currentUserHandler : HttpHandler =
        handleContext (
            fun context -> 
                async {
                    context.Logger.LogInformation("Handling CurrentUser request...")

                    let user = 
                        context.ClaimsPrincipal
                        |> Option.map (fun principal -> principal.Identity.Name)
                        |> Option.defaultWith (fun _ -> invalidOp "ClaimsPrincipal not available")

                    let result = 
                        context.Request.CreateResponse(HttpStatusCode.OK, sprintf "The current user is: %s" user)
                        |> context.ToFuncResult

                    return result
                })

    let testRequestExtensionsHandler : HttpHandler =
        handleContext (
            fun context ->
                let queryStringValue = 
                    context.Request.TryGetQueryStringValue "name"
                    |> Option.defaultValue "N/A"

                let headerValue =
                    context.Request.TryGetHeaderValue "X-Test"
                    |> Option.defaultValue "N/A"

                let data = {| QueryStringValue = queryStringValue; HeaderValue = headerValue |}

                context.Request.CreateResponse(HttpStatusCode.OK, data)
                |> context.ToFuncResult |> Async.singleton)

    [<FunctionName("HelloWorld")>]
    let helloWorld ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =                                           
        let handler = cors >=> helloWorldHandler

        handleRequest handler logger request
        |> Async.StartAsTask 

    [<FunctionName("HelloLaz")>]
    let helloLaz ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =                
        handleRequest helloLazHandler logger request
        |> Async.StartAsTask   

    [<FunctionName("CurrentUser")>]
    let currentUser ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =         
        let handler = cors >=> security getClaimsPrincipal >=> currentUserHandler
        
        handleRequestWith errorHandler handler logger request
        |> Async.StartAsTask 

    [<FunctionName("TestRequestExtensions")>]
    let testRequestExtensions ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>] request : HttpRequestMessage) (logger : ILogger) =        
        handleRequest testRequestExtensionsHandler logger request
        |> Async.StartAsTask   