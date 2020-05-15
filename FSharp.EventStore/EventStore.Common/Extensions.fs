namespace EventStore.Common

[<RequireQualifiedAccess>]
module Async =
    
    let bind f x = async.Bind(x, f)

    let singleton x = async.Return x

    let map f x = x |> bind (f >> singleton)

