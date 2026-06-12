# ADR-001: Orchestrator-Based Saga vs Choreography-Based Saga

**Date:** 2024-01-15  
**Status:** Accepted  
**Deciders:** Platform Architecture Team

---

## Context

The Order Placement workflow involves 4 independent microservices:

```
Order Service → Inventory Service → Payment Service → Shipment Service
```

With a compensation flow:

```
Shipment Failure → Refund (Payment) → Release (Inventory) → Cancel (Order)
```

We must choose between **Orchestration** and **Choreography** for coordinating this distributed transaction.

---

## Decision: Orchestrator-Based Saga

We adopt the **Orchestrator pattern** for the Order Placement Saga.

### Rationale

| Criterion | Orchestrator | Choreography |
|---|---|---|
| Visibility | ✅ Single source of truth for workflow state | ❌ State distributed across services |
| Compensation complexity | ✅ Orchestrator knows all steps to undo | ❌ Each service must know what to compensate |
| Testing | ✅ Test orchestrator in isolation | ❌ Must test emergent behavior |
| Debugging | ✅ Single place to trace failures | ❌ Requires correlation across all event logs |
| Service coupling | ⚠️ Services coupled to orchestrator events | ✅ Services only know their domain |
| Scalability | ✅ Orchestrator can scale independently | ✅ No bottleneck |
| Cyclic dependency risk | ✅ None | ❌ Risk of event cycles |

### Why Not Choreography?

Choreography creates **implicit coupling via event contracts**. Adding a new service to the workflow requires understanding what events trigger it and what it must publish — knowledge scattered across codebases. For a **complex compensation workflow with 4+ services**, the debugging overhead is too high for the team's current maturity.

### Consequences

- The `OrderService` becomes the saga coordinator. This is intentional — **Order owns the business process**.
- Other services (Inventory, Payment, Shipment) are pure **command handlers** — they subscribe to requests, execute, and publish results. They know nothing about the workflow.
- Saga state is persisted in `OrderSagaState` table, enabling recovery after crashes.

---

## ADR-002: Messaging Abstraction (IEventBus Factory)

**Status:** Accepted

### Decision

All cross-service communication goes through `IEventBus` with factory-based provider selection.

### Rationale

- Development: RabbitMQ (local Docker)
- Production: Azure Service Bus (managed, enterprise-grade)
- Migration path: Kafka for high-throughput scenarios

Switching providers requires only a config change (`Messaging:Provider`), not code changes. This follows the **Open/Closed Principle** and **Twelve-Factor App** principle 3 (config in environment).

---

## ADR-003: Outbox Pattern for At-Least-Once Delivery

**Status:** Accepted

### Decision

Domain events are written to an `OutboxMessages` table in the **same database transaction** as aggregate state changes. A background `OutboxProcessor` polls and publishes to the message bus.

### Why

Without the Outbox Pattern:

```
1. Save order to DB          ← succeeds
2. Publish OrderCreated event ← CRASHES
→ Inventory never gets notified → Order stuck in Pending forever
```

With Outbox:

```
1. Save order + OutboxMessage (same transaction)
2. OutboxProcessor polls and publishes
3. If publish fails → retry (idempotent consumer handles duplicates)
→ At-least-once delivery guaranteed
```

---

## ADR-004: Clean Architecture Layer Separation

**Status:** Accepted

### Layer Dependency Rule

```
API → Application → Domain
           ↑
    Infrastructure
```

- **Domain** — zero external dependencies. Pure C# business logic.
- **Application** — depends only on Domain interfaces. Orchestrates use cases via CQRS/MediatR.
- **Infrastructure** — implements Domain interfaces (EF Core, messaging). Depends on Application.
- **API** — ASP.NET Core host. Depends on Infrastructure for DI wiring only.

This ensures the domain model is **testable in isolation** and **infrastructure-agnostic**.

---

## ADR-005: Database Per Service

**Status:** Accepted

### Decision

Each microservice owns its dedicated database:

| Service | Database |
|---|---|
| ProductService | ProductDB |
| InventoryService | InventoryDB |
| OrderService | OrderDB |
| PaymentService | PaymentDB |
| ShipmentService | ShipmentDB |

### Consequences

- **No cross-service JOINs** — queries must aggregate via API calls or materialized views
- **Eventual consistency** — services synchronize state via integration events
- **Independent schema migration** — each service migrates on its own schedule
- **Independent scaling** — hot services (Order, Inventory) scale without affecting others

---

## ADR-006: YARP as API Gateway

**Status:** Accepted

### Decision

[YARP (Yet Another Reverse Proxy)](https://microsoft.github.io/reverse-proxy/) is used as the API Gateway instead of Azure API Management or Ocelot.

### Rationale

| Factor | YARP | Azure APIM | Ocelot |
|---|---|---|---|
| Cost | Free (OSS) | ~$300/month | Free (OSS) |
| .NET native | ✅ | ❌ | ✅ |
| Performance | ✅ High | ⚠️ Overhead | ⚠️ Medium |
| Config-driven routing | ✅ JSON | ✅ Portal/ARM | ✅ JSON |
| Middleware pipeline | ✅ Full ASP.NET | ❌ Policy expressions | ⚠️ Limited |
| Circuit Breaker | ✅ Via Polly | ⚠️ Limited | ⚠️ Limited |

YARP's full ASP.NET Core pipeline access allows applying the same authentication, rate limiting, and observability middleware used in services.

---

## ADR-007: JWT + Azure AD for Authentication

**Status:** Accepted

### Decision

- **OAuth 2.0 Authorization Code Flow** for web clients
- **Client Credentials Flow** for service-to-service
- **Azure AD** as the identity provider
- **JWT Bearer tokens** validated at the API Gateway and individual services

### RBAC Roles

| Role | Permissions |
|---|---|
| Admin | Full CRUD on all resources |
| Customer | Create orders, view own orders |
| Warehouse | Manage inventory |
| ShipmentOperator | Manage shipments |

---

## ADR-008: OpenTelemetry for Distributed Tracing

**Status:** Accepted

### Decision

All services instrument with **OpenTelemetry** (vendor-neutral) exporting to:

- **Jaeger** (development) — open-source distributed tracing
- **Azure Application Insights** (production) — via OTLP exporter

### CorrelationId Propagation

```
API Gateway injects X-Correlation-Id header
    ↓
Each service reads CorrelationId from header
    ↓
CorrelationId set on all log messages and spans
    ↓
CorrelationId embedded in all integration events
    ↓
End-to-end trace reconstructable from any service's logs
```
