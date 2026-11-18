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
        if (request.InventoryDatasetId.HasValue)
        {
            var fields = request.InventoryFieldMappings;
            var productCol = fields?.GetValueOrDefault("inv_product")
                ?? fields?.GetValueOrDefault("product") ?? fields?.GetValueOrDefault("product_id");
            var stockCol = fields?.GetValueOrDefault("stock");
            if (string.IsNullOrWhiteSpace(productCol) || string.IsNullOrWhiteSpace(stockCol))
                throw new ValidationException("inventory", "Mapea las columnas de producto y stock del inventario.");
            stockMap = await _datasets.ReadInventoryStockAsync(
                request.InventoryDatasetId.Value, userId, productCol, stockCol, ct);
            if (stockMap.Count == 0 || stockMap.Values.Any(v => !double.IsFinite(v) || v < 0))
                throw new ValidationException("inventory", "El inventario debe contener productos con stock válido y no negativo.");
        }

        var productGroups = execution.ForecastResults
            .GroupBy(f => f.ProductName)
            .ToList();

        bool hasInventory = stockMap.Count > 0;

        var items = productGroups.Select(g =>
        {
            var demandPredicted = g.Sum(f => f.PredictedQuantity);
            double? stockActual = hasInventory && stockMap.TryGetValue(g.Key, out var stock) ? stock : null;
            if (hasInventory && !stockActual.HasValue)
                throw new ValidationException("inventory", $"El producto '{g.Key}' no tiene stock informado. Incluye una fila con cero si no hay existencias.");
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

        bool hasPharmacyPlan = pharmacyPlanFile is not null;
        Dictionary<string, double> pharmacyPlan = [];
        if (hasPharmacyPlan)
        {
            if (string.IsNullOrWhiteSpace(pharmacyProductCol) || string.IsNullOrWhiteSpace(pharmacyQuantityCol))
                throw new ValidationException("pharmacyPlan", "Mapea producto y cantidad del plan de la farmacia.");
            (pharmacyPlan, _) = ParseCsvColumns(pharmacyPlanFile!, pharmacyProductCol, pharmacyQuantityCol, null);
        }

        static double CalcAbsError(double planned, double actual) =>
            Math.Abs(planned - actual);

        static string AccuracyLabel(double absErr, double actual) =>
            actual <= 0 ? (absErr == 0 ? "good" : "poor")
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
        var normalizedPharmacy = pharmacyPlan.GroupBy(kv => NormName(kv.Key))
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
        var planKeys = plan.Items.Select(i => NormName(i.ProductName)).ToHashSet();
        var missingSales = plan.Items.Where(i => !normalizedSales.ContainsKey(NormName(i.ProductName)))
            .Select(i => i.ProductName).ToList();
        var extraSales = actualSales.Keys.Where(name => !planKeys.Contains(NormName(name))).ToList();
        if (missingSales.Count > 0 || extraSales.Count > 0)
            throw new ValidationException("products",
                $"Productos sin correspondencia. Sin ventas informadas: {string.Join(", ", missingSales)}. " +
                $"Sin producto en el plan: {string.Join(", ", extraSales)}. Informa cero explícitamente cuando no hubo ventas.");
        if (hasPharmacyPlan && planKeys.Any(key => !normalizedPharmacy.ContainsKey(key)))
            throw new ValidationException("pharmacyPlan", "El plan de la farmacia debe informar cantidades para todos los productos evaluados, incluyendo ceros.");
        // Only evaluate products that appear in the actual-sales CSV.
        var items = plan.Items
            .Where(i => normalizedSales.ContainsKey(NormName(i.ProductName)))
            .Select(i =>
            {
                var key        = NormName(i.ProductName);
                var actualSold = normalizedSales[key];

                double pharmaCastPlan = i.RecommendedQuantity;
                double? pharmacyQty = normalizedPharmacy.TryGetValue(key, out var qty) ? qty : null;
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
        if (n == 0) throw new ValidationException("products", "No hay productos coincidentes entre el plan y las ventas reales.");
        int pcGood = items.Count(i => i.PharmaCastAccuracy == "good");
        int pharmGood = items.Count(i => i.PharmacyAccuracy == "good");
        int pharmacyCount = items.Count(i => i.PharmacyPlan.HasValue);

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
            PharmaCastGoodPct              = Math.Round(100.0 * pcGood / n, 1),
            PharmacyGoodPct                = pharmacyCount > 0 ? Math.Round(100.0 * pharmGood / pharmacyCount, 1) : 0,
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
        var constraint = DatasetFileValidator.GetConstraintError(file);
        if (constraint is not null) throw new ValidationException("file", constraint);
        if (string.IsNullOrWhiteSpace(productCol) || string.IsNullOrWhiteSpace(primaryCol))
            throw new ValidationException("mapping", "Selecciona las columnas de producto y cantidad.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var stream = file.OpenReadStream();
        var config = new ExcelReaderConfiguration { FallbackEncoding = System.Text.Encoding.GetEncoding(1252), AutodetectSeparators = [',', ';', '\t'] };
        using var reader = ext == ".csv" ? ExcelReaderFactory.CreateCsvReader(stream, config)
            : ExcelReaderFactory.CreateReader(stream, config);
        var primary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var secondary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (!reader.Read()) throw new ValidationException("file", "El archivo está vacío.");
        var headers = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetValue(i)?.ToString()?.Trim().TrimStart('\uFEFF') ?? "").ToList();
        int Column(string name)
        {
            var matches = headers.Select((h, i) => (h, i)).Where(x => string.Equals(x.h, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) throw new ValidationException("mapping", $"La columna '{name}' debe existir una sola vez en el archivo.");
            return matches[0].i;
        }
        var productIndex = Column(productCol);
        var quantityIndex = Column(primaryCol);
        var priceIndex = string.IsNullOrWhiteSpace(secondaryCol) ? -1 : Column(secondaryCol);
        double Number(int index)
        {
            var raw = reader.GetValue(index);
            var ok = double.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value);
            if (!ok || !double.IsFinite(value) || value < 0)
                throw new ValidationException("file", $"La columna '{headers[index]}' contiene una cantidad inválida o negativa.");
            return value;
        }
        while (reader.Read())
        {
            if (Enumerable.Range(0, reader.FieldCount).All(i => string.IsNullOrWhiteSpace(reader.GetValue(i)?.ToString()))) continue;
            var product = reader.GetValue(productIndex)?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(product)) throw new ValidationException("file", "Hay filas sin nombre de producto.");
            primary[product] = primary.GetValueOrDefault(product) + Number(quantityIndex);
            if (priceIndex >= 0) secondary[product] = Number(priceIndex);
        }
        if (primary.Count == 0) throw new ValidationException("file", "El archivo debe contener al menos una fila de datos.");
        return (primary, secondary);
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
