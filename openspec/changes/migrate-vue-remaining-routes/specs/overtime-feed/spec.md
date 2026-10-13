# Spec Delta

## Purpose

Keep payment events visible when a stale client submits a full overtime-feed snapshot after a payment callback has appended a newer event.

## ADDED Requirements

### Requirement: Preserve concurrently appended payment events
The system SHALL preserve current `SuperChat` and third-party payment events that are missing from a full-feed `PUT` snapshot, without changing the endpoint, payload, authorization scope, or SignalR notification contract.

#### Scenario: A payment arrives between feed read and write
- **WHEN** an authorized client reads the overtime feed and a payment event is appended before the client writes its older full-feed snapshot
- **THEN** the `PUT /api/overtime-feed` operation retains the concurrently appended payment event
- **AND** applies the client's other feed entries within the existing feed-size limit

#### Scenario: Explicitly clear the feed
- **WHEN** an authorized client requests `DELETE /api/overtime-feed`
- **THEN** the system clears the feed, including payment events
