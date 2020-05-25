namespace EventStore.Core

open System
open System.Data
open EventStore.Data

type ConcurrencyException(message : string) =
    inherit Exception(message)

type RecordNotFoundException(message : string) =
    inherit Exception(message)

type EntityValidationException(message : string) =
    inherit Exception(message)

type DbConnectionString = DbConnectionString of string

[<RequireQualifiedAccess>]
module Models =

    type Stream = {
        StreamId : int64
        Version : int32
        Name : string
        CreatedAt : DateTimeOffset
        UpdatedAt : DateTimeOffset option }
    
    type Event = {
        EventId : int64
        StreamId : int64
        Version : int32
        Data : string
        Type : string    
        CreatedAt : DateTimeOffset }
    
    type Snapshot = {
        SnapshotId : int64
        StreamId : int64
        Version : int32
        Data : string
        Description : string    
        CreatedAt : DateTimeOffset } 

    type NewEvent = {
        Data : string
        Type : string }

    type AppendEvents = {
        ExpectedVersion : int32
        StreamName : string
        Events : NewEvent list }

    type AddSnapshot = {
        StreamName : string
        Description : string
        Data : string }

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
        Repository -> Models.AppendEvents -> Async<unit>

type AddSnapshot = 
    GetDbConnection -> DbConnectionString -> 
        Repository -> Models.AddSnapshot -> Async<unit>

type DeleteSnapshots = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Async<unit>

type GetAllStreams = 
    GetDbConnection -> DbConnectionString ->
        Repository -> Async<Models.Stream list>

type GetSnapshots = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Async<Models.Snapshot list>

type GetEvents = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Version -> Async<Models.Event list>

type GetStream = 
    GetDbConnection -> DbConnectionString ->
        Repository -> StreamName -> Async<Models.Stream option>

[<RequireQualifiedAccess>]
module Mapper =
    
    let toStream (entity : Entities.Stream) : Models.Stream = {
        StreamId = entity.StreamId
        Version = entity.Version
        Name = entity.Name
        CreatedAt = entity.CreatedAt
        UpdatedAt = entity.UpdatedAt |> Option.ofNullable }

    let toSnapshot (entity : Entities.Snapshot) : Models.Snapshot = {
        SnapshotId = entity.SnapshotId
        StreamId = entity.StreamId 
        Version = entity.Version
        Data = entity.Data
        Description = entity.Description
        CreatedAt = entity.CreatedAt }

    let toEvent (entity : Entities.Event) : Models.Event = {
        EventId = entity.EventId 
        StreamId = entity.StreamId 
        Version = entity.Version
        Data = entity.Data
        Type = entity.Type
        CreatedAt = entity.CreatedAt }

[<RequireQualifiedAccess>]
module Validation =

    let private required propertyName value = 
        if String.IsNullOrWhiteSpace(value)
        then raise (EntityValidationException(sprintf "%s is required"  propertyName))
        else value

    let private withMaxLength propertyName length (value : string) =
        if value.Length > length
        then raise(EntityValidationException(sprintf "%s cannot be longer than %i" propertyName length))
        else value

    let private validateWithMaxLength propertyName length =
        required propertyName >> withMaxLength propertyName length

    let private unwrapSteamName (StreamName streamName) = streamName

    let validateStreamName (StreamName streamName) =
        validateWithMaxLength "Stream Name" 256 streamName |> StreamName

    let validateAddSnapshot (model : Models.AddSnapshot) : Models.AddSnapshot = {
        Data = required "Snapshot Data" model.Data
        Description = validateWithMaxLength "Snapshot Description" 256 model.Description
        StreamName = validateStreamName (StreamName model.StreamName) |> unwrapSteamName }

    let validateNewEvent (model : Models.NewEvent) : Models.NewEvent = {
        Data = required "Event Data" model.Data
        Type = validateWithMaxLength "Event Type" 256 model.Type }

    let validateAppendEvents (model : Models.AppendEvents) : Models.AppendEvents = {
        StreamName = validateStreamName (StreamName model.StreamName) |> unwrapSteamName
        ExpectedVersion = model.ExpectedVersion
        Events = model.Events |> List.map validateNewEvent
    }