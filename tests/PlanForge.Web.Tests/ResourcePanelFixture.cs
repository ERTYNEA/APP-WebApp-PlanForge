namespace PlanForge.Web.Tests;

using PlanForge.Contracts.Library;
using PlanForge.Contracts.Planning;

public sealed class ResourcePanelFixture : IDisposable
{
	public ResourcePanelFixture()
	{
		Context=new ComponentTestContext(async request=>
		{
			string path=request.RequestUri!.AbsolutePath;
			if(request.Method!=HttpMethod.Get)
			{
				Body=request.Content is null ? null : await request.Content.ReadAsByteArrayAsync();
				Headers=request.Headers.ToDictionary(header=>header.Key,header=>header.Value.Single());
				Commands.Add((request.Method.Method,path));
				return ComponentTestContext.Json(path.EndsWith("/orphans/clean",StringComparison.Ordinal) ? (object)new{deletedCount=1,skippedCount=0} : Resource);
			}
			object value=path.EndsWith("/usage",StringComparison.Ordinal) ? new ResourceUsage(3,10000,1024,false) : path.EndsWith("/projects/summary",StringComparison.Ordinal) ? new ProjectSummaryPage([new(ProjectId,1,"Target project","","Pending","PlanningOnly",DateTimeOffset.UtcNow,1,0,0,0,0,0)],1,0,100,false) : path.StartsWith("/api/v1/projects/",StringComparison.Ordinal) ? path.EndsWith("/resources",StringComparison.Ordinal) ? new[]{Resource} : new ProjectSnapshot(ProjectId,1,"Target project","","Pending","PlanningOnly",DateTimeOffset.UtcNow,[new(PhaseId,"Phase",null,0,"Target phase")]) : new ResourcePage([Resource],1,0,50);
			return ComponentTestContext.Json(value);
		});
	}
	public Guid ProjectId { get; }=Guid.NewGuid();
	public Guid PhaseId { get; }=Guid.NewGuid();
	public ComponentTestContext Context { get; }
	public ResourceProfile Resource { get; set; }=new(Guid.NewGuid(),"Test file","test.txt","text/plain",3,"hash",2,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,[],[]);
	public byte[]? Body { get; private set; }
	public Dictionary<string,string> Headers { get; private set; }=[];
	public List<(string Method,string Path)> Commands { get; }=[];
	public void Dispose() => Context.Dispose();
}
