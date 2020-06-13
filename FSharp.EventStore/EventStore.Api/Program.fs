namespace EventStore.Api

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.HttpsPolicy;
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http

module Program =

    let sayHello (context : HttpContext) =
        context.Response.WriteAsync("Hello")

    let configureServices (services : IServiceCollection) =
        services.AddAuthorization()
            |> ignore

    let configureApp (context : WebHostBuilderContext) (app : IApplicationBuilder) =
        if (context.HostingEnvironment.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseRouting()
           .UseAuthorization()
           .UseEndpoints(fun enpoints -> 
                enpoints.MapGet("/hello", RequestDelegate sayHello) |> ignore)
           |> ignore

    [<EntryPoint>]
    let main args =
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun builder ->
                builder.Configure(Action<WebHostBuilderContext, IApplicationBuilder> configureApp) 
                       .ConfigureServices(configureServices)
                    |> ignore )
            .Build()
            .Run()

        0
