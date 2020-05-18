namespace EventStore.Data

open System
open System.Data

type DatabaseException(message : string, ex : Exception) =
    inherit Exception(message, ex)

type EntityValidationException(message : string) =
    inherit Exception(message)

[<RequireQualifiedAccess>]
module Entities =

    type Stream = {
        StreamId : int64
        Version : int32
        Name : string
        CreatedAt : DateTimeOffset
        UpdatedAt : DateTimeOffset Nullable }

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

type StreamName = StreamName of string

type Version = Version of int32

type UniqueId = UniqueId of int64

type GetAllStreams = IDbConnection -> Async<Entities.Stream list>

type GetStream = IDbConnection -> StreamName -> Async<Entities.Stream option>

type GetSnapshots = IDbConnection -> StreamName -> Async<Entities.Snapshot list>

type GetEvents = IDbConnection -> StreamName -> Version -> Async<Entities.Event list>

type DeleteSnapshots = IDbConnection -> StreamName -> Async<unit>

type UpdateStream = IDbConnection -> IDbTransaction -> Entities.Stream -> Async<unit>

type AddSnapshot = IDbConnection -> Entities.Snapshot -> Async<UniqueId>

type AddStream = IDbConnection -> IDbTransaction -> Entities.Stream -> Async<UniqueId>

type AddEvent = IDbConnection -> IDbTransaction -> Entities.Event -> Async<UniqueId>


