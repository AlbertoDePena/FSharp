namespace EventStore.Data

open System
open System.Data
open Dapper
open EventStore.Common

[<RequireQualifiedAccess>]
module Repository =
    
    let private storedProcedure = Nullable CommandType.StoredProcedure

    let private tryOrThrowDatabaseError task =
        
        let handleChoice choice =
            match choice with
            | Choice1Of2 data -> data
            | Choice2Of2 (error : exn) -> raise (DatabaseException(error.Message, error))

        task
        |> Async.AwaitTask
        |> Async.Catch
        |> Async.map handleChoice

    let getStream : GetStream =
        fun connection streamName ->
            
            let toOption (stream : Stream) =
                if isNull (box stream)
                then None
                else Some stream

            let param = {| StreamName = streamName |}

            connection.QuerySingleOrDefaultAsync<Stream>("dbo.GetStream", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map toOption

    let getAllStreams : GetAllStreams =
        fun connection ->
            connection.QueryAsync<Stream>("dbo.GetAllStreams", commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let getEvents : GetEvents =
        fun connection streamName startAtVersion ->
            let param = {| StreamName = streamName; StartAtVersion = startAtVersion |}

            connection.QueryAsync<Event>("dbo.GetEvents", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let getSnapshots : GetSnapshots =
        fun connection streamName ->
            let param = {| StreamName = streamName |}

            connection.QueryAsync<Snapshot>("dbo.GetSnapshots", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.map Seq.toList

    let addStream : AddStream =
        fun connection transaction stream ->
            let param = {| Name = stream.Name; Version = stream.Version |}

            connection.ExecuteScalarAsync<int64>("dbo.AddStream", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError

    let addEvents : AddEvents =
        fun connection transaction events ->
            let param = events |> List.map (fun x -> {| StreamId = x.StreamId; Type = x.Type; Data = x.Data; Version = x.Version  |})

            connection.ExecuteAsync("dbo.AddEvent", param, transaction, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.Ignore

    let addSnapshot : AddSnapshot =
        fun connection snapshot ->
            let param = {| StreamId = snapshot.StreamId; Description = snapshot.Description; Data = snapshot.Data; Version = snapshot.Version |}

            connection.ExecuteAsync("dbo.AddSnapshot", param, commandType = storedProcedure)
            |> tryOrThrowDatabaseError
            |> Async.Ignore

    let deleteSnapshots : DeleteSnapshots =
        fun connection streamName ->
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
