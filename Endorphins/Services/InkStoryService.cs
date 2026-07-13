using Endorphins.Models;
using Ink;
using Ink.Runtime;
namespace Endorphins.Services;

public sealed class InkStoryService
{
    public string? EditorContent => _editorContent;
    
    public Action<string>? InkScriptSelected { get; set; }
    public Action<DialogLine>? DialogUpdated { get; set; }
    public Action? StoryReset { get; set; }
    public Action? ChoicesReset { get; set; }
    public Action<string>? InkCompileStateUpdated { get; set; }
    public Action<List<Choice>>? ChoicesAdded { get; set; }

    private Story? _activeStory;
    private const string MainCharacter = "Aiden";
    private string? _editorContent;
    
    public void Run()
    {
        if(_editorContent == null) return;
        _activeStory = ParseEditorContentToStory(_editorContent);
        ContinueStory();
    }

    public void ContinueStory()
    {
        if (CanContinue())
        {
            var nextLine = _activeStory.Continue();
            var dialogueLine = new DialogLine
            {
                Speaker = "Speaker",
                Line = nextLine,
                IsMainCharacter = nextLine.Contains(MainCharacter),
            };
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
        StoryReset?.Invoke();
    }
    
    public void MakeChoice(int index)
    {
        if (_activeStory?.currentChoices.Count <= 0) return;
        _activeStory?.ChooseChoiceIndex(index);
        ContinueStory();
        ChoicesReset?.Invoke();
    }

    public void SetContent(string newContent)
    {
        _editorContent = newContent;
    }
    
    public void Setup(Story story)
    {
        SetErrorHandling(story);
        BindExternalFunctions(story);
    }

    public void GoToKnot(Story story, string knotId)
    {
        story.ChoosePathString(knotId);
        story.Continue();
    }

    public void GoToStitch(Story story, string stitchId)
    {
        //Knot.Stitch
        story.ChoosePathString(stitchId);
        story.Continue();
    }
    
    private static Story ParseEditorContentToStory(string editorContent)
    {
        var parser = new InkParser(editorContent);
        var parsed = parser.Parse();
        var story = parsed.ExportRuntime();
        return story;
    }
    
    private bool HasChoices()
    {
        return _activeStory?.currentChoices.Count != 0;
    }

    private void AddChoices()
    {
        if (!HasChoices()) return;
        ChoicesAdded?.Invoke(_activeStory.currentChoices);
    }
    
    private bool CanContinue()
    {
        return _activeStory != null && _activeStory.canContinue;
    }

    private void CreateVariableObserver(string variableName, Story story)
    {
        if (!HasVariable(variableName, story)) return;
        story.ObserveVariable(variableName, (name, newValue) => { Callback(name, (int)newValue); });
    }

    private void SetVariableState(string variableName, object value, Story story)
    {
        story.variablesState[variableName] = value;
    }

    private T GetVariableState<T>(string variableName, Story story)
    {
        return (T)story.variablesState[variableName];
    }

    private void Callback(string variableName, int newValue)
    {
        Console.WriteLine($"Variable name: {variableName} with value: {newValue}");
    }

    private void SaveState(Story story)
    {
        var savedState = story?.state.ToJson();
        Console.WriteLine(savedState);
    }

    private void LoadState(string savedState, Story story)
    {
        story?.state.LoadJson(savedState);
    }

    private static bool HasVariable(string variableName, Story story)
    {
        var variable = story?.variablesState[variableName];
        return variable != null;
    }

    private void BindExternalFunctions(Story story)
    {
        Console.WriteLine($"Binding external functions to story: {story}");
        story.BindExternalFunction<string>("PlaySound", ServiceFunctions.PlaySound, true);
        story.BindExternalFunction<string, int>("DisplayImage", ServiceFunctions.DisplayImage, true);
        story.BindExternalFunction<string>("AnimateCharacter", ServiceFunctions.AnimateCharacter, true);
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
    public static void DisplayImage(string imageName, int duration)
    {
        Console.WriteLine($"Displaying image: {imageName} for {duration}");
    }

    public static void PlaySound(string soundName)
    {
        Console.WriteLine($"Playing sound: {soundName}");
    }

    public static void AnimateCharacter(string characterId)
    {
        Console.WriteLine($"Animating character: {characterId}");
    }
}