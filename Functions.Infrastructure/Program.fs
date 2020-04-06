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
        fun context ex ->
            context.Logger.LogError(ex.Message)
            context.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex)

    let getClaimsPrincipal : GetClaimsPrincipal =
        fun logger request ->
            let bearerToken = 
                request.TryGetBearerToken ()
                |> Option.defaultWith (fun _ -> invalidOp "Bearer token is required")

            //TODO: validate bearerToken
            logger.LogInformation(sprintf "Bearer Token: %s" bearerToken)

            let claims = [Claim(ClaimTypes.Name, "Test User")]                
            let identity = ClaimsIdentity(claims, "Bearer")

            identity |> ClaimsPrincipal |> Some |> Async.singleton

    let helloWorldHandler : HttpHandler =
        fun context -> 
            context.Logger.LogInformation("Handling HelloWorld request...")
            context.Request.CreateResponse(HttpStatusCode.OK, "Hello World!")
            |> Some |> Async.singleton

    let helloLazHandler : HttpHandler =
        fun context -> 
            context.Logger.LogInformation("Handling HelloLaz request...")
            context.Request.CreateResponse(HttpStatusCode.OK, "Hello Laz!")
            |> Some |> Async.singleton

    let currentUserHandler : HttpHandler =
        fun context -> 
            async {
                context.Logger.LogInformation("Handling CurrentUser request...")

                let! claimsPrincipal = context.GetClaimsPrincipal context.Logger context.Request

                let user = 
                    claimsPrincipal
                    |> Option.map (fun principal -> principal.Identity.Name)
                    |> Option.defaultWith (fun _ -> invalidOp "ClaimsPrincipal not available")

                let response = context.Request.CreateResponse(HttpStatusCode.OK, sprintf "The current user is: %s" user)

                return Some response
            }

    let testRequestExtensionsHandler : HttpHandler =
        fun context ->
            let queryStringValue = 
                context.Request.TryGetQueryStringValue "name"
                |> Option.defaultValue "N/A"

            let headerValue =
                context.Request.TryGetHeaderValue "X-Test"
                |> Option.defaultValue "N/A"

            let data = {| QueryStringValue = queryStringValue; HeaderValue = headerValue |}

            context.Request.CreateResponse(HttpStatusCode.OK, data)
            |> Some |> Async.singleton

    [<FunctionName("HelloWorld")>]
    let helloWorld ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =                                           
        HttpFunctionContext.bootstrap logger request
        |> HttpHandler.handle helloWorldHandler
        |> Async.StartAsTask

    [<FunctionName("HelloLaz")>]
    let helloLaz ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =        
        HttpFunctionContext.bootstrap logger request
        |> HttpHandler.handle helloLazHandler
        |> Async.StartAsTask    

    [<FunctionName("CurrentUser")>]
    let currentUser ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =         
        HttpFunctionContext.bootstrapWith logger request getClaimsPrincipal
        |> HttpHandler.handleWith errorHandler currentUserHandler
        |> Async.StartAsTask   

    [<FunctionName("TestRequestExtensions")>]
    let testRequestExtensions ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =        
        HttpFunctionContext.bootstrap logger request
        |> HttpHandler.handle testRequestExtensionsHandler
        |> Async.StartAsTask    