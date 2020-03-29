namespace Numaka.FSharp.Functions.FunctionApp

open Numaka.FSharp.Functions.Infrastructure
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
        fun context ->
            async {
                let bearerToken = 
                    context.Request.TryGetBearerToken ()
                    |> Option.defaultWith (fun _ -> invalidOp "Bearer token is required")

                //TODO: validate bearerToken
                context.Logger.LogInformation(sprintf "Bearer Token: %s" bearerToken)

                let claims = [Claim(ClaimTypes.Name, "Test User")]                
                let identity = ClaimsIdentity(claims, "Bearer")

                return identity |> ClaimsPrincipal |> Some 
            }

    let helloWorldHandler : HttpHandler =
        fun context -> 
            async {
                context.Logger.LogInformation("Handling HelloWorld request...")

                let response = context.Request.CreateResponse(HttpStatusCode.OK, "Hello World!")

                return Some response
            }

    let helloLazHandler : HttpHandler =
        fun context -> 
            async {
                context.Logger.LogInformation("Handling HelloLaz request...")

                let response = context.Request.CreateResponse(HttpStatusCode.OK, "Hello Laz!")

                return Some response
            }

    let currentUserHandler : HttpHandler =
        fun context -> 
            async {
                context.Logger.LogInformation("Handling CurrentUser request...")

                let! claimsPrincipal = context.GetClaimsPrincipal context

                let user = 
                    claimsPrincipal
                    |> Option.map (fun principal -> principal.Identity.Name)
                    |> Option.defaultWith (fun _ -> invalidOp "ClaimsPrincipal not available")

                let response = context.Request.CreateResponse(HttpStatusCode.OK, sprintf "The current user is: %s" user)

                return Some response
            }

    [<FunctionName("HelloWorld")>]
    let helloWorld ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =                                           
        HttpFunctionContext.bootstrap logger request
        |> HttpHandler.handle errorHandler helloWorldHandler
        |> Async.StartAsTask

    [<FunctionName("HelloLaz")>]
    let helloLaz ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =        
        HttpFunctionContext.bootstrap logger request
        |> HttpHandler.handle errorHandler helloLazHandler
        |> Async.StartAsTask    

    [<FunctionName("CurrentUser")>]
    let currentUser ([<HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")>] request : HttpRequestMessage) (logger : ILogger) =         
        HttpFunctionContext.bootstrapWithSecurity logger request getClaimsPrincipal
        |> HttpHandler.handle errorHandler currentUserHandler
        |> Async.StartAsTask      