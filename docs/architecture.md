# Architecture

How this Products service fits into a wider event-driven system, and why it is
built the way it is.

## 1. Service topology

![Architecture diagram](./architecture.png)

<details>
<summary>Mermaid source</summary>

```mermaid
flowchart LR
    classDef client  fill:#f3e8fd,stroke:#8a4fd4,color:#2b1244
    classDef edge    fill:#e8f0fe,stroke:#3b6fd4,color:#10243e
    classDef service fill:#ffffff,stroke:#3b6fd4,stroke-width:2px,color:#10243e
    classDef store   fill:#fef7e0,stroke:#c9911a,color:#3d2c05
    classDef broker  fill:#e6f4ea,stroke:#2d8a4e,stroke-width:2px,color:#0d2a17

    spa["React SPA"]:::client
    partner["Mobile / partner clients"]:::client

    gateway["API Gateway / BFF<br/>TLS · authn · routing · quotas"]:::edge

    spa --> gateway
    partner --> gateway

    subgraph products_bc["Products (bounded context)"]
        direction TB
        products_api["<b>Products API</b><br/>this repository<br/>Clean Architecture · CQRS"]:::service
        products_db[("Products DB")]:::store
        products_outbox[["outbox"]]:::store
        products_api --> products_db
        products_api -. same transaction .-> products_outbox
    end

    subgraph orders_bc["Orders (bounded context)"]
        direction TB
        orders_api["<b>Orders API</b>"]:::service
        orders_db[("Orders DB")]:::store
        orders_outbox[["outbox"]]:::store
        orders_api --> orders_db
        orders_api -. same transaction .-> orders_outbox
    end

    subgraph payments_bc["Payments (bounded context)"]
        direction TB
        payments_api["<b>Payments API</b>"]:::service
        payments_db[("Payments DB")]:::store
        payments_outbox[["outbox"]]:::store
        payments_api --> payments_db
        payments_api -. same transaction .-> payments_outbox
    end

    gateway --> products_api
    gateway --> orders_api
    gateway --> payments_api

    broker{{"<b>Message broker: RabbitMQ</b><br/>topic exchange · durable queues · DLQ"}}:::broker

    products_outbox -- "ProductCreated<br/>ProductPriceChanged" --> broker
    orders_outbox   -- "OrderPlaced<br/>OrderConfirmed / OrderCancelled" --> broker
    payments_outbox -- "PaymentProcessed<br/>PaymentFailed" --> broker

    broker -- "OrderPlaced" --> payments_api
    broker -- "PaymentProcessed<br/>PaymentFailed" --> orders_api
    broker -- "OrderConfirmed / OrderCancelled" --> products_api
    broker -- "ProductPriceChanged" --> orders_api

    search["Search / read-model projector"]:::service
    notify["Notifications"]:::service
    broker -- "ProductCreated<br/>ProductPriceChanged" --> search
    broker -- "OrderConfirmed<br/>PaymentFailed" --> notify

    %% Neutral backgrounds for the bounded-context groupings, so the colour in
    %% the diagram carries meaning (service / store / broker) rather than
    %% decorating the containers.
    style products_bc fill:#f8fafc,stroke:#94a3b8,stroke-dasharray:4 3
    style orders_bc   fill:#f8fafc,stroke:#94a3b8,stroke-dasharray:4 3
    style payments_bc fill:#f8fafc,stroke:#94a3b8,stroke-dasharray:4 3
```

</details>

### Events, publishers and consumers

| Event | Published by | Consumed by | Why the consumer cares |
|---|---|---|---|
| `ProductCreated` | Products | Search, Notifications | Index the new item, announce it |
| `ProductPriceChanged` | Products | Orders, Search | Orders re-checks baskets against current pricing |
| `OrderPlaced` | Orders | Payments | Trigger to take payment |
| `PaymentProcessed` | Payments | Orders | Move the order to Confirmed |
| `PaymentFailed` | Payments | Orders, Notifications | Cancel the order, tell the customer |
| `OrderConfirmed` | Orders | Products, Notifications | Decrement stock, send confirmation |
| `OrderCancelled` | Orders | Products | Release reserved stock |

