namespace PlanForge.Application.Planning;

using PlanForge.Contracts.Planning;

public static class PlanProgress
{
	/// <summary>Derives hierarchy status from actual child execution and decision state.</summary>
	public static ProjectSnapshot Recalculate(ProjectSnapshot snapshot)
	{
		List<PlanItem> items = snapshot.Items.ToList();
		Dictionary<Guid, PlanItem> indexById = items.ToDictionary(item => item.Id);
		HashSet<Guid> pendingDecisionAncestors = [];
		foreach (PlanItem decision in items.Where(item => item.Kind == "Decision" && item.Status == "Pending"))
		{
			Guid? parentId = decision.ParentId;
			while (parentId is Guid id && indexById.TryGetValue(id, out PlanItem? parent))
			{
				pendingDecisionAncestors.Add(id);
				parentId = parent.ParentId;
			}
		}
		foreach (string kind in new[] { "Subphase", "Phase" })
			for (int index = 0; index < items.Count; index++)
			{
				PlanItem item = items[index];
				if (item.Kind == kind)
					items[index] = item with { Status = Derive(items.Where(child => child.ParentId == item.Id).ToList(), item.Status, pendingDecisionAncestors.Contains(item.Id)) };
			}
		return snapshot with { Items = items, Status = Derive(items.Where(item => item.Kind == "Phase").ToList(), snapshot.Status, pendingDecisionAncestors.Count > 0) };
	}

	private static string Derive(List<PlanItem> children, string requested, bool hasPendingDecision)
	{
		if (requested == "Blocked" || children.Any(item => item.Status == "Blocked"))
			return "Blocked";
		if (!hasPendingDecision && children.Any(item => item.Kind != "Decision") && children.All(item => item.Status == (item.Kind == "Decision" ? "Decided" : "Completed")))
			return "Completed";
		return requested == "InProgress" || children.Any(item => item.Status is "Completed" or "Decided" or "InProgress") ? "InProgress" : "Pending";
	}
}
