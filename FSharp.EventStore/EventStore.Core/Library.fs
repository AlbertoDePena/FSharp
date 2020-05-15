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

                let streamName = NonEmptyString.value streamName

                let! stream = 
                    repository.GetStream connection streamName

                return stream |> Option.map StreamModel.fromEntity
            }

    let getAllStreams : EventStore.Core.GetAllStreams =
        fun getDbConnection dbConnectionString repository ->
            async {
                use! connection = getDbConnection dbConnectionString

                let! streams =
                    repository.GetAllStreams connection

                return streams |> List.map StreamModel.fromEntity
            }

    let getEvents : EventStore.Core.GetEvents =
        fun getDbConnection dbConnectionString repository streamName startAtVersion ->
            async {
                use! connection = getDbConnection dbConnectionString

                let streamName = NonEmptyString.value streamName
                let startAtVersion = NonNegativeInt.value startAtVersion

                let! events =
                    repository.GetEvents connection streamName startAtVersion

                return events |> List.map EventModel.fromEntity
            }

    let getSnapshots : EventStore.Core.GetSnapshots =
        fun getDbConnection dbConnectionString repository streamName ->
            async {
                use! connection = getDbConnection dbConnectionString

                let streamName = NonEmptyString.value streamName

                let! snapshots =
                    repository.GetSnapshots connection streamName

                return snapshots |> List.map SnapshotModel.fromEntity
            }

    let deleteSnapshots : EventStore.Core.DeleteSnapshots =
        fun getDbConnection dbConnectionString repository streamName ->
            async {
                use! connection = getDbConnection dbConnectionString

                let streamName = NonEmptyString.value streamName

                do! repository.DeleteSnapshots connection streamName
            }

    let addSnapshot : EventStore.Core.AddSnapshot =
        fun getDbConnection dbConnectionString repository model ->
            async {
                use! connection = getDbConnection dbConnectionString

                let! streamOption = 
                    repository.GetStream connection (NonEmptyString.value model.StreamName)

                let stream =
                    streamOption |> Option.defaultWith (fun _ -> raise (RecordNotFoundException("Stream not found")))

                let snapshot = {
                    StreamId = stream.StreamId
                    Version = stream.Version
                    SnapshotId = 0L
                    Description = NonEmptyString.value model.Description
                    Data = NonEmptyString.value model.Data
                    CreatedAt = DateTimeOffset.UtcNow }

                do! Repository.addSnapshot connection snapshot
            }

    let appendEvents : EventStore.Core.AppendEvents =
        fun getDbConnection dbConnectionString repository model ->
            async {
                use! connection = getDbConnection dbConnectionString

                let! streamOption = 
                    repository.GetStream connection (NonEmptyString.value model.StreamName)

                use transaction = connection.BeginTransaction()

                try
                    let getStream () =
                        async {
                            match streamOption with
                            | Some stream -> return stream
                            | None ->
                                let stream = {
                                    StreamId = 0L
                                    Name = NonEmptyString.value model.StreamName
                                    Version = 0
                                    CreatedAt = DateTimeOffset.UtcNow
                                    UpdatedAt = DateTimeOffset.UtcNow |> Nullable }
                                
                                let! streamId = repository.AddStream connection transaction stream

                                return { stream with StreamId = streamId }
                        }

                    let! stream = getStream ()

                    let expectedVersion = NonNegativeInt.value model.ExpectedVersion

                    if stream.Version <> expectedVersion then
                        let message = sprintf "Concurrency error - expected stream version to be %i but got %i" stream.Version expectedVersion
                        raise (ConcurrencyException(message))

                    let toEvent index (event : NewEventModel) = {
                        EventId = 0L
                        StreamId = stream.StreamId
                        Type = NonEmptyString.value event.Type
                        Data = NonEmptyString.value event.Data
                        CreatedAt = DateTimeOffset.UtcNow
                        Version = stream.Version + index + 1 }

                    let events = model.Events |> List.mapi toEvent
                    let newVersion = events |> List.map (fun x -> x.Version) |> List.max

                    do! repository.AddEvents connection transaction events
                    do! repository.UpdateStream connection transaction { stream with Version = newVersion }

                    transaction.Commit()
                with
                | ex ->
                    transaction.Rollback()

                    return raise ex
            }

[<RequireQualifiedAccess>]
module CompositionRoot =

    let getDbConnection dbConnectionString =
        let connection = new SqlConnection(NonEmptyString.value dbConnectionString)
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
        AddEvents = Repository.addEvents
        UpdateStream = Repository.updateStream
    }
