using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;
using SharpHook.Native;
using SharpHook.Reactive;
using Spectre.Console;

namespace Repro;

public static class Program
{
    private static readonly CancellationTokenSource TokenSource = new();
    private static readonly IReactiveGlobalHook     Hook       = new SimpleReactiveGlobalHook();
    private static          Layout                  _layout     = CreateLayout();
    private static          string                  _input      = string.Empty;
    private static          IDisposable?            _subscriptions;

    private static int ContentHeight => AnsiConsole.Profile.Height - 4;
    
    public static async Task Main(string[] args)
    {
        Console.TreatControlCAsInput = true;
        SubscribeInput();
        
        await Task.WhenAll(
            Hook.RunAsync().ToTask(),
            AnsiConsole.Live(_layout)
                       .AutoClear(false)
                       .Overflow(VerticalOverflow.Ellipsis)
                       .Cropping(VerticalOverflowCropping.Bottom)
                       .StartAsync(static ctx =>
                       {
                           while (!TokenSource.IsCancellationRequested)
                           {
                               ctx.UpdateTarget(_layout);
                               ctx.Refresh();
                           }
                           
                           return Task.CompletedTask;
                       }));
    }

    private static void Cancel()
    {
        TokenSource.Cancel();
        TokenSource.Token.WaitHandle.WaitOne();
        _subscriptions?.Dispose();
        Hook.Dispose();
        Console.Clear();
    }

    private static void SubscribeInput() =>
        _subscriptions = new CompositeDisposable(
            Hook.KeyTyped.Subscribe(e =>
            {
                if (e.Data.KeyCode is KeyCode.VcBackspace && _input != string.Empty)
                    _input = _input[..^1];
                else if (e.Data.KeyCode is not KeyCode.VcEnter && e.Data.KeyCode is not KeyCode.VcBackspace)
                    _input += (char) e.Data.KeyChar;

                _layout = CreateLayout(string.Join(string.Empty, _input.Reverse().Take(AnsiConsole.Profile.Width - 4).Reverse()));
    
                // e.SuppressEvent = true;
            }),
            Hook.KeyPressed.Subscribe(e =>
            {
                if (e.RawEvent.Mask.HasFlag(ModifierMask.LeftCtrl))
                {
                    if (e.Data.KeyCode == KeyCode.VcC)
                        Cancel();
                    if (e.Data.KeyCode == KeyCode.VcBackspace)
                        _input = string.Empty;
                }
                
                // e.SuppressEvent = true;
            }));

    private static Layout CreateLayout(string input = "")
    {
        var markup = new Markup(input);
        var grid = new Grid()
                   .Centered()
                   .Expand()
                   .AddColumn(new GridColumn());
        var available = Enumerable
                        .Range(0, ContentHeight)
                        .Select(i => new Markup(i.ToString()))
                        .ToArray();
        var _ = available.Select(c => grid.AddRow(c)).ToArray();
        grid.AddRow(new Rule());
        grid.AddRow(markup);
        var panel  = new Panel(grid).Expand();
        
        return new Layout(panel);
    }
}