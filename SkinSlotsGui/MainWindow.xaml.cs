using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SkinSlotsCore;

namespace SkinSlotsGui;

public partial class MainWindow : Window
{
    private readonly GuiSettings _settings;
    private SkinSourcePreview? _preview;
    private int _previewGeneration;
    private bool _ready;
    private bool _busy;
    private bool _adjustingSlot;
    private int _slotMinimum = 2;
    private int _slotMaximum = AssignmentStore.LastPawnSuffix + 1;
    private string? _lastPak;
    private int _scanGeneration;
    private string? _previewSource;
    private readonly ObservableCollection<QueuedSkin> _queue = new();
    private readonly Dictionary<char, (string Name, string Description)> _languages = new();
    private char _language = 'A';
    private bool _loadingLanguage;

    public MainWindow()
    {
        InitializeComponent();
        _settings = GuiSettings.Load();
        ModsBox.Text = _settings.ModsDirectory;
        SourceBox.Text = _settings.SourcePath;
        UnrealPakBox.Text = _settings.UnrealPakExe;
        PawnBox.Text = _settings.PawnRoot;
        OutputBox.Text = _settings.OutputPakPath;
        ChooseSlotCheck.IsChecked = _settings.ChooseSlot;
        SetSlotText(_settings.Slot);
        SlotBox.Visibility = _settings.ChooseSlot ? Visibility.Visible : Visibility.Collapsed;

        QueueList.ItemsSource = _queue;
        LanguageBox.ItemsSource = SkinLanguage.All;
        LanguageBox.SelectedItem = SkinLanguage.All[0];
        Closing += (_, _) => SaveSettings();
        _ready = true;
        UpdateSlotHint();
        QueueModScan();
    }

    private void SaveSettings()
    {
        _settings.ModsDirectory = ModsBox.Text.Trim();
        _settings.SourcePath = SourceBox.Text.Trim();
        _settings.DisplayName = "";
        _settings.Description = "";
        _settings.UnrealPakExe = UnrealPakBox.Text.Trim();
        _settings.PawnRoot = PawnBox.Text.Trim();
        _settings.OutputPakPath = OutputBox.Text.Trim();
        _settings.ChooseSlot = ChooseSlotCheck.IsChecked == true;
        _settings.Slot = HangarSlot();
        _settings.Save();
    }

    private int HangarSlot()
    {
        if (!int.TryParse(SlotBox.Text.Trim(), out var slot))
            slot = _slotMinimum;
        if (slot < _slotMinimum)
            return _slotMinimum;
        if (slot > _slotMaximum)
            return _slotMaximum;
        return slot;
    }

    private void SetSlotText(int slot)
    {
        _adjustingSlot = true;
        SlotBox.Text = slot.ToString();
        _adjustingSlot = false;
    }

    private void ModsBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => QueueModScan();

    private void SourceBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => QueuePreview();

    private void Path_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => QueuePreview();

