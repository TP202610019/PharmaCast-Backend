using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Domain.Enums;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Infrastructure.Persistence;
using PharmacyApiService.Presentation.DTOs.PurchasePlan;

namespace PharmacyApiService.Application.Services;

public class PurchasePlanService : IPurchasePlanService
{
    private readonly IPurchasePlanRepository _plans;
    private readonly IPredictionRepository _predictions;
    private readonly IDatasetService _datasets;
    private readonly ApplicationDbContext _db;

    public PurchasePlanService(
        IPurchasePlanRepository plans,
        IPredictionRepository predictions,
        IDatasetService datasets,
        ApplicationDbContext db)
    {
        _plans = plans;
        _predictions = predictions;
        _datasets = datasets;
        _db = db;
    }

    public async Task<PurchasePlanResponse> GenerateAsync(GeneratePurchasePlanRequest request, Guid userId, CancellationToken ct = default)
    {
        var execution = await _predictions.GetWithDetailsAsync(request.PredictionExecutionId, ct)
            ?? throw new NotFoundException(nameof(PredictionExecution), request.PredictionExecutionId);

        if (execution.ExecutedBy != userId)
            throw new ForbiddenException();

        if (execution.ExecutionStatus != ExecutionStatus.Completed)
            throw new BadRequestException("Cannot generate a purchase plan for an incomplete prediction execution.");

        var existing = await _plans.GetByExecutionAsync(execution.Id, ct);
        if (existing is not null)
            throw new ConflictException("A purchase plan already exists for this execution.");

        // Read inventory stock if provided
        Dictionary<string, double> stockMap = [];
        if (request.InventoryDatasetId.HasValue && request.InventoryFieldMappings is { Count: > 0 })
        {
            var productCol = request.InventoryFieldMappings.GetValueOrDefault("product", "");
            var stockCol   = request.InventoryFieldMappings.GetValueOrDefault("stock", "");
            if (!string.IsNullOrWhiteSpace(productCol) && !string.IsNullOrWhiteSpace(stockCol))
            {
                try
                {
                    stockMap = await _datasets.ReadInventoryStockAsync(
                        request.InventoryDatasetId.Value, userId, productCol, stockCol, ct);
                }
                catch { /* non-fatal: fall back to demand-only plan */ }
            }
        }

        var productGroups = execution.ForecastResults
            .GroupBy(f => f.ProductName)
            .ToList();

        bool hasInventory = stockMap.Count > 0;

        var items = productGroups.Select(g =>
        {
            var demandPredicted = g.Sum(f => f.PredictedQuantity);
            var stockActual     = hasInventory
                ? stockMap.GetValueOrDefault(g.Key, stockMap.GetValueOrDefault(g.Key.ToLowerInvariant(), 0))
                : (double?)null;
            var toPurchase      = stockActual.HasValue
                ? Math.Max(0, demandPredicted - stockActual.Value)
                : demandPredicted;

            return new PurchasePlanItem
            {
                ProductName       = g.Key,
                DemandPredicted   = demandPredicted,
                StockActual       = stockActual,
                ToPurchase        = toPurchase,
                RecommendedQuantity = Math.Ceiling(toPurchase),
            };
        }).ToList();

        var plan = new PurchasePlan
        {
            PredictionExecutionId = execution.Id,
            PlanName = request.PlanName ?? $"Plan - {execution.Dataset?.OriginalFileName ?? execution.Id.ToString("N")[..8]} - {DateTime.UtcNow:yyyy-MM-dd}",
            GeneratedAt = DateTime.UtcNow,
            Items = items
        };

        await _plans.AddAsync(plan, ct);
        await _plans.SaveChangesAsync(ct);

        return MapToResponse(plan);
    }

    public async Task<PurchasePlanResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var plan = await _plans.GetWithItemsAsync(id, ct)
            ?? throw new NotFoundException(nameof(PurchasePlan), id);

        var execution = await _predictions.GetByIdAsync(plan.PredictionExecutionId, ct);
        if (execution?.ExecutedBy != userId)
            throw new ForbiddenException();

