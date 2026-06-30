using PharmacyApiService.Domain.Enums;

namespace PharmacyApiService.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Analyst;
    public bool IsActive { get; set; } = true;

    public ICollection<UserSession> Sessions { get; set; } = [];
    public ICollection<Dataset> Datasets { get; set; } = [];
    public ICollection<MappingConfiguration> MappingConfigurations { get; set; } = [];
    public ICollection<PredictionExecution> PredictionExecutions { get; set; } = [];
}