    private void ChooseSlotCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        SlotBox.Visibility = ChooseSlotCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateSlotHint();
    }

    private void SlotBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_ready || _adjustingSlot)
            return;
        UpdateSlotHint();
    }

    private void QueueModScan()
    {
        if (!_ready)
            return;

        var generation = ++_scanGeneration;
        _ = RunModScanAsync(generation);
    }

    private async Task RunModScanAsync(int generation)
    {
        try
        {
            await Task.Delay(300);
        }
        catch (Exception)
        {
            return;
        }

        if (generation != _scanGeneration)
            return;

        var mods = ModsBox.Text.Trim();
        ModFolderScan scan;
        try
        {
            scan = await Task.Run(() => ModFolder.Scan(mods));
        }
        catch (Exception ex)
        {
            if (generation != _scanGeneration)
                return;
            ModStatus.Text = ex.Message;
            ModStatus.Foreground = (Brush)FindResource("ErrorText");
            QueuePreview();
            return;
        }

        if (generation != _scanGeneration)
            return;

        ModStatus.Text = DescribeScan(mods, scan);
        ModStatus.Foreground = (Brush)FindResource("InfoText");
        QueuePreview();
    }

    private static string DescribeScan(string mods, ModFolderScan scan)
    {
        if (string.IsNullOrWhiteSpace(mods))
            return "Set the game's ~mods folder. The next slot is read from a SkinDataTable in that folder when one is there.";
        if (!Directory.Exists(mods))
            return "That folder was not found.";

        if (scan.DataPak != null)
        {
            return "SkinReSlotter data pak: " + Path.GetFileName(scan.DataPak) + Environment.NewLine
                + "The table and the names in that pak are updated in place, and the file keeps this name." + Environment.NewLine
                + "A skin pak that is already here keeps its textures.";
        }

        var table = scan.SkinTablePak == null
            ? "No SkinDataTable in this folder. New rows are added to the saved game table."
            : "SkinDataTable: " + Path.GetFileName(scan.SkinTablePak);
        var localization = scan.LocalizationPak == null
            ? "No localization in this folder. Names are added to the saved game files."
            : "Localization: " + Path.GetFileName(scan.LocalizationPak);
        return table + Environment.NewLine + localization + Environment.NewLine
            + "The table and the names are packed again as SkinReSlotter_Data_P, with leading tildes so that pak mounts last. A skin pak that is already here keeps its textures.";
    }

    private void QueuePreview()
    {
        if (!_ready)
            return;

        var generation = ++_previewGeneration;
        _ = RunPreviewAsync(generation);
    }

    private async Task RunPreviewAsync(int generation)
    {
        try
        {
            await Task.Delay(300);
        }
        catch (Exception)
        {
            return;
        }

        if (generation != _previewGeneration)
            return;

        var source = SourceBox.Text.Trim();
        if (!File.Exists(source) && !Directory.Exists(source))
        {
            _preview = null;
            _previewSource = null;
            PreviewText.Text = "No source loaded.";
            PreviewText.Foreground = (Brush)FindResource("MutedText");
            ClearMaterialChoices();
            UpdateSlotHint();
            return;
        }

        var unrealPak = UnrealPakBox.Text.Trim();
        var pawns = PawnBox.Text.Trim();
        var mods = ModsBox.Text.Trim();
        try
        {
            var preview = await Task.Run(() => SkinSlotBuilder.Inspect(source, unrealPak, pawns, mods));
            if (generation != _previewGeneration)
                return;

            _preview = preview;
            _previewSource = source;
            FillMaterialChoices(preview);
            var pawn = !preview.HasTemplatePawn
                ? "none"
                : preview.ShippedPawn
                    ? preview.TemplatePawn + " from the pak"
                    : preview.TemplatePawn;
            PreviewText.Text = string.Join(Environment.NewLine,
                "Plane: " + preview.PlaneLabel,
                "Skin " + preview.SourceSkinNumber + " (" + preview.SourceSlotIndex + ")",
                "Stock pawn: " + pawn,
                "Data in the PAK:",
                "Texture: " + YesNo(preview.ShipsDiffuse, preview.ShipsDiffuseUasset),
                "MREC: " + YesNo(preview.ShipsMrec, preview.ShipsMrecUasset),
                "Normals: " + YesNo(preview.ShipsNormalFile, preview.ShipsNormalUasset),
                "Instance: " + YesNo(preview.ShipsInstance),
                "Decal instance: " + YesNo(preview.ShipsDecal));
            PreviewText.Foreground = (Brush)FindResource(preview.HasHangarMaterial && preview.HasTemplatePawn ? "InfoText" : "WarningText");
        }
        catch (Exception ex)
        {
            if (generation != _previewGeneration)
                return;
            _preview = null;
            _previewSource = null;
            ClearMaterialChoices();
            PreviewText.Text = "Could not read this skin. " + ex.Message;
            PreviewText.Foreground = (Brush)FindResource("ErrorText");
        }

        UpdateSlotHint();
    }

    private void UpdateSlotHint()
    {
        if (!_ready)
            return;

        var sourceReady = _preview != null
            && string.Equals(_previewSource, SourceBox.Text.Trim(), StringComparison.OrdinalIgnoreCase);
        var canCreate = !_busy && sourceReady && _preview!.HasTemplatePawn && (_preview.HasHangarMaterial || SelectedSlot(InstanceBox) != null);
        int? blockedSlot = null;
        if (_preview == null)
        {
            SlotHint.Text = "The next slot is the number after the latest skin in the SkinDataTable.";
        }
        else
        {
            var stockMinimum = _preview.LastStockSuffix + 2;
            var next = _preview.NextHangarSlot;
            var maximum = AssignmentStore.LastPawnSuffix + 1;
            var current = int.TryParse(SlotBox.Text.Trim(), out var parsed) ? parsed : next;
            if (_slotMinimum != stockMinimum || _slotMaximum != maximum
                || (ChooseSlotCheck.IsChecked != true && current != next))
            {
                _slotMaximum = maximum;
                _slotMinimum = stockMinimum;
                if (ChooseSlotCheck.IsChecked != true || current < stockMinimum || current > maximum)
                    SetSlotText(next);
            }

            var origin = _preview.TableFromMods ? "the mod SkinDataTable" : "the saved game table";
            if (ChooseSlotCheck.IsChecked == true)
            {
                var hangar = HangarSlot();
                var suffix = hangar - 1;
                if (hangar < stockMinimum)
                {
                    blockedSlot = hangar;
                    SlotHint.Text = $"Slot {hangar} is one of the original skins. {_preview.PlaneLabel} already has skins through {_preview.LastStockLabel}.";
                }
                else if (_preview.SkinNos.Contains(suffix))
                {
                    SlotHint.Text = $"Slot {hangar} is already in {origin}. Its skin pak is left alone when that pak is already in the mods folder. The table and names are written again.";
                }
                else
                {
                    SlotHint.Text = $"Slot {hangar} uses pawn AcePlayerPawn_{_preview.Plane}_s{suffix:D2}.";
                }
            }
            else if (next > maximum)
            {
                canCreate = false;
                SlotHint.Text = $"No slot left for {_preview.PlaneLabel}.";
            }
            else
            {
                SlotHint.Text = $"{_preview.PlaneLabel} goes through slot {_preview.ThroughHangar} in {origin}. Next is slot {next}.";
            }

            if (!_preview.HasTemplatePawn)
            {
                canCreate = false;
                SlotHint.Text = $"The library has no pawn for {_preview.PlaneLabel} slot {_preview.SourceSkinNumber}, and this pak does not include one.";
            }
        }

        var formReady = canCreate && blockedSlot == null;
        AddButton.IsEnabled = formReady;
        StartButton.IsEnabled = !_busy && (_queue.Count > 0 || formReady);
        RemoveButton.IsEnabled = !_busy && QueueList.SelectedItem != null;
        QueueBox.Header = _queue.Count == 0 ? "Queue" : $"Queue ({_queue.Count})";
        QueueEmpty.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!TryEnqueueCurrent())
            return;
        UpdateSlotHint();
    }

    private bool TryEnqueueCurrent()
    {
        if (_busy || _preview == null || _previewSource == null)
            return false;
        if (!string.Equals(_previewSource, SourceBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        if (!_preview.HasHangarMaterial && SelectedSlot(InstanceBox) == null)
            return false;

        var specific = ChooseSlotCheck.IsChecked == true;
        if (specific && HangarSlot() < _preview.LastStockSuffix + 2)
            return false;

        RememberLanguage();
        var english = _languages['A'];
        var translations = _languages
            .Where(pair => pair.Key != 'A' && (!string.IsNullOrWhiteSpace(pair.Value.Name) || !string.IsNullOrWhiteSpace(pair.Value.Description)))
            .ToDictionary(
                pair => pair.Key,
                pair => new SkinLocale
                {
                    Name = BlankToNull(pair.Value.Name),
                    Description = BlankToNull(pair.Value.Description)
                });
        var item = new QueuedSkin
        {
            SourcePath = SourceBox.Text.Trim(),
            DisplayName = english.Name,
            Description = english.Description,
            Languages = translations,
            Slot = specific ? HangarSlot() : null,
            BodySlot = SelectedSlot(InstanceBox),
            DecalSlot = SelectedSlot(DecalBox),
            MaterialSlot = SelectedSlot(MaterialBox),
            NormalSlot = SelectedSlot(NormalBox),
            OutputPakPath = BlankToNull(OutputBox.Text),
            PlaneLabel = _preview.PlaneLabel
        };
        _queue.Add(item);
        _languages.Clear();
        _loadingLanguage = true;
        _language = 'A';
        LanguageBox.SelectedItem = SkinLanguage.All[0];
        NameBox.Clear();
        DescriptionBox.Clear();
        _loadingLanguage = false;
        LanguageHint.Text = "This name and description are written into every language.";
        OutputBox.Clear();
        SourceBox.Clear();
        Log($"Queued {item.Title}.");
        return true;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        LogBox.Clear();
        TryEnqueueCurrent();
        if (_queue.Count == 0)
            return;

        _busy = true;
        UpdateSlotHint();
        BusyBar.Visibility = Visibility.Visible;
        ShowButton.Visibility = Visibility.Collapsed;
        ResultText.Visibility = Visibility.Collapsed;

        var mods = BlankToNull(ModsBox.Text);
        var unrealPak = BlankToNull(UnrealPakBox.Text);
        var pawns = string.IsNullOrWhiteSpace(PawnBox.Text) ? PawnLibrary.DefaultRoot : PawnBox.Text.Trim();
        var completed = 0;
        string? failure = null;

        while (_queue.Count > 0)
        {
            var item = _queue[0];
            if (completed > 0)
                Log("");
            Log(item.Title);
            try
            {
                var result = await Task.Run(() => SkinSlotBuilder.Build(item.Request(mods, unrealPak, pawns), Log));
                _lastPak = result.DataPakPath;
                completed++;
                var skin = result.ContentSkipped
                    ? $"Textures left in place: {Path.GetFileName(result.PakPath)}."
                    : $"Skin pak: {Path.GetFileName(result.PakPath)}.";
                Log($"{result.Manifest.Name} is slot {result.Manifest.SkinNo + 1} on {result.Manifest.Plane}. {skin}");
                Log($"Skin pak: {result.PakPath}");
                Log($"Table pak: {result.DataPakPath}");
                _queue.RemoveAt(0);
            }
            catch (Exception ex)
            {
                failure = item.Title + " was not created. " + ex.Message;
                Log(ex.Message);
                break;
            }
        }

        if (failure == null)
            ShowResult(completed == 1 ? "1 skin created." : $"{completed} skins created.", "SuccessText");
        else if (completed == 0)
            ShowResult(failure, "ErrorText");
        else
            ShowResult($"{completed} created. {failure}", "ErrorText");

        ShowButton.Visibility = string.IsNullOrWhiteSpace(_lastPak) ? Visibility.Collapsed : Visibility.Visible;
        _busy = false;
        BusyBar.Visibility = Visibility.Collapsed;
        QueueModScan();
        UpdateSlotHint();
    }

    private void LanguageBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready || _loadingLanguage || LanguageBox.SelectedItem is not SkinLanguage language)
            return;
        RememberLanguage();
        _language = language.Letter;
        _loadingLanguage = true;
        if (_languages.TryGetValue(language.Letter, out var text))
        {
            NameBox.Text = text.Name;
            DescriptionBox.Text = text.Description;
        }
        else
        {
            NameBox.Clear();
            DescriptionBox.Clear();
        }
        _loadingLanguage = false;
        LanguageHint.Text = language.Letter == 'A'
            ? "This name and description are written into every language."
            : "A blank name or description here keeps the English text.";
    }

    private void RememberLanguage()
    {
        _languages[_language] = (NameBox.Text, DescriptionBox.Text);
    }

    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelected();

    private void QueueList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_ready)
            UpdateSlotHint();
    }

    private void QueueList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            RemoveSelected();
    }

    private void RemoveSelected()
    {
        if (_busy || QueueList.SelectedItem is not QueuedSkin item)
            return;
        _queue.Remove(item);
        UpdateSlotHint();
    }

    private sealed class QueuedSkin
    {
        public required string SourcePath { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
        public required IReadOnlyDictionary<char, SkinLocale> Languages { get; init; }
        public int? Slot { get; init; }
        public string? BodySlot { get; init; }
        public string? DecalSlot { get; init; }
        public string? MaterialSlot { get; init; }
        public string? NormalSlot { get; init; }
        public string? OutputPakPath { get; init; }
        public required string PlaneLabel { get; init; }

        public string Title
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(DisplayName) ? "(unnamed)" : DisplayName.Trim();
                var slot = Slot == null ? "next slot" : $"slot {Slot}";
                return $"{PlaneLabel} — {name} — {slot}";
            }
        }

        public string Detail
        {
            get
            {
                var line = $"{Path.GetFileName(SourcePath)} · instance {ChoiceLabel(BodySlot)} · decal {ChoiceLabel(DecalSlot)} · material {ChoiceLabel(MaterialSlot)} · normal {ChoiceLabel(NormalSlot)}";
                if (!string.IsNullOrWhiteSpace(Description))
                {
                    var description = Description.Trim().Replace("\r\n", " ").Replace('\n', ' ');
                    line += " · " + description;
                }
                if (Languages.Count > 0)
                {
                    var names = string.Join(", ", Languages.Keys.OrderBy(letter => letter).Select(letter => SkinLanguage.All.First(language => language.Letter == letter).Name));
                    line += " · " + names;
                }
                return line;
            }
        }

        public SkinSlotBuildRequest Request(string? mods, string? unrealPak, string pawns) => new()
        {
            SourcePath = SourcePath,
            UnrealPakExe = unrealPak,
            OutputPakPath = OutputPakPath,
            ModsDirectory = mods,
            PawnRoot = pawns,
            DisplayName = DisplayName,
            Description = Description,
            Languages = Languages,
            Slot = Slot,
            BodySlot = BodySlot,
            DecalSlot = DecalSlot,
            MaterialSlot = MaterialSlot,
            NormalSlot = NormalSlot
        };

        private static string ChoiceLabel(string? slot) =>
            slot == "mod" ? "from the mod" : slot == "shared" ? "shared texture folder" : slot ?? "saved";
    }

    private string? _choiceKey;

    private sealed class SlotChoice
    {
        public required string Slot { get; init; }
        public string? Caption { get; init; }
        public string Label => Caption ?? (int.TryParse(Slot, out var number) ? $"{Slot} (slot {number + 1})" : Slot);
    }

    private void FillMaterialChoices(SkinSourcePreview preview)
    {
        var key = preview.Plane + ":" + preview.SourceSlotIndex;
        var keep = key == _choiceKey;
        var body = keep ? SelectedSlot(InstanceBox) : null;
        var decal = keep ? SelectedSlot(DecalBox) : null;
        var material = keep ? SelectedSlot(MaterialBox) : null;
        var normal = keep ? SelectedSlot(NormalBox) : null;
        _choiceKey = key;
        if (preview.SharedTextures)
        {
            InstanceBox.ItemsSource = new[] { SharedCaption() };
            DecalBox.ItemsSource = new[] { preview.ShipsDecal ? SharedCaption() : new SlotChoice { Slot = preview.SourceSlotIndex } };
            MaterialBox.ItemsSource = new[] { preview.MaterialFromShared ? SharedCaption() : new SlotChoice { Slot = preview.SharedMaterialSlot ?? preview.SourceSlotIndex } };
            NormalBox.ItemsSource = new[] { preview.NormalFromShared ? SharedCaption() : new SlotChoice { Slot = preview.NormalReferenceSlot ?? preview.SourceSlotIndex } };
            InstanceBox.SelectedIndex = 0;
            DecalBox.SelectedIndex = 0;
            MaterialBox.SelectedIndex = 0;
            NormalBox.SelectedIndex = 0;
            InstanceBox.IsEnabled = false;
            DecalBox.IsEnabled = false;
            MaterialBox.IsEnabled = false;
            NormalBox.IsEnabled = false;
            return;
        }
        var pawns = string.IsNullOrWhiteSpace(PawnBox.Text) ? PawnLibrary.DefaultRoot : PawnBox.Text.Trim();
        var bodies = PawnLibrary.BodySlots(pawns, preview.Plane).Select(slot => new SlotChoice { Slot = slot }).ToList();
        var decals = PawnLibrary.DecalSlots(pawns, preview.Plane).Select(slot => new SlotChoice { Slot = slot }).ToList();
        var materials = TextureChoices(PawnLibrary.MrecSlots(pawns, preview.Plane).Select(slot => new SlotChoice { Slot = slot }).ToList(), preview.ShipsMaterial);
        var normals = TextureChoices(PawnLibrary.NormalSlots(pawns, preview.Plane).Select(slot => new SlotChoice { Slot = slot }).ToList(), preview.ShipsNormal);
        InstanceBox.ItemsSource = bodies;
        DecalBox.ItemsSource = decals;
        MaterialBox.ItemsSource = materials;
        NormalBox.ItemsSource = normals;
        InstanceBox.SelectedItem = bodies.FirstOrDefault(item => item.Slot == (body ?? preview.SourceSlotIndex)) ?? bodies.FirstOrDefault();
        DecalBox.SelectedItem = decals.FirstOrDefault(item => item.Slot == (decal ?? preview.SourceSlotIndex)) ?? decals.FirstOrDefault();
        MaterialBox.SelectedItem = materials.FirstOrDefault(item => item.Slot == (material ?? (preview.ShipsMaterial ? "mod" : preview.MaterialReferenceSlot ?? preview.SourceSlotIndex))) ?? materials.FirstOrDefault();
        NormalBox.SelectedItem = normals.FirstOrDefault(item => item.Slot == (normal ?? (preview.ShipsNormal ? "mod" : preview.NormalReferenceSlot ?? preview.SourceSlotIndex))) ?? normals.FirstOrDefault();
        InstanceBox.IsEnabled = !preview.SharedTextures;
        DecalBox.IsEnabled = !preview.SharedTextures;
        MaterialBox.IsEnabled = !preview.SharedTextures && !preview.BulkOnly;
        NormalBox.IsEnabled = !preview.SharedTextures && !preview.BulkOnly;
    }

    private static SlotChoice SharedCaption() => new() { Slot = "shared", Caption = "Shared texture folder" };

    private static List<SlotChoice> TextureChoices(List<SlotChoice> slots, bool fromMod)
    {
        var choices = new List<SlotChoice>();
        if (fromMod)
            choices.Add(new SlotChoice { Slot = "mod", Caption = "From the mod" });
        choices.AddRange(slots);
        return choices;
    }

    private void ClearMaterialChoices()
    {
        _choiceKey = null;
        InstanceBox.ItemsSource = null;
        DecalBox.ItemsSource = null;
        MaterialBox.ItemsSource = null;
        NormalBox.ItemsSource = null;
        InstanceBox.IsEnabled = true;
        DecalBox.IsEnabled = true;
        MaterialBox.IsEnabled = true;
        NormalBox.IsEnabled = true;
    }

    private static string YesNo(bool present, bool hasHeader = true) => !present ? "no" : hasHeader ? "yes" : "yes (ubulk)";

    private static string? SelectedSlot(System.Windows.Controls.ComboBox box) =>
        (box.SelectedItem as SlotChoice)?.Slot;

    private void ShowResult(string message, string brushKey)
    {
        ResultText.Text = message;
        ResultText.Foreground = (Brush)FindResource(brushKey);
        ResultText.Visibility = Visibility.Visible;
    }

    private static string? BlankToNull(string text)
    {
        var trimmed = text.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private void Log(string line)
    {
        if (Dispatcher.CheckAccess())
            AppendLog(line);
        else
            Dispatcher.BeginInvoke(() => AppendLog(line));
    }

    private void AppendLog(string line)
    {
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.CaretIndex = LogBox.Text.Length;
        LogBox.ScrollToEnd();
    }

    private void ShowPak_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastPak))
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{_lastPak}\"",
            UseShellExecute = true
        });
    }

    private void BrowseMods_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();
        if (path != null)
            ModsBox.Text = path;
    }

    private void BrowsePak_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFile("Pak (*.pak)|*.pak");
        if (path != null)
            SourceBox.Text = path;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();
        if (path != null)
            SourceBox.Text = path;
    }

    private void BrowseUnrealPak_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFile("Executable (*.exe)|*.exe");
        if (path != null)
            UnrealPakBox.Text = path;
    }

    private void BrowsePawns_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();
        if (path != null)
            PawnBox.Text = path;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Pak (*.pak)|*.pak",
            FileName = "SkinReSlotter_Skin_P.pak"
        };
        if (dialog.ShowDialog(this) == true)
            OutputBox.Text = dialog.FileName;
    }

    private string? PickFile(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private string? PickFolder()
    {
        var dialog = new OpenFolderDialog();
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        var path = files[0];
        if (!string.IsNullOrWhiteSpace(path))
            SourceBox.Text = path;
    }
}
