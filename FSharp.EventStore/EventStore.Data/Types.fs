namespace EventStore.Data

open System
open System.Data
open EventStore.Common

type DatabaseException(message : string, ex : Exception) =
    inherit Exception(message, ex)

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

type GetAllStreams = IDbConnection -> Async<Stream list>

type GetStream = IDbConnection -> StreamName -> Async<Stream option>

type GetSnapshots = IDbConnection -> StreamName -> Async<Snapshot list>

type GetEvents = IDbConnection -> StreamName -> StartAtVersion -> Async<Event list>

type DeleteSnapshots = IDbConnection -> StreamName -> Async<unit>

type AddSnapshot = IDbConnection -> Snapshot -> Async<unit>

type AddStream = IDbConnection -> IDbTransaction -> Stream -> Async<int64>

type UpdateStream = IDbConnection -> IDbTransaction -> Stream -> Async<unit>

type AddEvents = IDbConnection -> IDbTransaction -> Event list -> Async<unit>


