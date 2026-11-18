using Microsoft.Extensions.Logging;
using PharmacyApiService.Application.Interfaces.Repositories;
using PharmacyApiService.Application.Interfaces.Services;
using PharmacyApiService.Domain.Entities;
using PharmacyApiService.Domain.Exceptions;
using PharmacyApiService.Presentation.DTOs.Mapping;

namespace PharmacyApiService.Application.Services;

public class MappingConfigService : IMappingConfigService
{
    private readonly IMappingConfigRepository _repo;
    private readonly IMappingFieldRepository _fields;
    private readonly ILogger<MappingConfigService> _logger;

    public MappingConfigService(
        IMappingConfigRepository repo,
        IMappingFieldRepository fields,
        ILogger<MappingConfigService> logger)
    {
        _repo   = repo;
        _fields = fields;
        _logger = logger;
    }

    public async Task<MappingConfigResponse> CreateAsync(CreateMappingRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = new MappingConfiguration
        {
            UserId = userId,
            ConfigurationName = request.ConfigurationName,
            IsDefault = request.IsDefault
        };
        var fieldEntities = await BuildFieldEntities(config.Id, request.Fields, ct);

        // Find-or-create: return the existing config if the user already has one with this name.
        var duplicate = await _repo.GetByNameAsync(userId, request.ConfigurationName, ct);
        if (duplicate is not null)
        {
            _logger.LogInformation(
                "[MAPPING-REUSE] User {UserId} submitted duplicate mapping name '{Name}'; returning existing id={Id}.",
                userId, request.ConfigurationName, duplicate.Id);
            return MapToResponse(duplicate);
        }

        if (request.IsDefault)
        {
            var currentDefault = await _repo.GetDefaultForUserAsync(userId, ct);
            if (currentDefault is not null)
            {
                var tracked = await _repo.GetTrackedByIdAsync(currentDefault.Id, ct);
                if (tracked is not null) { tracked.IsDefault = false; _repo.Update(tracked); }
            }
        }

        foreach (var fe in fieldEntities)
            config.Fields.Add(fe);

        await _repo.AddAsync(config, ct);
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[MAPPING] Created mapping '{Name}' (id={Id}) for user {UserId} — {Count} field(s).",
            config.ConfigurationName, config.Id, userId, fieldEntities.Count);

        var full = await _repo.GetByIdWithFieldsAsync(config.Id, ct) ?? config;
        return MapToResponse(full);
    }

    public async Task<MappingConfigResponse> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdWithFieldsAsync(id, ct)
            ?? throw new NotFoundException(nameof(MappingConfiguration), id);

        if (config.UserId != userId)
            throw new ForbiddenException();

        return MapToResponse(config);
    }

    public async Task<IReadOnlyList<MappingConfigResponse>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var configs = await _repo.GetByUserAsync(userId, ct);
        return configs.Select(MapToResponse).ToList();
    }

    public async Task<MappingConfigResponse> UpdateAsync(Guid id, CreateMappingRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = await _repo.GetTrackedByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MappingConfiguration), id);

        if (config.UserId != userId)
            throw new ForbiddenException();

        var fieldEntities = await BuildFieldEntities(config.Id, request.Fields, ct);
        if (request.IsDefault && !config.IsDefault)
        {
            var existing = await _repo.GetDefaultForUserAsync(userId, ct);
            if (existing is not null && existing.Id != id)
            {
                var tracked = await _repo.GetTrackedByIdAsync(existing.Id, ct);
                if (tracked is not null) { tracked.IsDefault = false; _repo.Update(tracked); }
            }
        }

        config.ConfigurationName = request.ConfigurationName;
        config.IsDefault         = request.IsDefault;
        config.UpdatedAt         = DateTime.UtcNow;

        // Replace all fields: remove old, add new
        config.Fields.Clear();
        foreach (var fe in fieldEntities)
            config.Fields.Add(fe);

        _repo.Update(config);
        await _repo.SaveChangesAsync(ct);

        var full = await _repo.GetByIdWithFieldsAsync(config.Id, ct) ?? config;
        return MapToResponse(full);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MappingConfiguration), id);

        if (config.UserId != userId)
            throw new ForbiddenException();

        _repo.Remove(config);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var existing = await _repo.GetDefaultForUserAsync(userId, ct);
        if (existing is not null && existing.Id != id)
        {
            var tracked = await _repo.GetTrackedByIdAsync(existing.Id, ct);
            if (tracked is not null) { tracked.IsDefault = false; _repo.Update(tracked); }
        }

        var config = await _repo.GetTrackedByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(MappingConfiguration), id);

        if (config.UserId != userId)
            throw new ForbiddenException();

        config.IsDefault = true;
        _repo.Update(config);
        await _repo.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<MappingConfigurationField>> BuildFieldEntities(
        Guid configId,
        IEnumerable<MappingFieldEntry> entries,
        CancellationToken ct)
    {
        var result = new List<MappingConfigurationField>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new HashSet<string>();
        foreach (var entry in entries)
        {
            var fieldDef = await _fields.GetByKeyAsync(entry.FieldKey, ct);
            if (fieldDef is null || !fieldDef.IsActive)
                throw new ValidationException("fields", $"El campo '{entry.FieldKey}' no está disponible.");
            if (string.IsNullOrWhiteSpace(entry.DatasetColumnName) || !keys.Add(fieldDef.FieldKey))
                throw new ValidationException("fields", $"Asigna una sola columna no vacía a '{fieldDef.DisplayName}'.");
            types.Add(fieldDef.FieldType);

            result.Add(new MappingConfigurationField
            {
                MappingConfigurationId = configId,
                MappingFieldId         = fieldDef.Id,
                DatasetColumnName      = entry.DatasetColumnName.Trim()
            });
        }
        var missing = new List<string>();
        if (result.Count == 0 || types.Contains("sales"))
        {
            if (!keys.Contains("date")) missing.Add("fecha");
            if (!keys.Contains("product") && !keys.Contains("product_id")) missing.Add("producto de ventas");
            if (!keys.Contains("quantity")) missing.Add("cantidad");
        }
        if (types.Contains("inventory"))
        {
            if (!keys.Contains("inv_product")) missing.Add("producto de inventario");
            if (!keys.Contains("stock")) missing.Add("stock");
        }
        if (missing.Count > 0)
            throw new ValidationException("fields", $"Completa los campos obligatorios: {string.Join(", ", missing)}.");
        return result;
    }

    private static MappingConfigResponse MapToResponse(MappingConfiguration c) => new()
    {
        Id                = c.Id,
        ConfigurationName = c.ConfigurationName,
        IsDefault         = c.IsDefault,
        CreatedAt         = c.CreatedAt,
        UpdatedAt         = c.UpdatedAt,
        Fields = c.Fields
            .OrderBy(f => f.MappingField?.FieldType)
            .ThenByDescending(f => f.MappingField?.IsRequired ?? false)
            .ThenBy(f => f.MappingField?.FieldKey)
            .Select(f => new MappingFieldEntryResponse
            {
                FieldKey          = f.MappingField?.FieldKey    ?? string.Empty,
                DatasetColumnName = f.DatasetColumnName,
                DisplayName       = f.MappingField?.DisplayName ?? string.Empty,
                FieldType         = f.MappingField?.FieldType   ?? string.Empty,
                IsRequired        = f.MappingField?.IsRequired  ?? false,
            }).ToList()
    };
}