        return MapToResponse(plan);
    }

    public async Task<PurchasePlanResponse> GetByExecutionAsync(Guid predictionExecutionId, Guid userId, CancellationToken ct = default)
    {
        var execution = await _predictions.GetByIdAsync(predictionExecutionId, ct)
            ?? throw new NotFoundException(nameof(PredictionExecution), predictionExecutionId);

        if (execution.ExecutedBy != userId)
            throw new ForbiddenException();

        var plan = await _plans.GetByExecutionAsync(predictionExecutionId, ct)
            ?? throw new NotFoundException(nameof(PurchasePlan), $"execution {predictionExecutionId}");

        return MapToResponse(plan);
    }

    public async Task<PlanEvaluationResponse> EvaluateAsync(
        Guid planId,
        IFormFile actualSalesFile,
        string productCol,
        string quantityCol,
        string? priceCol,
        IFormFile? pharmacyPlanFile,
        string? pharmacyProductCol,
        string? pharmacyQuantityCol,
        Guid userId,
        CancellationToken ct = default)
    {
        var plan = await _plans.GetWithItemsAsync(planId, ct)
            ?? throw new NotFoundException(nameof(PurchasePlan), planId);

        var execution = await _predictions.GetByIdAsync(plan.PredictionExecutionId, ct);
        if (execution?.ExecutedBy != userId)
            throw new ForbiddenException();

        var (actualSales, actualPrices) = ParseCsvColumns(actualSalesFile, productCol, quantityCol, priceCol);

        bool hasPrice = !string.IsNullOrWhiteSpace(priceCol) && actualPrices.Count > 0;

        Dictionary<string, double>? pharmacyPlan = null;
        bool hasPharmacyPlan = pharmacyPlanFile is not null
            && !string.IsNullOrWhiteSpace(pharmacyProductCol)
            && !string.IsNullOrWhiteSpace(pharmacyQuantityCol);

        if (hasPharmacyPlan)
            (pharmacyPlan, _) = ParseCsvColumns(pharmacyPlanFile!, pharmacyProductCol!, pharmacyQuantityCol!, null);

        static double CalcAbsError(double planned, double actual) =>
            Math.Abs(planned - actual);

        static string AccuracyLabel(double absErr, double actual) =>
            actual <= 0 ? "ok"
            : absErr / actual <= 0.15 ? "good"
            : absErr / actual <= 0.40 ? "ok" : "poor";

        // Normalize product names: remove accents, strip punctuation, collapse whitespace.
        // Handles mismatches like "SILDEX 100MG X 1 TAB." vs "Sildex 100mg x 1 tab"
        static string NormName(string s)
        {
            var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
            var stripped   = new System.Text.StringBuilder();
            foreach (var ch in normalized)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                    stripped.Append(ch);
            }
            var clean = System.Text.RegularExpressions.Regex.Replace(
                stripped.ToString().ToLowerInvariant(), @"[^a-z0-9\s]+", " ");
            return System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();
        }

        // GroupBy+Sum instead of ToDictionary to handle names that normalize to the same key
        // e.g. "PRODUCT X 20 UNID." and "PRODUCT X 20 UNID" both → "product x 20 unid"
        var normalizedSales = actualSales
            .GroupBy(kv => NormName(kv.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value), StringComparer.OrdinalIgnoreCase);
        var normalizedPrices = actualPrices
            .GroupBy(kv => NormName(kv.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value), StringComparer.OrdinalIgnoreCase);
        var normalizedPharmacy = pharmacyPlan?
            .GroupBy(kv => NormName(kv.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value), StringComparer.OrdinalIgnoreCase);

        // Only evaluate products that appear in the actual-sales CSV.
        var items = plan.Items
            .Where(i => normalizedSales.ContainsKey(NormName(i.ProductName)))
            .Select(i =>
            {
                var key            = NormName(i.ProductName);
                var actualSold     = normalizedSales[key];
                var pharmaCastPlan = i.DemandPredicted ?? i.RecommendedQuantity;
                var pharmacyQty    = normalizedPharmacy?.GetValueOrDefault(key);
                var price          = hasPrice ? normalizedPrices.GetValueOrDefault(key) : 0;

                var pcAbsErr    = CalcAbsError(pharmaCastPlan, actualSold);
                var pharmAbsErr = pharmacyQty.HasValue ? CalcAbsError(pharmacyQty.Value, actualSold) : (double?)null;

                return new PlanEvaluationItemResponse
                {
                    ProductName             = i.ProductName,
                    ActualSold              = actualSold,
                    PharmaCastPlan          = pharmaCastPlan,
                    PharmacyPlan            = pharmacyQty,
                    PharmaCastAccuracy      = AccuracyLabel(pcAbsErr, actualSold),
                    PharmacyAccuracy        = pharmacyQty.HasValue ? AccuracyLabel(pharmAbsErr!.Value, actualSold) : null,
                    Wape                    = Math.Round(actualSold > 0 ? pcAbsErr / actualSold * 100 : 0, 2),
                    PharmacyWape            = pharmAbsErr.HasValue ? Math.Round(actualSold > 0 ? pharmAbsErr.Value / actualSold * 100 : 0, 2) : null,
                    SobrestockSoles         = Math.Round(Math.Max(0, pharmaCastPlan - actualSold) * price, 2),
                    DesabastoSoles          = Math.Round(Math.Max(0, actualSold - pharmaCastPlan) * price, 2),
                    PharmacySobrestockSoles = pharmacyQty.HasValue ? Math.Round(Math.Max(0, pharmacyQty.Value - actualSold) * price, 2) : null,
                    PharmacyDesabastoSoles  = pharmacyQty.HasValue ? Math.Round(Math.Max(0, actualSold - pharmacyQty.Value) * price, 2) : null,
                };
            }).ToList();

        int n         = items.Count;
        int pcGood    = items.Count(x => x.PharmaCastAccuracy == "good");
        int pharmGood = items.Count(x => x.PharmacyAccuracy   == "good");

        double totalActual     = items.Sum(x => x.ActualSold);
        double pcTotalAbsErr   = items.Sum(x => Math.Abs(x.PharmaCastPlan - x.ActualSold));
        double pharmTotalAbsErr = hasPharmacyPlan
            ? items.Where(x => x.PharmacyPlan.HasValue).Sum(x => Math.Abs(x.PharmacyPlan!.Value - x.ActualSold))
            : 0;

        var summary = new PlanEvaluationSummaryResponse
        {
            TotalProducts                  = n,
            HasPharmacyPlan                = hasPharmacyPlan,
            HasPrice                       = hasPrice,
            PharmaCastGoodCount            = pcGood,
            PharmacyGoodCount              = pharmGood,
            PharmaCastGoodPct              = n > 0 ? Math.Round(pcGood    * 100.0 / n, 1) : 0,
            PharmacyGoodPct                = n > 0 ? Math.Round(pharmGood * 100.0 / n, 1) : 0,
            PharmaCastWape                 = totalActual > 0 ? Math.Round(pcTotalAbsErr / totalActual * 100, 1) : 0,
            PharmacyWape                   = hasPharmacyPlan && totalActual > 0
                ? Math.Round(pharmTotalAbsErr / totalActual * 100, 1)
                : null,
            PharmaCastTotalSobrestockSoles = Math.Round(items.Sum(x => x.SobrestockSoles), 2),
            PharmaCastTotalDesabastoSoles  = Math.Round(items.Sum(x => x.DesabastoSoles), 2),
            PharmacyTotalSobrestockSoles   = hasPharmacyPlan ? Math.Round(items.Sum(x => x.PharmacySobrestockSoles ?? 0), 2) : null,
            PharmacyTotalDesabastoSoles    = hasPharmacyPlan ? Math.Round(items.Sum(x => x.PharmacyDesabastoSoles  ?? 0), 2) : null,
        };

        // Persist evaluation so the user can view it later without re-uploading files
        var evaluation = new PlanEvaluation
        {
            PurchasePlanId                 = planId,
            EvaluatedAt                    = DateTime.UtcNow,
            TotalProducts                  = summary.TotalProducts,
            HasPharmacyPlan                = summary.HasPharmacyPlan,
            HasPrice                       = summary.HasPrice,
            PharmaCastGoodCount            = summary.PharmaCastGoodCount,
            PharmaCastGoodPct              = summary.PharmaCastGoodPct,
            PharmaCastWape                 = summary.PharmaCastWape,
            PharmacyGoodCount              = summary.HasPharmacyPlan ? summary.PharmacyGoodCount : null,
            PharmacyGoodPct                = summary.HasPharmacyPlan ? summary.PharmacyGoodPct   : null,
            PharmacyWape                   = summary.PharmacyWape,
            PharmaCastTotalSobrestockSoles = summary.PharmaCastTotalSobrestockSoles,
            PharmaCastTotalDesabastoSoles  = summary.PharmaCastTotalDesabastoSoles,
            PharmacyTotalSobrestockSoles   = summary.PharmacyTotalSobrestockSoles,
            PharmacyTotalDesabastoSoles    = summary.PharmacyTotalDesabastoSoles,
            Items = items.Select(i => new PlanEvaluationItem
            {
                ProductName             = i.ProductName,
                ActualSold              = i.ActualSold,
                PharmaCastPlan          = i.PharmaCastPlan,
                PharmacyPlan            = i.PharmacyPlan,
                PharmaCastAccuracy      = i.PharmaCastAccuracy,
                PharmacyAccuracy        = i.PharmacyAccuracy,
                Wape                    = i.Wape,
                PharmacyWape            = i.PharmacyWape,
                SobrestockSoles         = i.SobrestockSoles,
                DesabastoSoles          = i.DesabastoSoles,
                PharmacySobrestockSoles = i.PharmacySobrestockSoles,
                PharmacyDesabastoSoles  = i.PharmacyDesabastoSoles,
            }).ToList(),
        };

        _db.PlanEvaluations.Add(evaluation);
        await _db.SaveChangesAsync(ct);

        return new PlanEvaluationResponse { PlanId = planId.ToString(), Summary = summary, Items = items };
    }

    public async Task<PlanEvaluationResponse?> GetLatestEvaluationAsync(Guid planId, Guid userId, CancellationToken ct = default)
    {
        var plan = await _plans.GetByIdAsync(planId, ct)
            ?? throw new NotFoundException(nameof(PurchasePlan), planId);

        var execution = await _predictions.GetByIdAsync(plan.PredictionExecutionId, ct);
        if (execution?.ExecutedBy != userId)
            throw new ForbiddenException();

        var eval = await _db.PlanEvaluations
            .Where(e => e.PurchasePlanId == planId)
            .OrderByDescending(e => e.EvaluatedAt)
            .Include(e => e.Items)
            .FirstOrDefaultAsync(ct);

        if (eval is null) return null;

        var summary = new PlanEvaluationSummaryResponse
        {
            TotalProducts                  = eval.TotalProducts,
            HasPharmacyPlan                = eval.HasPharmacyPlan,
            HasPrice                       = eval.HasPrice,
            PharmaCastGoodCount            = eval.PharmaCastGoodCount,
            PharmaCastGoodPct              = eval.PharmaCastGoodPct,
            PharmaCastWape                 = eval.PharmaCastWape,
            PharmacyGoodCount              = eval.PharmacyGoodCount ?? 0,
            PharmacyGoodPct                = eval.PharmacyGoodPct  ?? 0,
            PharmacyWape                   = eval.PharmacyWape,
            PharmaCastTotalSobrestockSoles = eval.PharmaCastTotalSobrestockSoles,
            PharmaCastTotalDesabastoSoles  = eval.PharmaCastTotalDesabastoSoles,
            PharmacyTotalSobrestockSoles   = eval.PharmacyTotalSobrestockSoles,
            PharmacyTotalDesabastoSoles    = eval.PharmacyTotalDesabastoSoles,
        };

        var items = eval.Items.Select(i => new PlanEvaluationItemResponse
        {
            ProductName             = i.ProductName,
            ActualSold              = i.ActualSold,
            PharmaCastPlan          = i.PharmaCastPlan,
            PharmacyPlan            = i.PharmacyPlan,
            PharmaCastAccuracy      = i.PharmaCastAccuracy,
            PharmacyAccuracy        = i.PharmacyAccuracy,
            Wape                    = i.Wape,
            PharmacyWape            = i.PharmacyWape,
            SobrestockSoles         = i.SobrestockSoles,
            DesabastoSoles          = i.DesabastoSoles,
            PharmacySobrestockSoles = i.PharmacySobrestockSoles,
            PharmacyDesabastoSoles  = i.PharmacyDesabastoSoles,
        }).ToList();

        return new PlanEvaluationResponse { PlanId = planId.ToString(), Summary = summary, Items = items };
    }

    private static (Dictionary<string, double> primary, Dictionary<string, double> secondary)
        ParseCsvColumns(IFormFile file, string productCol, string primaryCol, string? secondaryCol)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls"
            ? ParseExcelColumns(file, productCol, primaryCol, secondaryCol)
            : ParseCsvColumnsFromStream(file, productCol, primaryCol, secondaryCol);
    }

    private static (Dictionary<string, double> primary, Dictionary<string, double> secondary)
        ParseCsvColumnsFromStream(IFormFile file, string productCol, string primaryCol, string? secondaryCol)
    {
        var primary   = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var secondary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(file.OpenReadStream());
        var header = reader.ReadLine();
        if (header is null) return (primary, secondary);
        if (header.Length > 0 && header[0] == '﻿') header = header[1..];
        var delim = header.Contains(';') ? ';' : ',';
        var cols  = header.Split(delim).Select(c => c.Trim().Trim('"')).ToList();
        var pIdx  = cols.FindIndex(c => string.Equals(c, productCol, StringComparison.OrdinalIgnoreCase));
        var qIdx  = cols.FindIndex(c => string.Equals(c, primaryCol, StringComparison.OrdinalIgnoreCase));
        var sIdx  = string.IsNullOrWhiteSpace(secondaryCol) ? -1
                    : cols.FindIndex(c => string.Equals(c, secondaryCol, StringComparison.OrdinalIgnoreCase));
        if (pIdx < 0 || qIdx < 0) return (primary, secondary);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var parts = line.Split(delim);
            if (parts.Length <= Math.Max(pIdx, qIdx)) continue;
            var prod = parts[pIdx].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(prod)) continue;
            if (double.TryParse(parts[qIdx].Trim().Trim('"'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var qty))
                primary[prod] = primary.GetValueOrDefault(prod) + qty;
            if (sIdx >= 0 && sIdx < parts.Length &&
                double.TryParse(parts[sIdx].Trim().Trim('"'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var sec))
                secondary[prod] = sec;
        }
        return (primary, secondary);
    }

    private static (Dictionary<string, double> primary, Dictionary<string, double> secondary)
        ParseExcelColumns(IFormFile file, string productCol, string primaryCol, string? secondaryCol)
    {
        var primary   = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var secondary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var stream = file.OpenReadStream();
        using var excelReader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
        {
            FallbackEncoding = System.Text.Encoding.GetEncoding(1252)
        });

        // Read first sheet, first row = headers
        if (!excelReader.Read()) return (primary, secondary);

        var headers = Enumerable.Range(0, excelReader.FieldCount)
            .Select(i => excelReader.GetValue(i)?.ToString()?.Trim() ?? "")
            .ToList();

        var pIdx = headers.FindIndex(c => string.Equals(c, productCol, StringComparison.OrdinalIgnoreCase));
        var qIdx = headers.FindIndex(c => string.Equals(c, primaryCol, StringComparison.OrdinalIgnoreCase));
        var sIdx = string.IsNullOrWhiteSpace(secondaryCol) ? -1
                   : headers.FindIndex(c => string.Equals(c, secondaryCol, StringComparison.OrdinalIgnoreCase));

        if (pIdx < 0 || qIdx < 0) return (primary, secondary);

        while (excelReader.Read())
        {
            var prod = excelReader.GetValue(pIdx)?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(prod)) continue;

            var rawQty = excelReader.GetValue(qIdx);
            double qty = rawQty is double d ? d
                : double.TryParse(rawQty?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0;
            primary[prod] = primary.GetValueOrDefault(prod) + qty;

            if (sIdx >= 0)
            {
                var rawSec = excelReader.GetValue(sIdx);
                if (rawSec is double ds)
                    secondary[prod] = ds;
                else if (double.TryParse(rawSec?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var s))
                    secondary[prod] = s;
            }
        }
        return (primary, secondary);
    }

    private static PurchasePlanResponse MapToResponse(PurchasePlan p) => new()
    {
        Id = p.Id,
        PredictionExecutionId = p.PredictionExecutionId,
        PlanName = p.PlanName,
        GeneratedAt = p.GeneratedAt,
        CreatedAt = p.CreatedAt,
        Items = p.Items.Select(i => new PurchasePlanItemResponse
        {
            Id = i.Id,
            ProductName = i.ProductName,
            RecommendedQuantity = i.RecommendedQuantity,
            DemandPredicted = i.DemandPredicted,
            StockActual = i.StockActual,
            ToPurchase = i.ToPurchase,
            Category = i.Category,
            Supplier = i.Supplier,
            EstimatedCost = i.EstimatedCost
        }).ToList()
    };
}
