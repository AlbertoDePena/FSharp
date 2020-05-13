namespace FSharp.Functions.FunctionApp

open FSharp.Functions.Infrastructure
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open System.Security.Claims
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc

type GetClaimsPrincipal = ILogger -> HttpRequest -> Async<ClaimsPrincipal>

module Program =

    let errorHandler : ErrorHandler =
        fun logger ex ->
            logger.LogError(ex, ex.Message)
            BadRequestObjectResult("Testing custom error handler") :> IActionResult

    let getClaimsPrincipal : GetClaimsPrincipal =
        fun logger request ->
            let bearerToken = 
                request.TryGetBearerToken ()
                |> Option.defaultWith (fun _ -> invalidOp "Bearer token is required")

            logger.LogInformation(sprintf "Bearer Token: %s" bearerToken)

            let claims = [Claim(ClaimTypes.Name, "Test User")]                
            let identity = ClaimsIdentity(claims, "Bearer")

            identity |> ClaimsPrincipal |> Async.singleton

    let helloWorldHandler : HttpHandler =
        fun logger request ->
            logger.LogInformation("Handling HelloWorld request...")
            OkObjectResult("Hello World!") :> IActionResult
            |> Async.singleton

    let currentUserHandler : HttpHandler =
        fun logger request -> 
            async {
                logger.LogInformation("Handling CurrentUser request...")

                let! claimsPrincipal = getClaimsPrincipal logger request

                let result = 
                    OkObjectResult(sprintf "The current user is: %s" claimsPrincipal.Identity.Name) :> IActionResult

                return result
            }

    let testRequestExtensionsHandler : HttpHandler =
        fun logger request ->
            let queryStringValue = 
                request.TryGetQueryStringValue "name"
                |> Option.defaultValue "N/A"

            let headerValue =
                request.TryGetHeaderValue "X-Test"
                |> Option.defaultValue "N/A"

            let data = {| QueryStringValue = queryStringValue; HeaderValue = headerValue |}

            OkObjectResult(data) :> IActionResult
            |> Async.singleton 

    [<FunctionName("HelloWorld")>]
    let helloWorld ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequest) (logger : ILogger) =                                           
        handle helloWorldHandler logger request
        |> Async.StartAsTask 

    [<FunctionName("CurrentUser")>]
    let currentUser ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequest) (logger : ILogger) =         
        handleWith errorHandler currentUserHandler logger request
        |> Async.StartAsTask 

    [<FunctionName("TestRequestExtensions")>]
    let testRequestExtensions ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>] request : HttpRequest) (logger : ILogger) =        
        handle testRequestExtensionsHandler logger request
        |> Async.StartAsTask   