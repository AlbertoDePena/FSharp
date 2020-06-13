namespace EventStore.Common

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module Async =
    
    let bind f x = async.Bind(x, f)

    let singleton x = async.Return x

    let map f x = x |> bind (f >> singleton)

    /// <summary>
    /// Async.StartAsTask and up-cast from Task<unit> to plain Task.
    /// </summary>
    /// <param name="task">The asynchronous computation.</param>
    let AsTask (task : Async<unit>) = Async.StartAsTask task :> Task

