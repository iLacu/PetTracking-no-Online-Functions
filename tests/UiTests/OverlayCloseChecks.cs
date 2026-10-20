using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PetTracker;

static class OverlayCloseChecks
{
    public static void Run(Action<bool, string> check, string folder, WorkerView view, Action<Window> show, Action<int> pump)
    {
        string root = Path.Combine(folder, "close-entire-application");
        using var host = new Host(root, false);
        host.Preview(view with { Settings = new AppSettings { ReducedMotion = true, ExpireInactive = false }, Recent = new() { new(1050, null, "", DateTimeOffset.UtcNow) } });
        var overlay = host.Overlay!;
        show(overlay);
        pump(20);
        int requested = 0;
        host.QuitRequested += () => requested++;
        overlay.Close();
        check(requested == 1 && overlay.IsVisible, "Window close requests application exit instead of hiding in the background");
        var button = FindButton(overlay);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(requested == 2 && overlay.IsVisible, "The tracker X uses the same complete-application exit request");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var received = Task.Run(async () =>
        {
            using var pipe = new NamedPipeServerStream(Storage.Pipe(root), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(cancellation.Token);
            using var reader = new StreamReader(pipe);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            var command = JsonSerializer.Deserialize<Command>((await reader.ReadLineAsync(cancellation.Token))!);
            await writer.WriteLineAsync(JsonSerializer.Serialize(view with { Protocol = 3 }));
            return command;
        });
        var quit = host.StopWorkerForQuit();
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while ((!quit.IsCompleted || !received.IsCompleted) && DateTime.UtcNow < deadline) pump(10);
        check(quit.IsCompleted && received.IsCompleted, "Application exit waits for the local capture shutdown acknowledgement");
        quit.GetAwaiter().GetResult();
        check(received.GetAwaiter().GetResult()!.Action == "exit", "The complete close path sends the worker its save-and-exit command");
        host.Dispose();
        host.Dispose();
        pump(10);
        check(!overlay.IsVisible && !Application.Current.Windows.Cast<Window>().Contains(overlay), "Successful quit closes the window completely and disposal can safely run twice");
    }

    static Button FindButton(System.Windows.DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Button button && System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "QuitApplication") return button;
            var nested = FindButton(child);
            if (nested != null) return nested;
        }
        return null!;
    }
}
