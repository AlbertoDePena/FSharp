namespace EventStore.Data

open System
open System.Data
open System.Data.SqlClient
open Dapper
open EventStore.Common

[<RequireQualifiedAccess>]
module Repository =

    let required propertyName value = 
        if String.IsNullOrWhiteSpace(value)
        then raise (EntityValidationException(sprintf "%s is required"  propertyName))
        else value

    let withMaxLength propertyName length (value : string) =
        if value.Length > length
        then raise(EntityValidationException(sprintf "%s cannot be longer than %i" propertyName length))
        else value

    let toNonNegativeInt value =
        if value < 0
        then 0
        else value

    let toNonNegativeLong value =
        if value < 0L
        then 0L
        else value
    
    let private storedProcedure = Nullable CommandType.StoredProcedure

    let private tryOrThrowDatabaseError task =
        try
            task |> Async.AwaitTask
        with
        | :? SqlException as ex -> raise (DatabaseException(ex.Message, ex))

    let getStream : GetStream =
        fun connection (StreamName streamName) ->
            
            let toOption (stream : Entities.Stream) =
                if isNull (box stream)
                then None
                else Some stream

            let param = {| StreamName = streamName |> required "Stream Name" |> withMaxLength "Stream Name" 256 |}

            connection.QuerySingleOrDefaultAsync<Entities.Stream>("dbo.GetStream", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map toOption

    let getAllStreams : GetAllStreams =
        fun connection ->
            connection.QueryAsync<Entities.Stream>("dbo.GetAllStreams", commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let getEvents : GetEvents =
        fun connection (StreamName streamName) (Version version) ->
            let param = {| 
                StreamName = streamName |> required "Stream Name" |> withMaxLength "Stream Name" 256
                StartAtVersion = version |> toNonNegativeInt |}

            connection.QueryAsync<Entities.Event>("dbo.GetEvents", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let getSnapshots : GetSnapshots =
        fun connection (StreamName streamName) ->
            let param = {| StreamName = streamName |> required "Stream Name" |> withMaxLength "Stream Name" 256 |}

            connection.QueryAsync<Entities.Snapshot>("dbo.GetSnapshots", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let addStream : AddStream =
        fun connection transaction stream ->
            let param = {| 
                Name = stream.Name |> required "Stream Name" |> withMaxLength "Stream Name" 256
                Version = stream.Version |> toNonNegativeInt |}

            connection.ExecuteScalarAsync<int64>("dbo.AddStream", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map UniqueId

    let addEvent : AddEvent =
        fun connection transaction event ->
            let param = {| 
                StreamId = event.StreamId |> toNonNegativeLong
                Type = event.Type |> required "Event Type" |> withMaxLength "Event Type" 256
                Data = event.Data |> required "Event Data"
                Version = event.Version |> toNonNegativeInt |}

            connection.ExecuteScalarAsync<int64>("dbo.AddEvent", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map UniqueId

    let addSnapshot : AddSnapshot =
        fun connection snapshot ->
            let param = {| 
                StreamId = snapshot.StreamId |> toNonNegativeLong
                Description = snapshot.Description |> required "Snapshot Description" |> withMaxLength "Snapshot Description" 256
                Data = snapshot.Data |> required "Snapshot Data"
                Version = snapshot.Version |> toNonNegativeInt |}

            connection.ExecuteScalarAsync<int64>("dbo.AddSnapshot", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map UniqueId

    let deleteSnapshots : DeleteSnapshots =
        fun connection (StreamName streamName) ->
            let param = {| StreamName = streamName |> required "Stream Name" |> withMaxLength "Stream Name" 256 |}

            connection.ExecuteAsync("dbo.DeleteSnapshots", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.Ignore

    let updateStream : UpdateStream =
        fun connection transaction stream ->
            let param = {| 
                StreamId = stream.StreamId |> toNonNegativeLong
                Version = stream.Version |> toNonNegativeInt |}

            connection.ExecuteAsync("dbo.UpdateStream", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.Ignore
