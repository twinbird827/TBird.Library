using LanobeReader.Helpers;
using NUnit.Framework;
using TBird.Core;

namespace LanobeReader.Tests;

public class CommandGuardTests
{
    private RecordingMessageService _messages = null!;

    [SetUp]
    public void SetUp()
    {
        _messages = new RecordingMessageService();
        MessageService.SetService(_messages);
    }

    [TearDown]
    public void TearDown() => MessageService.SetService(new ConsoleMessageService());

    [Test]
    public async Task RunAsyncSwallowsExceptionAndReportsIt()
    {
        string? shown = null;

        await CommandGuard.RunAsync(
            () => throw new InvalidOperationException("boom"),
            m => shown = m,
            "利用者向け文言");

        Assert.That(shown, Is.EqualTo("利用者向け文言"));
        Assert.That(_messages.Errors, Has.Count.EqualTo(1));
        Assert.That(_messages.Errors[0].Message, Does.StartWith($"{nameof(RunAsyncSwallowsExceptionAndReportsIt)} failed: System.InvalidOperationException: boom"));
        Assert.That(_messages.Errors[0].Member, Is.EqualTo(nameof(RunAsyncSwallowsExceptionAndReportsIt)));
    }

    [Test]
    public async Task RunAsyncDoesNotReportOnSuccess()
    {
        string? shown = null;

        await CommandGuard.RunAsync(() => Task.CompletedTask, m => shown = m, "利用者向け文言");

        Assert.That(shown, Is.Null);
        Assert.That(_messages.Errors, Is.Empty);
    }

    private sealed class RecordingMessageService : IMessageService
    {
        public List<(string Message, string Member)> Errors { get; } = [];

        public void Error(string message, string callerMemberName = "", string callerFilePath = "", int callerLineNumber = 0)
            => Errors.Add((message, callerMemberName));

        public void Info(string message, string callerMemberName = "", string callerFilePath = "", int callerLineNumber = 0) { }

        public void Warn(string message, string callerMemberName = "", string callerFilePath = "", int callerLineNumber = 0) { }

        public bool Confirm(string message, string callerMemberName = "", string callerFilePath = "", int callerLineNumber = 0) => true;

        public void Debug(string message, string callerMemberName = "", string callerFilePath = "", int callerLineNumber = 0) { }

        public void Exception(Exception exception, string callerMemberName = "", string callerFilePath = "", int callerLineNumber = 0) { }
    }
}
