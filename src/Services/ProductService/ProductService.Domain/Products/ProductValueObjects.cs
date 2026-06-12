using Shared.Domain.Primitives;

namespace ProductService.Domain.Products;

// ── Strongly-typed IDs ─────────────────────────────────────────────────────

public record ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
    public static ProductId From(Guid value) => new(value);
}

public record CategoryId(Guid Value)
{
    public static CategoryId New() => new(Guid.NewGuid());
}

// ── Value Objects ──────────────────────────────────────────────────────────

public sealed class Money : ValueObject
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency required.", nameof(currency));
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    private Money() { } // EF Core

    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = default!;

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add different currencies.");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract different currencies.");
        return new Money(Amount - other.Amount, Currency);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}

public sealed class Sku : ValueObject
{
    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("SKU required.");
        Value = value.ToUpperInvariant();
    }

    private Sku() { } // EF Core

    public string Value { get; private init; } = default!;

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
