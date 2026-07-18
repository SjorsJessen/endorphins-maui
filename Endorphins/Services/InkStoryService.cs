using Endorphins.Models;
using Ink;
using Ink.Runtime;
using Path = System.IO.Path;

namespace Endorphins.Services;


public sealed class ProjectFileHandler : IFileHandler
{
    private readonly string _root;
    public ProjectFileHandler(string root) => _root = root;

    public string ResolveInkFilename(string includeName)
        => Path.Combine(_root, includeName);

    public string LoadInkFileContents(string fullFilename)
        => File.ReadAllText(fullFilename);
}

public enum InkCompileState
{
    NotLoaded,
    Compiling,
    Compiled,
    Error,
}

public sealed record InkDiagnostic(string Message, ErrorType Type);

/// <summary>Documentation entry for a function offered by the ink editor's Functions dropdown.</summary>
public sealed record InkFunctionDoc(string Signature, string Description, string Snippet);

public sealed class InkStoryService
{
    public const string MainScriptPath = "scripts/main.ink";

    /// <summary>External functions this runtime binds (see <see cref="BindExternalFunctions"/>) —
    /// keep the two in sync when adding a binding.</summary>
    public static readonly IReadOnlyList<InkFunctionDoc> ExternalFunctions =
    [
        new("updateSpeaker(name)",  "Set the active speaker's name and portrait.", "~ updateSpeaker(\"speaker\")\n"),
        new("setImage(name)",       "Display an image asset by name.",             "~ setImage(\"image\")\n"),
        new("setBackground(name)",  "Set the background image by asset name.",     "~ setBackground(\"background\")\n"),
        new("playAudio(name)",      "Play an audio asset by name.",                "~ playAudio(\"audio\")\n"),
        new("addDivider()",         "Insert a section break between passages.",    "~ addDivider()\n"),
        new("transition(title)",    "Display a centered bold header.",             "~ transition(\"title\")\n"),
    ];

    /// <summary>Built-in ink functions available in any story.</summary>
    public static readonly IReadOnlyList<InkFunctionDoc> BuiltInFunctions =
    [
        new("RANDOM(min, max)",       "Random integer between min and max (inclusive).", "RANDOM(1, 6)"),
        new("SEED_RANDOM(seed)",      "Seed the random number generator.",               "~ SEED_RANDOM(1)\n"),
        new("CHOICE_COUNT()",         "Number of choices generated so far this turn.",   "CHOICE_COUNT()"),
        new("TURNS()",                "Number of game turns since the story began.",     "TURNS()"),
        new("TURNS_SINCE(-> knot)",   "Turns since the knot/stitch was last visited.",   "TURNS_SINCE(-> knot)"),
        new("POW(x, y)",              "x raised to the power y.",                        "POW(2, 3)"),
        new("FLOOR(x)",               "Round down to an integer.",                       "FLOOR(x)"),
        new("CEILING(x)",             "Round up to an integer.",                         "CEILING(x)"),
        new("INT(x)",                 "Truncate to an integer.",                         "INT(x)"),
        new("FLOAT(x)",               "Convert to a floating-point number.",             "FLOAT(x)"),
    ];

    public bool HasStoryStarted { get; private set; }
    public string? EditorContent => _editorContent;
    public string ActiveScriptPath { get; set; } = string.Empty;
    public bool IsMainScriptActive =>
        string.Equals(ActiveScriptPath.Replace('\\', '/'), MainScriptPath, StringComparison.OrdinalIgnoreCase);
    public bool IsStoryLoaded => _activeStory != null;

    public InkCompileState CompileState { get; private set; } = InkCompileState.NotLoaded;
    public IReadOnlyList<InkDiagnostic> Diagnostics => _diagnostics;

    // Story playback state lives here (singleton) so it survives the runner
    // component being torn down and recreated on tab switches.
    public IReadOnlyList<DialogLine> DialogLines => _dialogLines;
    public IReadOnlyList<Choice> Choices => _choices;

    public Action<string>? InkScriptSelected { get; set; }
    public Action<DialogLine>? DialogUpdated { get; set; }
    public Action? StoryReset { get; set; }
    public Action? ChoicesReset { get; set; }
    public Action? CompileStateChanged { get; set; }
    public Action<List<Choice>>? ChoicesAdded { get; set; }

    private Story? _activeStory;
    private const string MainCharacter = "Aiden";
    private string? _editorContent;
    private readonly List<DialogLine> _dialogLines = [];
    private readonly List<InkDiagnostic> _diagnostics = [];
    private List<Choice> _choices = [];
    private string _currentSpeaker;

    public void Run(string root, string scriptPath)
    {
        var story = Compile(root, scriptPath);
        if (story is null) return;
        _activeStory = story;
        ClearHistory();
        StoryReset?.Invoke();
        Setup(_activeStory);
        ContinueStory();
    }

    /// <summary>
    /// Compiles the current editor content, capturing parser errors/warnings into
    /// <see cref="Diagnostics"/> and updating <see cref="CompileState"/>.
    /// Returns the runtime story on success, null when there are errors.
    /// </summary>
    public Story? Compile(string root, string scriptPath)
    {
        if (_editorContent == null) return null;
        SetCompileState(InkCompileState.Compiling);
        _diagnostics.Clear();

        Story? story = null;
        try
        {
            story = ParseEditorContentToStory(root, scriptPath, _editorContent);
        }
        catch (Exception ex)
        {
            _diagnostics.Add(new InkDiagnostic(ex.Message, ErrorType.Error));
        }

        var hasErrors = _diagnostics.Any(d => d.Type == ErrorType.Error) || story is null;
        if (hasErrors && _diagnostics.Count == 0)
        {
            _diagnostics.Add(new InkDiagnostic("Compilation produced no output.", ErrorType.Error));
        }
        SetCompileState(hasErrors ? InkCompileState.Error : InkCompileState.Compiled);
        return hasErrors ? null : story;
    }

