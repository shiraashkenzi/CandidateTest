# Architecture — Scaling the Requests System

As the system grows, I would initially split it into two microservices: a **Request Service**, responsible for the request lifecycle, and a **Notification Service**, responsible for user notifications. Communication between them would be asynchronous through a durable message broker, using the **Transactional Outbox** pattern to prevent notification events from being lost when either the broker or Notification Service is temporarily unavailable.

## Current State

A single ASP.NET Core service owning one `Request` entity. Search, filtering, sorting, paging and authorization are composed through EF Core and applied before the query is materialized.

The current EF Core InMemory provider does not provide the transactional guarantees required by the Transactional Outbox, so the target design assumes a relational database.

## Target Architecture

```text id="mbs3js"
Client
   ↓
Request Service
   ↓
Request DB + Outbox
   ↓
Message Broker
   ↓
Notification Service
   ↓
Email / SMS / Push
```

## Service Responsibilities

* **Request Service** — owns request business rules, authorization, creation, status changes, search/pagination and persistence. It also writes integration events to the Outbox.
* **Notification Service** — consumes events and sends email, SMS or push notifications, with retries and idempotent processing.

## Reliable Notifications

When a Request is created or changes status, the Request Service creates a `RequestCreated` or `RequestStatusChanged` event.

1. **Atomic write** — the Request change and Outbox event are saved in the same database transaction.
2. **Publish** — a background worker publishes pending Outbox events to a durable message broker. If the broker is unavailable, the event remains in the Outbox and is retried later.
3. **Consume** — if the Notification Service is unavailable, the broker keeps the message until it can be processed.
4. **Idempotency** — each event has a unique `eventId`, allowing duplicate deliveries to be safely detected.
5. **Retry & DLQ** — transient notification failures are retried with backoff; repeated failures are moved to a Dead Letter Queue for inspection and replay.

Delivery is **at-least-once**, not exactly-once. Rare duplicate notifications are possible, but events are not silently lost.

## Benefits

This architecture provides **loose coupling, failure isolation, independent scaling and deployment, and reliable asynchronous communication**. A failure in the Notification Service does not prevent users from creating or updating Requests.

The trade-off is additional infrastructure, eventual consistency and the need to handle duplicate messages.

## Why Two Services?

Requests and notifications have different responsibilities, failure modes and scaling needs, making them a natural service boundary. Additional services should only be introduced when there is a concrete need for independent scaling, deployment or ownership.
