using FCG.Payments.Application.Abstractions.Repositories;
using FCG.Payments.Domain.Payments;
using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infrastructure.Repositories;

public sealed class PaymentRepository(PaymentsDbContext dbContext) : IPaymentRepository
{
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .Include(payment => payment.Attempts)
            .SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .Include(payment => payment.Attempts)
            .SingleOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);

    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Payments.AnyAsync(payment => payment.OrderId == orderId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
