namespace PlanForge.Web.State;

using PlanForge.Contracts.Planning;

/// <summary>Shares the already-loaded project name with navigation without a second plan request.</summary>
public sealed class BreadcrumbState
{
	public event Action? Changed;
	public Guid? ProjectId { get; private set; }
	public string? ProjectName { get; private set; }
	public bool SettingsOpen { get; private set; }

	public void SetProject(ProjectSnapshot? project)
	{
		if (ProjectId == project?.Id && ProjectName == project?.Name)
			return;
		if (ProjectId != project?.Id)
			SettingsOpen = false;
		ProjectId = project?.Id;
		ProjectName = project?.Name;
		Changed?.Invoke();
	}

	public void SetSettings(bool open)
	{
		if (SettingsOpen == open)
			return;
		SettingsOpen = open;
		Changed?.Invoke();
	}
}
