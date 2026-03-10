using OnTimeCRM.Domain.Common;

namespace OnTimeCRM.Domain.Entities;

/// <summary>
/// Stores UI-level CRUD permission per role per application route.
/// Backend API enforcement uses [ManagerOnly] / [Authorize]; this table controls frontend visibility.
/// </summary>
public class MenuItemPermission : BaseEntity
{
    /// <summary>User role: 0=Salesperson, 1=Manager</summary>
    public int Role { get; set; }

    /// <summary>Unique slug matching the frontend route key, e.g. "/clients", "/stages"</summary>
    public string RouteKey { get; set; } = string.Empty;

    public bool CanView { get; set; } = true;
    public bool CanCreate { get; set; } = true;
    public bool CanEdit { get; set; } = true;
    public bool CanDelete { get; set; } = true;
}