Each bounded context owns its datastore. No service reads another's tables. That
is what makes the boundaries real: if Orders could query the Products database,
the two would be one system in two deployment units, with the operational cost of
microservices and none of the independence.

## 2. An asynchronous flow

![Order sequence diagram](./sequence.png)

<details>
<summary>Mermaid source</summary>

```mermaid
sequenceDiagram
    autonumber
    actor Customer
    participant GW as API Gateway
    participant Orders as Orders service
    participant Broker as Message broker
    participant Payments as Payments service
    participant Products as Products service

    Customer->>GW: POST /api/orders
    GW->>Orders: create order
    Orders->>Orders: persist Order (Pending)<br/>+ OrderPlaced in the SAME transaction
    Orders-->>GW: 202 Accepted
    GW-->>Customer: 202 Accepted, order is Pending

    Note over Orders,Broker: A relay publishes the outbox row.<br/>The write and the announcement cannot diverge.
    Orders-)Broker: OrderPlaced

    Broker-)Payments: OrderPlaced
    Payments->>Payments: take payment (idempotent on OrderId)
    Payments-)Broker: PaymentProcessed

    Broker-)Orders: PaymentProcessed
    Orders->>Orders: Order -> Confirmed
    Orders-)Broker: OrderConfirmed

    Broker-)Products: OrderConfirmed
    Products->>Products: decrement stock for each line

    Note over Customer,Products: The customer was answered long before this settled.<br/>The system is eventually consistent, not immediately consistent.

    rect rgb(253, 237, 237)
        Note over Payments,Products: Failure path
        Payments-)Broker: PaymentFailed
        Broker-)Orders: PaymentFailed
        Orders->>Orders: Order -> Cancelled
        Orders-)Broker: OrderCancelled
        Broker-)Products: OrderCancelled
        Products->>Products: release any reserved stock
    end
```

</details>

The notable part is what is absent: no service calls another synchronously.
Orders does not wait for Payments, and Payments does not ask Products whether
stock exists. Each reacts to a fact that has already happened and publishes one of
its own.

The customer gets `202 Accepted` with the order `Pending` rather than `201
Created`, because at that moment nothing has been paid and no stock has moved.

## 3. Why events rather than direct calls

**Availability compounds badly in synchronous chains.** If Orders calls Payments
which calls Products, the flow needs all three up at once. At 99.9% each that is
about 99.7%, roughly a day a year of downtime no single service is responsible
for. With a broker, Payments can be down for ten minutes and orders keep being
accepted; the queue drains when it returns.

**Load spikes become queue depth instead of failures.** A synchronous chain
propagates a surge to every downstream service at once, and the slowest decides
the user's experience. A queue absorbs it and lets consumers work at their own
rate. Queue depth is also a better scaling signal than CPU, since it measures work
outstanding rather than effort spent.

**Adding a consumer stops being a change to the producer.** Search and
Notifications subscribe to events Products already publishes. Products was not
modified or redeployed. With direct calls, every new interested party is a code
change in the producing service, which is how a set of microservices becomes a
distributed monolith.

### The costs

- **Eventual consistency is a product decision.** Between `OrderPlaced` and
  `OrderConfirmed` the order exists and is not paid. The UI has to show that,
  support has to understand it, and someone has to decide what happens if it never
  resolves.
- **Every consumer must be idempotent.** Brokers deliver at least once, so a
  redelivered `PaymentProcessed` must not charge twice. This is the most common
  source of bugs in event-driven systems and it is the consumer's problem each
  time.
- **Debugging spans services.** "Where did this order go" becomes a question
  across several logs and a broker. Correlation IDs in message headers and
  distributed tracing are not optional. This service already stamps a `traceId` on
  every response and log entry.
