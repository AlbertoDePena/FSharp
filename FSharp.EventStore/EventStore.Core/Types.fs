﻿namespace EventStore.Core

open System
open System.Data
open EventStore.Data

type ConcurrencyException(message : string) =
    inherit Exception(message)

type RecordNotFoundException(message : string) =
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
    
    let toStream (entity : EventStore.Data.Entities.Stream) : Models.Stream = {
        StreamId = entity.StreamId
        Version = entity.Version
        Name = entity.Name
        CreatedAt = entity.CreatedAt
        UpdatedAt = entity.UpdatedAt |> Option.ofNullable }

    let toSnapshot (entity : EventStore.Data.Entities.Snapshot) : Models.Snapshot = {
        SnapshotId = entity.SnapshotId
        StreamId = entity.StreamId 
        Version = entity.Version
        Data = entity.Data
        Description = entity.Description
        CreatedAt = entity.CreatedAt }

    let toEvent (entity : EventStore.Data.Entities.Event) : Models.Event = {
        EventId = entity.EventId 
        StreamId = entity.StreamId 
        Version = entity.Version
        Data = entity.Data
        Type = entity.Type
        CreatedAt = entity.CreatedAt }