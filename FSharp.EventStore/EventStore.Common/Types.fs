namespace EventStore.Common

open System

type StreamName = string

type StartAtVersion = int32

type NonEmptyString = private NonEmptyString of string

type NonNegativeInt = private NonNegativeInt of int32

type NonNegativeLong = private NonNegativeLong of int64

[<RequireQualifiedAccess>]
module NonEmptyString =

    let value (NonEmptyString x) = x

    let createOptional x = 
        if String.IsNullOrWhiteSpace(x)
        then None
        else NonEmptyString x |> Some

[<RequireQualifiedAccess>]
module NonNegativeInt =
    
    let value (NonNegativeInt x) = x

    let createOptional x =
        if x < 0
        then None
        else NonNegativeInt x |> Some

[<RequireQualifiedAccess>]
module NonNegativeLong =
    
    let value (NonNegativeLong x) = x

    let createOptional x =
        if x < 0L
        then None
        else NonNegativeLong x |> Some