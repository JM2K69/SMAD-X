using System.ComponentModel;
using SMADX.Services;

namespace SMADX.Helpers
{
    /// <summary>
    /// Wrapper observable pour la localisation qui expose les traductions comme propriétés individuelles
    /// </summary>
    public class LocalizationProxy : INotifyPropertyChanged
    {
        private readonly LocalizationService _localizationService;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizationProxy()
        {
            _localizationService = LocalizationService.Instance;

            // S'abonner aux changements de langue
            _localizationService.PropertyChanged += (s, e) =>
            {
                // Notifier tous les changements de propriétés
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            };
        }

        // Propriétés individuelles pour chaque traduction
        // Menu File
        public string MenuFile => _localizationService["Menu.File"];
        public string MenuFileNewDomain => _localizationService["Menu.File.NewDomain"];
        public string MenuFileNewStructure => _localizationService["Menu.File.NewStructure"];
        public string MenuFileOpen => _localizationService["Menu.File.Open"];
        public string MenuFileSave => _localizationService["Menu.File.Save"];
        public string MenuFileExportJson => _localizationService["Menu.File.ExportJson"];
        public string MenuFileExportPowerShell => _localizationService["Menu.File.ExportPowerShell"];
        public string MenuFileGenerateImportScript => _localizationService["Menu.File.GenerateImportScript"];
        public string MenuFileExit => _localizationService["Menu.File.Exit"];

        // Menu Edit
        public string MenuEdit => _localizationService["Menu.Edit"];
        public string MenuEditCopy => _localizationService["Menu.Edit.Copy"];
        public string MenuEditPaste => _localizationService["Menu.Edit.Paste"];
        public string MenuEditRename => _localizationService["Menu.Edit.Rename"];
        public string MenuEditDelete => _localizationService["Menu.Edit.Delete"];
        public string MenuEditAddPSO => _localizationService["Menu.Edit.AddPSO"];

        // Menu View
        public string MenuView => _localizationService["Menu.View"];
        public string MenuViewExpandAll => _localizationService["Menu.View.ExpandAll"];
        public string MenuViewCollapseAll => _localizationService["Menu.View.CollapseAll"];
        public string MenuViewTheme => _localizationService["Menu.View.Theme"];
        public string MenuViewThemeLight => _localizationService["Menu.View.Theme.Light"];
        public string MenuViewThemeDark => _localizationService["Menu.View.Theme.Dark"];
        public string MenuViewLanguage => _localizationService["Menu.View.Language"];
        public string MenuViewLanguageFrench => _localizationService["Menu.View.Language.French"];
        public string MenuViewLanguageEnglish => _localizationService["Menu.View.Language.English"];

        // Menu Settings
        public string MenuSettings => _localizationService["Menu.Settings"];
        public string MenuSettingsManageTiers => _localizationService["Menu.Settings.ManageTiers"];

        // Toolbar
        public string ToolbarOU => _localizationService["Toolbar.OU"];
        public string ToolbarContainer => _localizationService["Toolbar.Container"];
        public string ToolbarUser => _localizationService["Toolbar.User"];
        public string ToolbarGroup => _localizationService["Toolbar.Group"];
        public string ToolbarComputer => _localizationService["Toolbar.Computer"];
        public string ToolbarGMSA => _localizationService["Toolbar.GMSA"];
        public string ToolbarPolicy => _localizationService["Toolbar.Policy"];
        public string ToolbarPSO => _localizationService["Toolbar.PSO"];
        public string ToolbarDelete => _localizationService["Toolbar.Delete"];
        public string ToolbarRelations => _localizationService["Toolbar.Relations"];
        public string ToolbarGraph => _localizationService["Toolbar.Graph"];
        public string MenuViewManageRelations => _localizationService["Menu.View.ManageRelations"];
        public string MenuViewShowGraph => _localizationService["Menu.View.ShowGraph"];

        // Details
        public string DetailsTitle => _localizationService["Details.Title"];
        public string DetailsName => _localizationService["Details.Name"];
        public string DetailsType => _localizationService["Details.Type"];
        public string DetailsTier => _localizationService["Details.Tier"];
        public string DetailsDN => _localizationService["Details.DN"];
        public string DetailsDescription => _localizationService["Details.Description"];
        public string DetailsActions => _localizationService["Details.Actions"];
        public string DetailsEdit => _localizationService["Details.Edit"];
        public string DetailsPreview => _localizationService["Details.Preview"];

