namespace EventStore.Data

open System
open System.Data
open System.Data.SqlClient
open Dapper
open EventStore.Common

[<RequireQualifiedAccess>]
module Repository =

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

            let param = {| StreamName = streamName |}

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
            let param = {| StreamName = streamName; StartAtVersion = version |}

            connection.QueryAsync<Entities.Event>("dbo.GetEvents", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let getSnapshots : GetSnapshots =
        fun connection (StreamName streamName) ->
            let param = {| StreamName = streamName |}

            connection.QueryAsync<Entities.Snapshot>("dbo.GetSnapshots", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let addStream : AddStream =
        fun connection transaction stream ->
            let param = {| Name = stream.Name; Version = stream.Version |}

            connection.ExecuteScalarAsync<int64>("dbo.AddStream", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map UniqueId

    let addEvent : AddEvent =
        fun connection transaction event ->
            let param = {| 
                StreamId = event.StreamId
                Type = event.Type
                Data = event.Data
                Version = event.Version |}

            connection.ExecuteScalarAsync<int64>("dbo.AddEvent", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map UniqueId

    let addSnapshot : AddSnapshot =
        fun connection snapshot ->
            let param = {| 
                StreamId = snapshot.StreamId
                Description = snapshot.Description
                Data = snapshot.Data
                Version = snapshot.Version |}

            connection.ExecuteScalarAsync<int64>("dbo.AddSnapshot", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map UniqueId

    let deleteSnapshots : DeleteSnapshots =
        fun connection (StreamName streamName) ->
            let param = {| StreamName = streamName |}

            connection.ExecuteAsync("dbo.DeleteSnapshots", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.Ignore

    let updateStream : UpdateStream =
        fun connection transaction stream ->
            let param = {| StreamId = stream.StreamId; Version = stream.Version |}

            connection.ExecuteAsync("dbo.UpdateStream", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.Ignore
