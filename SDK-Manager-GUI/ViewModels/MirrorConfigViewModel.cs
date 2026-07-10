using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SDK_Manager_GUI.Models;
using SDK_Manager_GUI.Services;

namespace SDK_Manager_GUI.ViewModels
{
    public class MirrorConfigViewModel : ViewModelBase
    {
        private string _selectedLanguage = "NodeJs";
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value) && !string.IsNullOrEmpty(value))
                    _ = LoadMirrorsAsync();
            }
        }

        public ObservableCollection<string> LanguageOptions { get; } = new ObservableCollection<string>
        {
            "NodeJs", "Java", "Python"
        };

        private ObservableCollection<MirrorSourceItem> _mirrors;
        public ObservableCollection<MirrorSourceItem> Mirrors
        {
            get => _mirrors;
            set => SetProperty(ref _mirrors, value);
        }

        private MirrorSourceItem _selectedMirror;
        public MirrorSourceItem SelectedMirror
        {
            get => _selectedMirror;
            set => SetProperty(ref _selectedMirror, value);
        }

        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }

        // 添加/编辑镜像的输入字段
        private string _editName;
        public string EditName
        {
            get => _editName;
            set => SetProperty(ref _editName, value);
        }

        private string _editBaseUrl;
        public string EditBaseUrl
        {
            get => _editBaseUrl;
            set => SetProperty(ref _editBaseUrl, value);
        }

        private bool _editIsEnabled = true;
        public bool EditIsEnabled
        {
            get => _editIsEnabled;
            set => SetProperty(ref _editIsEnabled, value);
        }

        private int _editPriority;
        public int EditPriority
        {
            get => _editPriority;
            set => SetProperty(ref _editPriority, value);
        }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        private string _editModeTitle;
        public string EditModeTitle
        {
            get => _editModeTitle;
            set => SetProperty(ref _editModeTitle, value);
        }

        private readonly IMirrorProvider _mirrorProvider;
        private readonly IDialogService _dialogService;
        private readonly ILanguageService _languageService;

        public ICommand LoadMirrorsCommand { get; }
        public ICommand TestAllMirrorsCommand { get; }
        public ICommand TestMirrorCommand { get; }
        public ICommand AddMirrorCommand { get; }
        public ICommand EditMirrorCommand { get; }
        public ICommand RemoveMirrorCommand { get; }
        public ICommand SetDefaultMirrorCommand { get; }
        public ICommand SaveMirrorCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ToggleMirrorCommand { get; }
        public ICommand ResetMirrorsCommand { get; }

        public MirrorConfigViewModel(IMirrorProvider mirrorProvider, IDialogService dialogService, ILanguageService languageService)
        {
            _mirrorProvider = mirrorProvider;
            _dialogService = dialogService;
            _languageService = languageService;
            _mirrors = new ObservableCollection<MirrorSourceItem>();

            LoadMirrorsCommand = new RelayCommand(async () => await LoadMirrorsAsync());
            TestAllMirrorsCommand = new RelayCommand(async () => await TestAllMirrorsAsync(), () => !IsTesting);
            TestMirrorCommand = new RelayCommand<MirrorSourceItem>(async item => await TestSingleMirrorAsync(item));
            AddMirrorCommand = new RelayCommand(() => ShowAddMode());
            EditMirrorCommand = new RelayCommand<MirrorSourceItem>(item => ShowEditMode(item));
            RemoveMirrorCommand = new RelayCommand<MirrorSourceItem>(async item => await RemoveMirrorAsync(item), item => item != null && !item.IsDefault);
            SetDefaultMirrorCommand = new RelayCommand<MirrorSourceItem>(async item => await SetDefaultMirrorAsync(item));
            SaveMirrorCommand = new RelayCommand(async () => await SaveMirrorAsync());
            CancelEditCommand = new RelayCommand(() => IsEditMode = false);
            ToggleMirrorCommand = new RelayCommand<MirrorSourceItem>(async item => await ToggleMirrorAsync(item));
            ResetMirrorsCommand = new RelayCommand(async () => await ResetMirrorsAsync());
        }

        private async Task LoadMirrorsAsync()
        {
            if (!Enum.TryParse<SdkLanguage>(SelectedLanguage, out var language)) return;

            var mirrors = await _mirrorProvider.GetMirrorsAsync(language);
            Mirrors = new ObservableCollection<MirrorSourceItem>(
                mirrors.Select(m => new MirrorSourceItem
                {
                    Id = m.Id,
                    Name = m.Name,
                    BaseUrl = m.BaseUrl,
                    IsEnabled = m.IsEnabled,
                    Priority = m.Priority,
                    Latency = m.Latency,
                    IsDefault = m.IsDefault,
                    IsPreset = m.IsPreset
                }));
        }

        private async Task TestAllMirrorsAsync()
        {
            IsTesting = true;
            try
            {
                if (!Enum.TryParse<SdkLanguage>(SelectedLanguage, out var language)) return;

                var mirrors = await _mirrorProvider.GetMirrorsAsync(language);
                var testTasks = mirrors.Where(m => m.IsEnabled).Select(async m =>
                {
                    await _mirrorProvider.TestMirrorLatencyAsync(m);
                    return m;
                }).ToList();

                await Task.WhenAll(testTasks);
                await LoadMirrorsAsync();
            }
            finally
            {
                IsTesting = false;
            }
        }

        private async Task TestSingleMirrorAsync(MirrorSourceItem item)
        {
            if (item == null) return;

            if (!Enum.TryParse<SdkLanguage>(SelectedLanguage, out var language)) return;

            var mirrors = await _mirrorProvider.GetMirrorsAsync(language);
            var mirror = mirrors.FirstOrDefault(m => m.Id == item.Id);
            if (mirror == null) return;

            await _mirrorProvider.TestMirrorLatencyAsync(mirror);

            // 刷新列表以更新 LatencyDisplay
            await LoadMirrorsAsync();
        }

        private void ShowAddMode()
        {
            EditModeTitle = _languageService.GetString("Mirror_AddMirrorSource");
            IsEditMode = true;
            EditName = "";
            EditBaseUrl = "";
            EditIsEnabled = true;
            EditPriority = Mirrors.Count + 1;
        }

        private void ShowEditMode(MirrorSourceItem item)
        {
            if (item == null) return;

            EditModeTitle = _languageService.GetString("Mirror_EditMirrorSource");
            IsEditMode = true;
            EditName = item.Name;
            EditBaseUrl = item.BaseUrl;
            EditIsEnabled = item.IsEnabled;
            EditPriority = item.Priority;
        }

        private async Task SaveMirrorAsync()
        {
            if (string.IsNullOrWhiteSpace(EditName) || string.IsNullOrWhiteSpace(EditBaseUrl))
            {
                await _dialogService.ShowErrorAsync(_languageService.GetString("Dialog_InputError"), _languageService.GetString("Dialog_NameUrlRequired"));
                return;
            }

            if (!Enum.TryParse<SdkLanguage>(SelectedLanguage, out var language)) return;

            var mirror = new MirrorSource
            {
                Name = EditName.Trim(),
                BaseUrl = EditBaseUrl.Trim(),
                IsEnabled = EditIsEnabled,
                Priority = EditPriority,
                Language = language
            };

            var existing = SelectedMirror;
            if (existing != null && EditModeTitle == _languageService.GetString("Mirror_EditMirrorSource"))
            {
                mirror.Id = existing.Id;
                await _mirrorProvider.UpdateMirrorAsync(mirror);
            }
            else
            {
                await _mirrorProvider.AddMirrorAsync(mirror);
            }

            IsEditMode = false;
            await LoadMirrorsAsync();
        }

        private async Task RemoveMirrorAsync(MirrorSourceItem item)
        {
            if (item == null || item.IsDefault) return;

            var confirm = await _dialogService.ShowConfirmAsync(_languageService.GetString("Dialog_DeleteConfirm"), _languageService.GetString("Dialog_DeleteMirrorConfirm"));
            if (!confirm) return;

            await _mirrorProvider.RemoveMirrorAsync(item.Id);
            await LoadMirrorsAsync();
        }

        private async Task SetDefaultMirrorAsync(MirrorSourceItem item)
        {
            if (item == null) return;
            await _mirrorProvider.SetDefaultMirrorAsync(item.Id);
            await LoadMirrorsAsync();
        }

        private async Task ToggleMirrorAsync(MirrorSourceItem item)
        {
            if (item == null) return;

            if (!Enum.TryParse<SdkLanguage>(SelectedLanguage, out var language)) return;

            var mirror = new MirrorSource
            {
                Id = item.Id,
                Name = item.Name,
                BaseUrl = item.BaseUrl,
                IsEnabled = !item.IsEnabled,
                Priority = item.Priority,
                Language = language
            };

            await _mirrorProvider.UpdateMirrorAsync(mirror);
            await LoadMirrorsAsync();
        }

        private async Task ResetMirrorsAsync()
        {
            var confirm = await _dialogService.ShowConfirmAsync(_languageService.GetString("Dialog_ResetMirrorConfirm"), _languageService.GetString("Dialog_ResetMirrorConfirmMsg"));
            if (!confirm) return;

            await _mirrorProvider.ResetToDefaultAsync();
            await LoadMirrorsAsync();
        }
    }
}