        // Actions
        public string ActionRename => _localizationService["Action.Rename"];
        public string ActionCopy => _localizationService["Action.Copy"];
        public string ActionDelete => _localizationService["Action.Delete"];

        // TreeView
        public string TreeViewTitle => _localizationService["TreeView.Title"];

        // Tier Configuration
        public string TierConfigTitle => _localizationService["TierConfig.Title"];
        public string TierConfigHeader => _localizationService["TierConfig.Header"];
        public string TierConfigExistingTiers => _localizationService["TierConfig.ExistingTiers"];
        public string TierConfigDelete => _localizationService["TierConfig.Delete"];
        public string TierConfigReset => _localizationService["TierConfig.Reset"];
        public string TierConfigAddNew => _localizationService["TierConfig.AddNew"];
        public string TierConfigName => _localizationService["TierConfig.Name"];
        public string TierConfigNamePlaceholder => _localizationService["TierConfig.NamePlaceholder"];
        public string TierConfigColor => _localizationService["TierConfig.Color"];
        public string TierConfigColorPlaceholder => _localizationService["TierConfig.ColorPlaceholder"];
        public string TierConfigColorSelect => _localizationService["TierConfig.ColorSelect"];
        public string TierConfigColorExamples => _localizationService["TierConfig.ColorExamples"];
        public string TierConfigDescription => _localizationService["TierConfig.Description"];
        public string TierConfigDescriptionPlaceholder => _localizationService["TierConfig.DescriptionPlaceholder"];
        public string TierConfigPriority => _localizationService["TierConfig.Priority"];
        public string TierConfigPriorityHint => _localizationService["TierConfig.PriorityHint"];
        public string TierConfigAdd => _localizationService["TierConfig.Add"];
        public string TierConfigClose => _localizationService["TierConfig.Close"];

        // Dialog
        public string DialogNewDomainTitle => _localizationService["Dialog.NewDomain.Title"];
        public string DialogNewDomainHeader => _localizationService["Dialog.NewDomain.Header"];
        public string DialogNewDomainDescription => _localizationService["Dialog.NewDomain.Description"];
        public string DialogNewDomainDomainName => _localizationService["Dialog.NewDomain.DomainName"];
        public string DialogNewDomainDomainPlaceholder => _localizationService["Dialog.NewDomain.DomainPlaceholder"];
        public string DialogNewDomainEnableTiering => _localizationService["Dialog.NewDomain.EnableTiering"];
        public string DialogNewDomainTieringTooltip => _localizationService["Dialog.NewDomain.TieringTooltip"];
        public string DialogNewDomainStructurePreview => _localizationService["Dialog.NewDomain.StructurePreview"];
        public string DialogNewDomainCreate => _localizationService["Dialog.NewDomain.Create"];
        public string DialogNewDomainCancel => _localizationService["Dialog.NewDomain.Cancel"];

        // Menu Help
        public string MenuHelp => _localizationService["Menu.Help"];
        public string MenuHelpAbout => _localizationService["Menu.Help.About"];

