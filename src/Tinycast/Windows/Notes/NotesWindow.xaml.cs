using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinycast.DesignSystem;
using Tinycast.Features.Commands;
using Tinycast.Platform;

namespace Tinycast;

public sealed partial class NotesWindow : Window
{
    readonly AppCore _core;
    NoteDocument? _current;
    readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _saveTimer;
    bool _loading;

    public NotesWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, 640, 520);
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(300);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => PersistCurrent();
        TitleBox.TextChanged += (_, _) => ArmSave();
        BodyBox.TextChanged += (_, _) => ArmSave();
        Closed += (_, _) =>
        {
            _saveTimer.Stop();
            PersistCurrent();
        };
        ReloadKeeping(null);
    }

    void ArmSave()
    {
        if (_loading)
            return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void Show(NoteDocument note)
    {
        _loading = true;
        _current = note;
        TitleBox.Text = note.Title;
        BodyBox.Text = note.Text;
        _loading = false;
    }

    void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedIndex is >= 0 and var index && index < _core.Notes.Count)
        {
            PersistCurrent();
            Show(_core.Notes[index]);
        }
    }

    void PersistCurrent()
    {
        if (_current is null)
            return;
        if (TitleBox.Text == _current.Title && BodyBox.Text == _current.Text)
            return;
        _core.SaveNote(_current.Id, TitleBox.Text, BodyBox.Text);
        var updated = _core.Notes.FirstOrDefault(n => n.Id == _current.Id);
        if (updated is not null)
            _current = updated;
    }

    void OnNew(object sender, RoutedEventArgs e)
    {
        PersistCurrent();
        var note = _core.CreateNote("Untitled");
        ReloadKeeping(note.Id);
    }

    void OnSave(object sender, RoutedEventArgs e)
    {
        if (_current is null)
            _current = _core.CreateNote(TitleBox.Text);
        _core.SaveNote(_current.Id, TitleBox.Text, BodyBox.Text);
        ReloadKeeping(_current.Id);
        _core.ShowMessage("Note saved", DialogTone.Success);
    }

    public void Open(string id)
    {
        ReloadKeeping(id);
        Activate();
    }

    void ReloadKeeping(string? id)
    {
        var keep = id ?? _current?.Id;
        FileList.Items.Clear();
        foreach (var note in _core.Notes)
            FileList.Items.Add(note.Title);
        if (_core.Notes.Count == 0)
        {
            TitleBox.Text = "";
            BodyBox.Text = "";
            _current = null;
            return;
        }

        var index = Math.Max(0, _core.Notes.FindIndex(n => n.Id == keep));
        FileList.SelectedIndex = index;
        Show(_core.Notes[index]);
    }

    void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_current is null)
            return;
        _core.DeleteNote(_current.Id);
        _current = null;
        ReloadKeeping(null);
    }
}
