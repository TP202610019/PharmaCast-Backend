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

        var config = new MappingConfiguration
        {
            UserId            = userId,
            ConfigurationName = request.ConfigurationName,
            IsDefault         = request.IsDefault
        };

        // Build field entities using the already-assigned Guid so EF cascades the insert
        var fieldEntities = await BuildFieldEntities(config.Id, request.Fields, ct);
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
        var fieldEntities = await BuildFieldEntities(config.Id, request.Fields, ct);
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
        foreach (var entry in entries)
        {
            var fieldDef = await _fields.GetByKeyAsync(entry.FieldKey, ct);
            if (fieldDef is null)
            {
                _logger.LogWarning("[MAPPING] Unknown field_key '{Key}' — skipping.", entry.FieldKey);
                continue;
            }

            result.Add(new MappingConfigurationField
            {
                MappingConfigurationId = configId,
                MappingFieldId         = fieldDef.Id,
                DatasetColumnName      = entry.DatasetColumnName
            });
        }
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
