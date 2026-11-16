namespace PlanForge.Api.Avatars;

using PlanForge.Application.Accounts;
using PlanForge.Infrastructure.Accounts;
using System.Diagnostics;

/// <summary>Runs native image work in an owned, disposable child, never in the HTTP or queue host process.</summary>
public sealed class IsolatedAvatarConverter : IAvatarConverter
{
	private readonly Func<ProcessStartInfo> createStartInfo;

	private TimeSpan ExecutionLimit { get; }

	public IsolatedAvatarConverter() : this(CreateStartInfo) { }
	public IsolatedAvatarConverter(Func<ProcessStartInfo> createStartInfo, TimeSpan? executionLimit = null)
	{
		this.createStartInfo = createStartInfo;
		ExecutionLimit = executionLimit ?? TimeSpan.FromSeconds(20);
	}

	public async Task<byte[]> ConvertAsync(byte[] bytes, string contentType, CancellationToken cancellationToken = default)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(ExecutionLimit);
		cancellationToken = deadline.Token;
		cancellationToken.ThrowIfCancellationRequested();
		ProcessStartInfo start = createStartInfo();
		start.RedirectStandardInput = true;
		start.RedirectStandardOutput = true;
		start.RedirectStandardError = true;
		start.UseShellExecute = false;
		start.ArgumentList.Add(contentType);
		using var process = new Process { StartInfo = start };
		process.Start();
		try
		{
			await process.StandardInput.BaseStream.WriteAsync(bytes, cancellationToken);
			process.StandardInput.Close();
			byte[] result = await ReadBoundedAsync(process.StandardOutput.BaseStream, cancellationToken);
			await process.WaitForExitAsync(cancellationToken);
			if (process.ExitCode != 0 || result.Length == 0)
				throw new InvalidDataException("The avatar conversion process failed.");
			return result;
		}
		finally
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
			}
		}
	}

	/// <summary>Private stdio protocol used only by this application's child process. No host, SQL or credentials are initialized.</summary>
	public static async Task<int> RunChildAsync(string contentType)
	{
		try
		{
			byte[] input = await ReadBoundedAsync(Console.OpenStandardInput(), CancellationToken.None);
			var validator = new SkiaAvatarImageValidator();
			byte[] output = await new SkiaAvatarImageConverter(validator).ConvertAsync(input, contentType);
			await Console.OpenStandardOutput().WriteAsync(output);
			return 0;
		}
		catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or PlanForge.Application.Common.PlanForgeException)
		{
			// Never emit image bytes, metadata, arguments or exception messages to diagnostic output.
			return 1;
		}
	}

	private static ProcessStartInfo CreateStartInfo()
	{
		var start = new ProcessStartInfo("dotnet");
		start.ArgumentList.Add(typeof(IsolatedAvatarConverter).Assembly.Location);
		start.ArgumentList.Add("--avatar-convert");
		return start;
	}

	private static async Task<byte[]> ReadBoundedAsync(Stream source, CancellationToken cancellationToken)
	{
		using var output = new MemoryStream();
		byte[] buffer = new byte[8192];
		int count;
		while ((count = await source.ReadAsync(buffer, cancellationToken)) != 0)
		{
			if (output.Length + count > AvatarValidator.MaximumBytes)
				throw new InvalidDataException("Avatar conversion protocol limit exceeded.");
			output.Write(buffer, 0, count);
		}
		return output.ToArray();
	}
}
