namespace PlanForge.Application.Library;

using PlanForge.Application.Accounts;
using PlanForge.Application.Common;
using PlanForge.Contracts;
using PlanForge.Contracts.Library;
using PlanForge.Domain.Library;
using System.Security.Cryptography;

public sealed class LibraryService(ILibraryStore store, ResourceLimits limits, TimeProvider timeProvider)
{
	private static readonly HashSet<string> allowedTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"application/octet-stream", "application/pdf", "application/zip", "application/json",
		"application/vnd.openxmlformats-officedocument.wordprocessingml.document",
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
		"application/vnd.openxmlformats-officedocument.presentationml.presentation",
		"image/avif", "image/gif", "image/jpeg", "image/png", "image/webp", "text/csv", "text/markdown", "text/plain"
	};

	public async Task<DownloadFile> DownloadAsync(AuthenticatedActor actor, Guid resourceId, CancellationToken cancellationToken = default)
	{
		Resource resource = await store.DownloadAsync(actor.UserId, resourceId, cancellationToken) ?? throw new PlanForgeException(404, "Resource not found.");
		if (resource.Content.LongLength != resource.Size || Convert.ToHexString(SHA256.HashData(resource.Content)) != resource.Sha256)
			throw new PlanForgeException(500, "The resource integrity check failed. Contact the owner before retrying.");
		return new DownloadFile(resource.Content, resource.ContentType, resource.FileName);
	}

	public Task<IReadOnlyList<ResourceProfile>> ListAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
		=> store.ListAsync(actor.UserId, cancellationToken);

	public async Task<ResourceProfile> UploadAsync(AuthenticatedActor actor, ResourceUploadRequest request, byte[] content, CancellationToken cancellationToken = default)
	{
		if (content is null || content.Length == 0)
			throw new PlanForgeException(400, "Choose a nonempty resource file.");
		if (content.Length > limits.MaximumFileBytes)
			throw new PlanForgeException(413, "The resource file exceeds the configured upload limit.");
		if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 240 || string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 500 || !allowedTypes.Contains(request.ContentType))
			throw new PlanForgeException(400, "Provide a supported MIME type, safe filename and title of up to 240 characters.");
		DateTimeOffset now = timeProvider.GetUtcNow();
		var resource = new Resource { OwnerUserId = actor.UserId, FileName = SafeFileName(request.FileName), Title = request.Title.Trim(), ContentType = request.ContentType.ToLowerInvariant(), Content = content, Size = content.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(content)), CreatedAt = now, UpdatedAt = now };
		return await store.UploadAsync(actor, resource, request.ProjectId, request.PhaseId, limits, cancellationToken, request.SubphaseId, request.StepId);
	}

	public Task<ResourceProfile> SetLinkAsync(AuthenticatedActor actor, Guid resourceId, ResourceLinkRequest request, CancellationToken cancellationToken = default)
	{
		if (new[] { request.ProjectId, request.PhaseId, request.SubphaseId, request.StepId }.Count(id => id.HasValue) != 1)
			throw new PlanForgeException(400, "Choose exactly one project, phase, subphase or step link.");
		return store.SetLinkAsync(actor, resourceId, request, cancellationToken);
	}
	public Task<int> CleanOrphansAsync(AuthenticatedActor actor, CleanOrphansRequest request, CancellationToken cancellationToken = default)
	{
		if (!request.Confirmed || request.ResourceIds is null || request.ResourceIds.Count > 1000 || request.ResourceIds.Any(id => id == Guid.Empty) || request.ResourceIds.Distinct().Count() != request.ResourceIds.Count)
			throw new PlanForgeException(400, "Confirm cleanup and provide up to 1000 unique resource IDs from the preview.");
		return store.CleanOrphansAsync(actor.UserId, request.ResourceIds, cancellationToken);
	}
	public Task<ResourceProfile> UpdateAsync(AuthenticatedActor actor, Guid resourceId, ResourceMetadataRequest request, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 240)
			throw new PlanForgeException(400, "Provide a resource title of up to 240 characters.");
		return store.UpdateAsync(actor, resourceId, request with { Title = request.Title.Trim() }, cancellationToken);
	}
	public Task DeleteAsync(AuthenticatedActor actor, Guid resourceId, long revision, bool confirmed, CancellationToken cancellationToken = default)
	{
		if (!confirmed)
			throw new PlanForgeException(400, "Confirm permanent resource deletion and removal of all its links.");
		return store.DeleteAsync(actor, resourceId, revision, cancellationToken);
	}

	public Task<IReadOnlyList<ResourceProfile>> ListProjectAsync(AuthenticatedActor actor, Guid projectId, Guid? phaseId = null, CancellationToken cancellationToken = default, string itemKind = "Phase")
		=> store.ListProjectAsync(actor.UserId, projectId, phaseId, cancellationToken, itemKind);
	public Task<ResourceUsage> UsageAsync(AuthenticatedActor actor, CancellationToken cancellationToken = default)
		=> store.UsageAsync(actor.UserId, limits, cancellationToken);

	public Task ValidateTargetsAsync(AuthenticatedActor actor, Guid? projectId, Guid? phaseId, CancellationToken cancellationToken = default, Guid? subphaseId = null, Guid? stepId = null)
		=> store.ValidateTargetsAsync(actor.UserId, projectId, phaseId, cancellationToken, subphaseId, stepId);

	public async Task<ResourceProfile> GetAsync(AuthenticatedActor actor, Guid resourceId, CancellationToken cancellationToken = default)
		=> await store.GetAsync(actor.UserId, resourceId, cancellationToken) ?? throw new PlanForgeException(404, "Resource not found.");

	public static string SafeFileName(string value)
	{
		string name = value.Replace('\\', '/').Split('/').Last();
		name = new string(name.Where(character => !char.IsControl(character)).ToArray());
		if (name is "" or "." or "..")
			name = "resource.bin";
		return name.Length > 180 ? throw new PlanForgeException(400, "The filename exceeds the 180 character limit.") : name;
	}
}