        // Relations Window
        public string RelationsTitle => _localizationService["Relations.Title"];
        public string RelationsTabMemberships => _localizationService["Relations.TabMemberships"];
        public string RelationsTabUserGroup => _localizationService["Relations.TabUserGroup"];
        public string RelationsTabGroupGroup => _localizationService["Relations.TabGroupGroup"];
        public string RelationsTabGpoLinks => _localizationService["Relations.TabGpoLinks"];
        public string RelationsTabPsoLinks => _localizationService["Relations.TabPsoLinks"];
        public string RelationsColSource => _localizationService["Relations.ColSource"];
        public string RelationsColType => _localizationService["Relations.ColType"];
        public string RelationsColTargetGroup => _localizationService["Relations.ColTargetGroup"];
        public string RelationsColRelation => _localizationService["Relations.ColRelation"];
        public string RelationsColGpo => _localizationService["Relations.ColGpo"];
        public string RelationsColTargetOU => _localizationService["Relations.ColTargetOU"];
        public string RelationsColPso => _localizationService["Relations.ColPso"];
        public string RelationsColTargetPso => _localizationService["Relations.ColTargetPso"];
        public string RelationsColUser => _localizationService["Relations.ColUser"];
        public string RelationsColGroup => _localizationService["Relations.ColGroup"];
        public string RelationsColGroupMember => _localizationService["Relations.ColGroupMember"];
        public string RelationsColGroupParent => _localizationService["Relations.ColGroupParent"];
        public string RelationsLabelSource => _localizationService["Relations.LabelSource"];
        public string RelationsLabelTargetGroup => _localizationService["Relations.LabelTargetGroup"];
        public string RelationsLabelUserSource => _localizationService["Relations.LabelUserSource"];
        public string RelationsLabelGroupMember => _localizationService["Relations.LabelGroupMember"];
        public string RelationsLabelGroupParent => _localizationService["Relations.LabelGroupParent"];
        public string RelationsLabelGpo => _localizationService["Relations.LabelGpo"];
        public string RelationsLabelTargetOU => _localizationService["Relations.LabelTargetOU"];
        public string RelationsLabelPso => _localizationService["Relations.LabelPso"];
        public string RelationsLabelTargetPso => _localizationService["Relations.LabelTargetPso"];
        public string RelationsChooseObject => _localizationService["Relations.ChooseObject"];
        public string RelationsChooseUser => _localizationService["Relations.ChooseUser"];
        public string RelationsChooseGroup => _localizationService["Relations.ChooseGroup"];
        public string RelationsChooseGPO => _localizationService["Relations.ChooseGPO"];
        public string RelationsChooseOU => _localizationService["Relations.ChooseOU"];
        public string RelationsChoosePSO => _localizationService["Relations.ChoosePSO"];
        public string RelationsChooseTarget => _localizationService["Relations.ChooseTarget"];
        public string RelationsAdd => _localizationService["Relations.Add"];
        public string RelationsDelete => _localizationService["Relations.Delete"];
        public string RelationsClose => _localizationService["Relations.Close"];

        // Graph Window
        public string GraphTitle => _localizationService["Graph.Title"];
        public string GraphFitView => _localizationService["Graph.FitView"];
        public string GraphZoomIn => _localizationService["Graph.ZoomIn"];
        public string GraphZoomOut => _localizationService["Graph.ZoomOut"];
        public string GraphRelayout => _localizationService["Graph.Relayout"];
        public string GraphReload => _localizationService["Graph.Reload"];
        public string GraphFilters => _localizationService["Graph.Filters"];
        public string GraphFilterMemberOf => _localizationService["Graph.FilterMemberOf"];
        public string GraphFilterGpoLinks => _localizationService["Graph.FilterGpoLinks"];
        public string GraphFilterGpoInheritance => _localizationService["Graph.FilterGpoInheritance"];
        public string GraphFilterPso => _localizationService["Graph.FilterPso"];
        public string GraphFilterHierarchy => _localizationService["Graph.FilterHierarchy"];
        public string GraphFilterIsolated => _localizationService["Graph.FilterIsolated"];
        public string GraphFitViewTooltip => _localizationService["Graph.FitViewTooltip"];
        public string GraphRelayoutTooltip => _localizationService["Graph.RelayoutTooltip"];
        public string GraphReloadTooltip => _localizationService["Graph.ReloadTooltip"];
        public string GraphClose => _localizationService["Graph.Close"];

        // About
        public string AboutTitle => _localizationService["About.Title"];
        public string AboutAppName => _localizationService["About.AppName"];
        public string AboutFullName => _localizationService["About.FullName"];
        public string AboutVersion => _localizationService["About.Version"];
        public string AboutDescription => _localizationService["About.Description"];
        public string AboutFeatures => _localizationService["About.Features"];
        public string AboutFeature1 => _localizationService["About.Feature1"];
        public string AboutFeature2 => _localizationService["About.Feature2"];
        public string AboutFeature3 => _localizationService["About.Feature3"];
        public string AboutFeature4 => _localizationService["About.Feature4"];
        public string AboutFeature5 => _localizationService["About.Feature5"];
        public string AboutFeature6 => _localizationService["About.Feature6"];
        public string AboutCopyright => _localizationService["About.Copyright"];
        public string AboutClose => _localizationService["About.Close"];
    }
}
