namespace PlanForge.Api.Tests;

using PlanForge.Api.Avatars;
using PlanForge.Infrastructure.Accounts;
using SkiaSharp;
using System.Diagnostics;

public sealed class IsolatedAvatarConverterTests
{
	[Fact]
	public async Task ActualApiChild_ConvertsValidRasterToValidatedBoundedWebpAsync()
	{
		byte[] input = AvatarHttpImage.Create();
		byte[] result = await new IsolatedAvatarConverter().ConvertAsync(input, "image/png");
		new SkiaAvatarImageValidator().Validate(result, "image/webp");
		using SKBitmap image = SKBitmap.Decode(result);
		Assert.InRange(image.Width, 1, 512);
		Assert.InRange(image.Height, 1, 512);
	}

	[Fact]
	public async Task ExecutionDeadline_StopsNoncooperativeChildWithoutCallerCancellationAsync()
	{
		var start = new ProcessStartInfo("python3");
		start.ArgumentList.Add("-c");
		start.ArgumentList.Add("import sys,time;sys.stdin.buffer.read();time.sleep(60)");
		using var cleanup = new CancellationTokenSource();
		Task<byte[]> operation = new IsolatedAvatarConverter(() => start, TimeSpan.FromMilliseconds(300)).ConvertAsync([1], "image/png", cleanup.Token);
		try
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(3)));
		}
		finally
		{
			cleanup.Cancel();
			try { await operation; } catch (OperationCanceledException) when (cleanup.IsCancellationRequested) { }
		}
	}

	[Fact]
	public async Task Cancellation_ReturnsOnlyAfterOwnedNoncooperativeChildHasExitedAsync()
	{
		string marker = Path.Combine(AppContext.BaseDirectory, "converter-" + Guid.NewGuid().ToString("N") + ".pid");
		var start = new ProcessStartInfo("python3");
		start.ArgumentList.Add("-c");
		start.ArgumentList.Add("import os,sys,time;open(sys.argv[1],'w').write(str(os.getpid()));sys.stdin.buffer.read();time.sleep(60)");
		start.ArgumentList.Add(marker);
		using var cancellation = new CancellationTokenSource();
		Task<byte[]> operation = new IsolatedAvatarConverter(() => start).ConvertAsync([1], "image/png", cancellation.Token);
		Process? child = null;
		try
		{
			var wait = Stopwatch.StartNew();
			while (!File.Exists(marker) && wait.Elapsed < TimeSpan.FromSeconds(5))
				await Task.Delay(10);
			Assert.True(File.Exists(marker));
			child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker)));
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
			Assert.True(child.HasExited, "Conversion returned cancellation while its native reader process was still alive.");
		}
		finally
		{
			cancellation.Cancel();
			if (child is not null && !child.HasExited)
			{
				child.Kill(entireProcessTree: true);
				await child.WaitForExitAsync();
			}
			child?.Dispose();
			File.Delete(marker);
		}
	}
}
