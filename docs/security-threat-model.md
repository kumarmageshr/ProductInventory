# Security Threat Model — E-Commerce Platform

## STRIDE Analysis

| Threat | Vector | Mitigation |
|---|---|---|
| **Spoofing** | Impersonate another user/service | JWT validation at gateway + per-service; Azure AD identity |
| **Tampering** | Modify in-transit messages | TLS 1.3 everywhere; message signing via HMAC in Service Bus |
| **Repudiation** | Deny performing an action | Immutable audit log via Outbox pattern; structured logging |
| **Information Disclosure** | Expose sensitive data | Secrets in Azure Key Vault; no secrets in code/config files |
| **Denial of Service** | Flood endpoints | Rate limiting at gateway (100 req/min/user); Bulkhead in Polly |
| **Elevation of Privilege** | Gain Admin access | RBAC enforcement; policy-based auth; principle of least privilege |

---

## OWASP Top 10 Mitigations

### A01 — Broken Access Control
- Policy-based authorization (`[Authorize(Policy = "AdminPolicy")]`)
- Resource-level authorization: customers can only access their own orders
- `[AllowAnonymous]` only on public catalog endpoints

### A02 — Cryptographic Failures
- All connections use TLS (enforced via `UseHttpsRedirection`)
- SQL Server connections encrypted (`Encrypt=true`)
- JWT signed with RS256 (asymmetric)
- Passwords never stored (Azure AD delegates auth)

### A03 — Injection
- EF Core parameterized queries — no raw SQL
- FluentValidation input validation at all API boundaries
- No dynamic SQL construction

### A04 — Insecure Design
- Saga compensation flows handle all failure scenarios
- No single point of failure (replicated services, circuit breakers)
- Defense in depth: Gateway + Service level auth

### A05 — Security Misconfiguration
- Non-root Docker user (`adduser appuser`)
- Read-only root filesystem in containers
- All capabilities dropped (`drop: [ALL]`)
- NetworkPolicy restricts pod-to-pod traffic
- Secret rotation via Azure Key Vault

### A06 — Vulnerable Components
- Automated OWASP Dependency Check in CI/CD
- Trivy image scanning in pipeline
- Renovate bot for automated dependency PRs

### A07 — Authentication Failures
- JWT expiry enforcement with 30s clock skew tolerance
- Refresh tokens managed by Azure AD
- No session state (stateless APIs)

### A08 — Software and Data Integrity Failures
- Outbox pattern ensures event integrity
- Duplicate detection in Azure Service Bus (MessageId = EventId)
- Docker image signing via ORAS

### A09 — Security Logging Failures
- Structured logging via Serilog with CorrelationId on every log entry
- Authentication failures logged and alerted
- Azure Monitor alerts on 4xx/5xx rate spikes

### A10 — Server-Side Request Forgery
- No user-controlled URLs in server-side requests
- Outbound HTTP restricted to known hosts via NetworkPolicy
- YARP route configuration is declarative (no dynamic routing from user input)

---

## Secrets Management

```
Azure Key Vault
    ├── db-product-connection-string
    ├── db-inventory-connection-string
    ├── db-order-connection-string
    ├── db-payment-connection-string
    ├── db-shipment-connection-string
    ├── servicebus-connection-string
    ├── azure-ad-tenant-id
    └── stripe-api-key
```

Services access secrets via **Azure Workload Identity** (no stored credentials).

---

## Production Readiness Checklist

### Security
- [ ] Azure AD app registrations created per service
- [ ] Key Vault secrets populated
- [ ] Network policies applied
- [ ] TLS certificates provisioned (cert-manager)
- [ ] Container images scanned (no CRITICAL CVEs)

### Reliability
- [ ] HPA configured for all services
- [ ] PodDisruptionBudgets defined
- [ ] Circuit breakers tuned for production latency
- [ ] Dead Letter Queue monitoring configured
- [ ] Outbox processor health monitored

### Observability
- [ ] Dashboards created in Grafana
- [ ] Alerts configured (error rate > 1%, p99 > 2s)
- [ ] Distributed traces flowing to Application Insights
- [ ] Log Analytics workspace connected

### Operations
- [ ] Runbooks written for common incidents
- [ ] Database backup policies configured
- [ ] Blue/Green or canary deployment strategy chosen
- [ ] Chaos engineering tests run (pod kill, network partition)
- [ ] Load tests executed (target: 1000 rps sustained)

### Compliance
- [ ] Data retention policies configured
- [ ] PII data identified and masked in logs
- [ ] GDPR deletion workflow implemented
- [ ] Audit trail for all payment operations