- **Ordering is not guaranteed across partitions.** Two `ProductPriceChanged`
  events for one product can arrive out of order. Either partition by aggregate id
  or carry a version and discard stale updates.
- **Event schemas are contracts.** Adding a field is safe; removing or
  repurposing one breaks consumers you may not know about.

### Where synchronous calls still win

Query paths needing an immediate consistent answer ("is this token valid", "what
is this product right now") should stay request/response. The gateway calling
Products over HTTP for a read is correct. It is state changes that fan out which
benefit from events; using a broker for a lookup adds latency and failure modes
for nothing.

## 4. RabbitMQ or Kafka

The diagram shows RabbitMQ:

- Traffic is business events at human scale, thousands per minute rather than
  millions per second, so Kafka's throughput advantage buys nothing.
- The interaction is competing consumers on a work queue, which is RabbitMQ's
  native model.
- Per-message acknowledgement, dead-letter queues and delayed retry are built in.
  On Kafka these are patterns you assemble.
- It is operationally lighter, which matters with a small team.

Kafka would be the better answer if events needed retaining and replaying to
rebuild a read model, if the system moved to event sourcing where the log is the
source of truth, if throughput reached hundreds of thousands per second, or if a
stream processing layer were wanted over the history.

The real trade is that RabbitMQ is a broker (once acknowledged, a message is gone)
whereas Kafka is a durable replayable log. Replay is the deciding capability, not
throughput.

## 5. How this service already fits

The seam is present and working.

**Domain events are raised by the aggregate.** `Product.Create` raises
`ProductCreatedDomainEvent`; `ChangePrice` raises `ProductPriceChangedDomainEvent`
carrying both old and new price, because a consumer needs the delta and cannot
reconstruct it from the new state.

**They are dispatched after commit.** `ProductsDbContext.SaveChangesAsync`
collects pending events, saves, then dispatches, so no handler reacts to a change
that is later rolled back.

**The domain does not know how they are delivered.** `IDomainEvent` has no MediatR
reference; an Application-layer wrapper adapts it. Publishing to a broker instead
changes that one adapter.

### What publishing for real would take

The handler that currently logs is the extension point:

```csharp
// Products.Application/Products/EventHandlers/ProductCreatedDomainEventHandler.cs
public Task Handle(DomainEventNotification<ProductCreatedDomainEvent> notification, ...)
{
    // Today: a log line.
    // Production: write a ProductCreated integration event to the outbox,
    // in the same transaction as the product itself.
}
```

Three pieces remain:

1. An **outbox table** in the Products database, written in the same transaction
   as the product.
2. A **relay** polling unpublished rows, publishing them and marking them sent.
3. An **integration event contract** versioned separately from the domain event.
   The domain event is internal and can be refactored; the integration event is a
   published API and cannot.

### Why the outbox

Publishing straight to the broker from that handler would be a dual write: two
systems changed with no transaction across them. If the database commits and the
publish then fails, the product exists and nothing downstream hears about it.
Search never indexes it, and no error is raised, because from the API's point of
view the request succeeded. The inconsistency is silent and permanent.

Writing the event to the same database in the same transaction makes "saved" and
"will be announced" atomic, and a relay guarantees delivery by retrying until the
broker acknowledges. The cost is at-least-once delivery, which is why consumers
must be idempotent.

## 6. What production would also need

- **A real identity provider.** `/api/auth/token` is a stand-in. Production would
  use Azure AD, Auth0 or IdentityServer with RS256 and keys published via JWKS, so
  the API holds only a verification key.
- **Distributed tracing** with OpenTelemetry, propagated through message headers
  so a trace survives the broker hop.
- **A schema registry** for event contracts, with compatibility checks in CI.
- **Kubernetes manifests** using the liveness and readiness probes this service
  already exposes. Readiness includes the database; liveness does not, because
  restarting a process does not fix a database outage.
- **Migrations as a deployment step** rather than at application start-up, so
  replicas do not race to migrate one schema.