    private void SetCompileState(InkCompileState state)
    {
        CompileState = state;
        CompileStateChanged?.Invoke();
    }

    public void ContinueStory()
    {
        HasStoryStarted = true;
        if (CanContinue())
        {
            var nextLine = _activeStory.Continue();
            var type = _currentSpeaker switch
            {
                "Narrator" => DialogType.Narrator,
                MainCharacter => DialogType.Main,
                _ => DialogType.Npc
            };
            var dialogueLine = new DialogLine
            {
                Speaker = _currentSpeaker,
                Line = nextLine,
                Type = type
            };
            _dialogLines.Add(dialogueLine);
            DialogUpdated?.Invoke(dialogueLine);
        }
        else if(HasChoices())
        {
            AddChoices();
        }
        else
        {
            Console.WriteLine("End of story.");
        }
    }

    public void ResetStory()
    {
        _activeStory?.ResetState();
        ClearHistory();
        StoryReset?.Invoke();
    }

    public void MakeChoice(int index)
    {
        if (_activeStory?.currentChoices.Count <= 0) return;
        _activeStory?.ChooseChoiceIndex(index);
        _choices.Clear();
        ContinueStory();
        ChoicesReset?.Invoke();
    }

    private void ClearHistory()
    {
        _dialogLines.Clear();
        _choices.Clear();
    }

    public void SetContent(string newContent)
    {
        _editorContent = newContent;
    }

    private void Setup(Story story)
    {
        SetErrorHandling(story);
        BindExternalFunctions(story);
    }
    
    private Story? ParseEditorContentToStory(string filesRoot, string scriptPath, string editorContent)
    {
        var fileHandler = new ProjectFileHandler(filesRoot);
        var parser = new InkParser(
            str: editorContent,
            filenameForMetadata: scriptPath,
            externalErrorHandler: OnCompileDiagnostic,
            fileHandler: fileHandler
        );
        var parsed = parser.Parse();
        var story = parsed?.ExportRuntime(OnCompileDiagnostic);
        return story;
    }

    private void OnCompileDiagnostic(string message, ErrorType type)
    {
        _diagnostics.Add(new InkDiagnostic(message, type));
    }
    
    private bool HasChoices()
    {
        return _activeStory?.currentChoices.Count != 0;
    }

    private void AddChoices()
    {
        if (!HasChoices()) return;
        _choices = [.._activeStory.currentChoices];
        ChoicesAdded?.Invoke(_choices);
    }
    
    private bool CanContinue()
    {
        return _activeStory != null && _activeStory.canContinue;
    }

    private void AddDivider()
    {
        _dialogLines.Add(new DialogLine { Speaker = string.Empty, Line = "***", Type = DialogType.Divider });
    }
    
    private void Transition(string title)
    {
        Console.WriteLine($"Setting title: {title}");
        _dialogLines.Add(new DialogLine { Speaker = string.Empty, Line = title, Type = DialogType.Title });
    }

    private void UpdateSpeaker(string speakerName)
    {
        _currentSpeaker = speakerName;
    }   
    
    private void BindExternalFunctions(Story story)
    {
        Console.WriteLine($"Binding external functions to story: {story}");
        story.BindExternalFunction<string>("setImage", ServiceFunctions.SetImage, true);
        story.BindExternalFunction<string>("setBackground", ServiceFunctions.SetBackground, true);
        // lookaheadSafe must be false: UpdateSpeaker mutates _currentSpeaker, and the
        // ink engine evaluates ahead of the current line to detect glue. If it were
        // lookahead-safe, the next line's updateSpeaker would fire while the current
        // line is still being emitted, attributing the current line to the wrong speaker.
        story.BindExternalFunction<string>("updateSpeaker", UpdateSpeaker, false);
        story.BindExternalFunction<string>("playAudio", ServiceFunctions.PlayAudio, true);
        story.BindExternalFunction("addDivider", AddDivider, false);
        story.BindExternalFunction<string>("transition", Transition, false);
    }

    private static void SetErrorHandling(Story story)
    {
        story.onError += (message, type) =>
        {
            switch (type)
            {
                case ErrorType.Warning:
                    Console.WriteLine($"Warning message: {message}");
                    break;
                case ErrorType.Error:
                    Console.Error.WriteLine(message);
                    break;
                case ErrorType.Author:
                    Console.WriteLine(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        };
        story.onMakeChoice += (choice) => { Console.WriteLine($"Made choice: {choice.text}"); };
        story.onChoosePathString += (path, objs) => { Console.WriteLine($"Path: {path}"); };
    }
}


public static class ServiceFunctions
{
    public static void SetImage(string imageName)
    {
        Console.WriteLine($"Set image: {imageName}");
    }    
    
    public static void SetBackground(string imageName)
    {
        Console.WriteLine($"Set background: {imageName}");
    }
    
    
    public static void PlayAudio(string audioName)
    {
        Console.WriteLine($"Playing: {audioName}");
    }    

}