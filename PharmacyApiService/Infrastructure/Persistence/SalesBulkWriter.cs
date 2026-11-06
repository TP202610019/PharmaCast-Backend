using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PharmacyApiService.Domain.Entities;

namespace PharmacyApiService.Infrastructure.Persistence;

// COPY participates in the caller's transaction; it never commits independently.
public static class SalesBulkWriter
{
    public static async Task HistoryAsync(ApplicationDbContext db, IEnumerable<HistoricalDailyPoint> rows, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY historical_sales_daily (id,dataset_id,product_name,date,quantity) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var row in rows)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(row.Id, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.DatasetId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.ProductName, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.Date, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(row.Quantity, NpgsqlDbType.Double, ct);
        }
        await writer.CompleteAsync(ct);
    }

    public static async Task ForecastAsync(ApplicationDbContext db, IEnumerable<ForecastResult> rows, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY forecast_results (id,prediction_execution_id,product_name,forecast_date,forecast_step,predicted_quantity,lower_bound,upper_bound,created_at,updated_at) FROM STDIN (FORMAT BINARY)", ct);
        foreach (var row in rows)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(row.Id, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.PredictionExecutionId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(row.ProductName, NpgsqlDbType.Text, ct);
            await writer.WriteAsync(row.ForecastDate, NpgsqlDbType.TimestampTz, ct);
            if (row.ForecastStep.HasValue) await writer.WriteAsync(row.ForecastStep.Value, NpgsqlDbType.Integer, ct);
            else await writer.WriteNullAsync(ct);
            await writer.WriteAsync(row.PredictedQuantity, NpgsqlDbType.Double, ct);
            if (row.LowerBound.HasValue) await writer.WriteAsync(row.LowerBound.Value, NpgsqlDbType.Double, ct);
            else await writer.WriteNullAsync(ct);
            if (row.UpperBound.HasValue) await writer.WriteAsync(row.UpperBound.Value, NpgsqlDbType.Double, ct);
            else await writer.WriteNullAsync(ct);
            await writer.WriteAsync(row.CreatedAt, NpgsqlDbType.TimestampTz, ct);
            if (row.UpdatedAt.HasValue) await writer.WriteAsync(row.UpdatedAt.Value, NpgsqlDbType.TimestampTz, ct);
            else await writer.WriteNullAsync(ct);
        }
        await writer.CompleteAsync(ct);
    }
}
