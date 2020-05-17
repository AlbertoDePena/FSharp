namespace EventStore.Core

open System
open System.Data
open EventStore.Common

type ConcurrencyException(message : string) =
    inherit Exception(message)

type RecordNotFoundException(message : string) =
    inherit Exception(message)

type ModelValidationException(message : string) =
    inherit Exception(message)

type CorruptedDataException(message : string) =
    inherit Exception(message)

type CreatedAtDate = CreatedAtDate of DateTimeOffset

type UpdatedAtDate = UpdatedAtDate of DateTimeOffset

type StreamName = NonEmptyString

type StartAtVersion = NonNegativeInt

type DbConnectionString = NonEmptyString

type StreamModel = {
    StreamId : NonNegativeLong
    Version : NonNegativeInt
    Name : NonEmptyString
    CreatedAt : CreatedAtDate
    UpdatedAt : UpdatedAtDate option }
    
type EventModel = {
    EventId : NonNegativeLong
    StreamId : NonNegativeLong
    Version : NonNegativeInt
    Data : NonEmptyString
    Type : NonEmptyString    
    CreatedAt : CreatedAtDate }
    
type SnapshotModel = {
    SnapshotId : NonNegativeLong
    StreamId : NonNegativeLong
    Version : NonNegativeInt
    Data : NonEmptyString
    Description : NonEmptyString    
    CreatedAt : CreatedAtDate } 

type NewEventModel = {
    Data : NonEmptyString
    Type : NonEmptyString }

type AppendEventsModel = {
    ExpectedVersion : NonNegativeInt
    StreamName : NonEmptyString
    Events : NewEventModel list
}

type AddSnapshotModel = {
    StreamName : NonEmptyString
    Description : NonEmptyString
    Data : NonEmptyString }

type Repository = {
    GetAllStreams : EventStore.Data.GetAllStreams
    GetStream : EventStore.Data.GetStream
    GetSnapshots : EventStore.Data.GetSnapshots
    GetEvents : EventStore.Data.GetEvents
    DeleteSnapshots : EventStore.Data.DeleteSnapshots
    AddSnapshot : EventStore.Data.AddSnapshot
    AddStream : EventStore.Data.AddStream
    UpdateStream : EventStore.Data.UpdateStream
    AddEvent : EventStore.Data.AddEvent }

type GetDbConnection = DbConnectionString -> Async<IDbConnection>

type AppendEvents = 
    GetDbConnection -> DbConnectionString ->
        Repository -> AppendEventsModel -> Async<unit>

type AddSnapshot = 
    GetDbConnection -> DbConnectionString -> 
        Repository -> AddSnapshotModel -> Async<unit>

type DeleteSnapshots = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Async<unit>

type GetAllStreams = 
    GetDbConnection -> DbConnectionString ->
        Repository -> Async<StreamModel list>

type GetSnapshots = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Async<SnapshotModel list>

type GetEvents = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> StartAtVersion -> Async<EventModel list>

type GetStream = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Async<StreamModel option>

[<RequireQualifiedAccess>]
module StreamModel =
    
    let private throwCorruptedDataError () = 
        raise (CorruptedDataException("Stream data is corrupted"))

    let fromEntity (entity : EventStore.Data.Stream) : StreamModel = {
        StreamId = 
            entity.StreamId 
            |> NonNegativeLong.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Version =
            entity.Version
            |> NonNegativeInt.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Name =
            entity.Name
            |> NonEmptyString.createOptional
            |> Option.defaultWith throwCorruptedDataError
        CreatedAt = 
            CreatedAtDate entity.CreatedAt
        UpdatedAt = 
            entity.UpdatedAt            
            |> Option.ofNullable
            |> Option.map UpdatedAtDate }

[<RequireQualifiedAccess>]
module SnapshotModel =
    
    let private throwCorruptedDataError () = 
        raise (CorruptedDataException("Snapshot data is corrupted"))

    let fromEntity (entity : EventStore.Data.Snapshot) : SnapshotModel = {
        SnapshotId = 
            entity.SnapshotId 
            |> NonNegativeLong.createOptional
            |> Option.defaultWith throwCorruptedDataError
        StreamId = 
            entity.StreamId 
            |> NonNegativeLong.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Version =
            entity.Version
            |> NonNegativeInt.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Data =
            entity.Data
            |> NonEmptyString.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Description =
            entity.Description
            |> NonEmptyString.createOptional
            |> Option.defaultWith throwCorruptedDataError
        CreatedAt = 
            CreatedAtDate entity.CreatedAt }

    //let toEntity (model : SnapshotModel) : EventStore.Data.

[<RequireQualifiedAccess>]
module EventModel =
    
    let private throwCorruptedDataError () = 
        raise (CorruptedDataException("Event data is corrupted"))

    let fromEntity (entity : EventStore.Data.Event) : EventModel = {
        EventId = 
            entity.EventId 
            |> NonNegativeLong.createOptional
            |> Option.defaultWith throwCorruptedDataError
        StreamId = 
            entity.StreamId 
            |> NonNegativeLong.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Version =
            entity.Version
            |> NonNegativeInt.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Data =
            entity.Data
            |> NonEmptyString.createOptional
            |> Option.defaultWith throwCorruptedDataError
        Type =
            entity.Type
            |> NonEmptyString.createOptional
            |> Option.defaultWith throwCorruptedDataError
        CreatedAt = 
            CreatedAtDate entity.CreatedAt }
        