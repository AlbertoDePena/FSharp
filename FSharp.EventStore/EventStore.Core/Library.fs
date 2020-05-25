namespace EventStore.Core

open System
open EventStore.Common
open EventStore.Data
open System.Data.SqlClient
open System.Data

[<RequireQualifiedAccess>]
module Service =
    
    let getStream : EventStore.Core.GetStream =
        fun getDbConnection dbConnectionString repository streamName ->
            async {
                use! connection = getDbConnection dbConnectionString

                let! stream = 
                    streamName
                    |> Validation.validateStreamName
                    |> repository.GetStream connection

                return stream |> Option.map Mapper.toStream
            }

    let getAllStreams : EventStore.Core.GetAllStreams =
        fun getDbConnection dbConnectionString repository ->
            async {
                use! connection = getDbConnection dbConnectionString

                let! streams =
                    repository.GetAllStreams connection

                return streams |> List.map Mapper.toStream
            }

    let getEvents : EventStore.Core.GetEvents =
        fun getDbConnection dbConnectionString repository streamName startAtVersion ->
            async {
                let streamName = streamName |>  Validation.validateStreamName 
                
                use! connection = getDbConnection dbConnectionString

                let! events =
                    repository.GetEvents connection streamName startAtVersion

                return events |> List.map Mapper.toEvent
            }

    let getSnapshots : EventStore.Core.GetSnapshots =
        fun getDbConnection dbConnectionString repository streamName ->
            async {
                use! connection = getDbConnection dbConnectionString

                let! snapshots =
                    streamName
                    |> Validation.validateStreamName
                    |> repository.GetSnapshots connection

                return snapshots |> List.map Mapper.toSnapshot
            }

    let deleteSnapshots : EventStore.Core.DeleteSnapshots =
        fun getDbConnection dbConnectionString repository streamName ->
            async {
                use! connection = getDbConnection dbConnectionString

                do! streamName |> Validation.validateStreamName |> repository.DeleteSnapshots connection
            }

    let addSnapshot : EventStore.Core.AddSnapshot =
        fun getDbConnection dbConnectionString repository model ->
            async {
                let model = model |> Validation.validateAddSnapshot

                use! connection = getDbConnection dbConnectionString

                let streamName = StreamName model.StreamName

                let! streamOption = 
                    repository.GetStream connection streamName

                let stream =
                    streamOption |> Option.defaultWith (fun _ -> raise (RecordNotFoundException("Stream not found")))

                let snapshot : Entities.Snapshot = {
                    StreamId = stream.StreamId
                    Version = stream.Version
                    SnapshotId = 0L
                    Description = model.Description
                    Data = model.Data
                    CreatedAt = DateTimeOffset.UtcNow }

                do! Repository.addSnapshot connection snapshot |> Async.Ignore
            }

    let appendEvents : EventStore.Core.AppendEvents =
        fun getDbConnection dbConnectionString repository model ->
            async {
                let model = model |> Validation.validateAppendEvents

                use! connection = getDbConnection dbConnectionString

                let streamName = StreamName model.StreamName

                let! streamOption = 
                    repository.GetStream connection streamName

                use transaction = connection.BeginTransaction()

                try
                    let getStream () =
                        async {
                            match streamOption with
                            | Some stream -> return stream
                            | None ->
                                let stream : Entities.Stream = {
                                    StreamId = 0L
                                    Name = model.StreamName
                                    Version = 0
                                    CreatedAt = DateTimeOffset.UtcNow
                                    UpdatedAt = DateTimeOffset.UtcNow |> Nullable }
                                
                                let! (UniqueId streamId) = repository.AddStream connection transaction stream

                                return { stream with StreamId = streamId }
                        }

                    let! stream = getStream ()

                    if stream.Version <> model.ExpectedVersion then
                        let message = sprintf "Concurrency error - expected stream version to be %i but got %i" stream.Version model.ExpectedVersion
                        raise (ConcurrencyException(message))

                    let toEvent index (event : Models.NewEvent) : Entities.Event = {
                        EventId = 0L
                        StreamId = stream.StreamId
                        Type = event.Type
                        Data = event.Data
                        CreatedAt = DateTimeOffset.UtcNow
                        Version = stream.Version + index + 1 }

                    let events = model.Events |> List.mapi toEvent
                    let newVersion = events |> List.map (fun x -> x.Version) |> List.max

                    for event in events do
                        do! repository.AddEvent connection transaction event |> Async.Ignore

                    do! repository.UpdateStream connection transaction { stream with Version = newVersion }

                    transaction.Commit()
                with
                | ex ->
                    transaction.Rollback()

                    return raise ex
            }

[<RequireQualifiedAccess>]
module CompositionRoot =

    let getDbConnection (DbConnectionString dbConnectionString) =
        let connection = new SqlConnection(dbConnectionString)
        connection.OpenAsync()
        |> Async.AwaitTask
        |> Async.map (fun _ -> connection :> IDbConnection)

    let getRepository () = {
        GetAllStreams = Repository.getAllStreams
        GetStream = Repository.getStream
        GetSnapshots = Repository.getSnapshots
        GetEvents = Repository.getEvents
        DeleteSnapshots = Repository.deleteSnapshots
        AddSnapshot = Repository.addSnapshot
        AddStream = Repository.addStream
        AddEvent = Repository.addEvent
        UpdateStream = Repository.updateStream
    }
