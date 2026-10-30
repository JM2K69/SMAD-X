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

        // Tooltips toolbar
        public string TooltipAddOU => _localizationService["Tooltip.AddOU"];
        public string TooltipAddContainer => _localizationService["Tooltip.AddContainer"];
        public string TooltipAddUser => _localizationService["Tooltip.AddUser"];
        public string TooltipAddGroup => _localizationService["Tooltip.AddGroup"];
        public string TooltipAddComputer => _localizationService["Tooltip.AddComputer"];
        public string TooltipAddGMSA => _localizationService["Tooltip.AddGMSA"];
        public string TooltipAddPolicy => _localizationService["Tooltip.AddPolicy"];
        public string TooltipAddPSO => _localizationService["Tooltip.AddPSO"];
        public string TooltipDelete => _localizationService["Tooltip.Delete"];
        public string TooltipRelations => _localizationService["Tooltip.Relations"];
        public string TooltipGraph => _localizationService["Tooltip.Graph"];
        public string TooltipExportPNG => _localizationService["Tooltip.ExportPNG"];
        public string TooltipClearSearch => _localizationService["Tooltip.ClearSearch"];
        public string MenuViewManageRelations => _localizationService["Menu.View.ManageRelations"];
        public string MenuViewShowGraph => _localizationService["Menu.View.ShowGraph"];

        // Details
        public string DetailsTitle => _localizationService["Details.Title"];
        public string DetailsName => _localizationService["Details.Name"];
        public string DetailsType => _localizationService["Details.Type"];
        public string DetailsTier => _localizationService["Details.Tier"];
        public string DetailsSecurity => _localizationService["Details.Security"];
        public string DetailsPasswordPolicy => _localizationService["Details.PasswordPolicy"];
        public string DetailsPasswordPolicyHint => _localizationService["Details.PasswordPolicy.Hint"];
        public string PolicyPrecedence => _localizationService["Policy.Precedence"];
        public string PolicyMinLength => _localizationService["Policy.MinLength"];
        public string PolicyHistory => _localizationService["Policy.History"];
        public string PolicyComplexity => _localizationService["Policy.Complexity"];
        public string PolicyMaxAge => _localizationService["Policy.MaxAge"];
        public string PolicyMinAge => _localizationService["Policy.MinAge"];
        public string PolicyLockoutThreshold => _localizationService["Policy.LockoutThreshold"];
        public string PolicyLockoutDuration => _localizationService["Policy.LockoutDuration"];
        public string PolicyObservation => _localizationService["Policy.Observation"];
        public string DomainMachineAccountQuota => _localizationService["Domain.MachineAccountQuota"];
        public string DomainServerVersion => _localizationService["Domain.ServerVersion"];
        public string SecurityAccountDisabled => _localizationService["Security.AccountDisabled"];
        public string SecurityPasswordNotRequired => _localizationService["Security.PasswordNotRequired"];
        public string SecurityPasswordNeverExpires => _localizationService["Security.PasswordNeverExpires"];
        public string SecurityDontRequirePreauth => _localizationService["Security.DontRequirePreauth"];
        public string SecurityTrustedForDelegation => _localizationService["Security.TrustedForDelegation"];
        public string SecurityTrustedToAuthForDelegation => _localizationService["Security.TrustedToAuthForDelegation"];
        public string SecurityNotDelegated => _localizationService["Security.NotDelegated"];
        public string SecurityAdminCount => _localizationService["Security.AdminCount"];
        public string SecuritySpn => _localizationService["Security.Spn"];
        public string SecurityAllowedToDelegateTo => _localizationService["Security.AllowedToDelegateTo"];
        public string SecurityComputer => _localizationService["Security.Computer"];
        public string SecurityLaps => _localizationService["Security.Laps"];
        public string SecurityLapsExpiration => _localizationService["Security.LapsExpiration"];
        public string SecurityLocalAdmins => _localizationService["Security.LocalAdmins"];
        public string SecuritySessions => _localizationService["Security.Sessions"];
        public string SecurityGroupScope => _localizationService["Security.GroupScope"];
        public string SecurityGroupCategory => _localizationService["Security.GroupCategory"];
        public string SecurityOwner => _localizationService["Security.Owner"];
        public string SecurityPwdLastSet => _localizationService["Security.PwdLastSet"];
        public string MenuAnalyseAudit => _localizationService["Menu.Analyse.Audit"];
        public string MenuAnalyseAttackPaths => _localizationService["Menu.Analyse.AttackPaths"];
        public string AttackPathTitle => _localizationService["AttackPath.Title"];
        public string AttackPathIntro => _localizationService["AttackPath.Intro"];
        public string AttackPathShowTier0 => _localizationService["AttackPath.ShowTier0"];
        public string AttackPathSources => _localizationService["AttackPath.Sources"];
        public string AttackPathSteps => _localizationService["AttackPath.Steps"];
        public string AttackPathShowInGraph => _localizationService["AttackPath.ShowInGraph"];
        public string AttackPathRefresh => _localizationService["AttackPath.Refresh"];
        public string AttackPathNone => _localizationService["AttackPath.None"];
        public string MenuEditUndo => _localizationService["Menu.Edit.Undo"];
        public string MenuEditRedo => _localizationService["Menu.Edit.Redo"];
        public string MenuAnalyseComparison => _localizationService["Menu.Analyse.Comparison"];
        public string MenuAnalyseMarkBaseline => _localizationService["Menu.Analyse.MarkBaseline"];
        public string ComparisonTitle => _localizationService["Comparison.Title"];
        public string ComparisonScore => _localizationService["Comparison.Score"];
        public string ComparisonRefresh => _localizationService["Comparison.Refresh"];
        public string ComparisonFixed => _localizationService["Comparison.Fixed"];
        public string ComparisonNew => _localizationService["Comparison.New"];
        public string ComparisonChanges => _localizationService["Comparison.Changes"];
        public string MenuLabs => _localizationService["Menu.Labs"];
        public string MenuLabsOpen => _localizationService["Menu.Labs.Open"];
        public string MenuLabsBuiltIn => _localizationService["Menu.Labs.BuiltIn"];
        public string MenuLabsAttackPaths => _localizationService["Menu.Labs.AttackPaths"];
        public string MenuLabsShowCurrent => _localizationService["Menu.Labs.ShowCurrent"];
        public string MenuLabsSaveSubmission => _localizationService["Menu.Labs.SaveSubmission"];
        public string MenuLabsLeave => _localizationService["Menu.Labs.Leave"];
        public string LabSaveSubmission => _localizationService["Lab.SaveSubmission"];
        public string LabSubmissionNeedName => _localizationService["Lab.Submission.NeedName"];
        public string LabExamNotice => _localizationService["Lab.Exam.Notice"];
        public string LabGuard => _localizationService["Lab.Guard"];
        public string LabGuardTip => _localizationService["Lab.Guard.Tip"];
        public string LabObjectives => _localizationService["Lab.Objectives"];
        public string LabQuestions => _localizationService["Lab.Questions"];
        public string LabAnswerWatermark => _localizationService["Lab.Answer.Watermark"];
        public string LabStudentId => _localizationService["Lab.StudentId"];
        public string LabStudentIdTip => _localizationService["Lab.StudentId.Tip"];
        public string LabHint => _localizationService["Lab.Hint"];
        public string LabCheck => _localizationService["Lab.Check"];
        public string LabRestart => _localizationService["Lab.Restart"];
        public string LabExportResult => _localizationService["Lab.ExportResult"];
        public string LabStudentName => _localizationService["Lab.StudentName"];
        public string LabCompleted => _localizationService["Lab.Completed"];
        public string DelegationAddLabel => _localizationService["Delegation.AddLabel"];
        public string DelegationTrustee => _localizationService["Delegation.Trustee"];
        public string DelegationAdd => _localizationService["Delegation.Add"];
        public string DelegationRemove => _localizationService["Delegation.Remove"];
        public string ErrorTitle => _localizationService["Error.Title"];
        public string ErrorIntro => _localizationService["Error.Intro"];
        public string ErrorDetails => _localizationService["Error.Details"];
        public string ErrorCopy => _localizationService["Error.Copy"];
        public string ErrorOpenLog => _localizationService["Error.OpenLog"];
        public string ErrorClose => _localizationService["Error.Close"];
        public string AuditWindowTitle => _localizationService["Audit.Window.Title"];
        public string AuditRun => _localizationService["Audit.Run"];
        public string AuditScore => _localizationService["Audit.Score"];
        public string AuditAnssiLevel => _localizationService["Audit.AnssiLevel"];
        public string AuditAnssiLevelNote => _localizationService["Audit.AnssiLevel.Note"];
        public string AuditColumnSeverity => _localizationService["Audit.Column.Severity"];
        public string AuditColumnRule => _localizationService["Audit.Column.Rule"];
        public string AuditColumnObject => _localizationService["Audit.Column.Object"];
        public string AuditColumnDetail => _localizationService["Audit.Column.Detail"];
        public string AuditReferences => _localizationService["Audit.References"];
        public string AuditSelectInTree => _localizationService["Audit.SelectInTree"];
        public string AuditSeverityCritical => _localizationService["Audit.Severity.Critical"];
        public string AuditSeverityHigh => _localizationService["Audit.Severity.High"];
        public string AuditSeverityMedium => _localizationService["Audit.Severity.Medium"];
        public string AuditSeverityLow => _localizationService["Audit.Severity.Low"];
        public string SecurityLastLogon => _localizationService["Security.LastLogon"];
        public string DetailsDN => _localizationService["Details.DN"];
        public string DetailsDescription => _localizationService["Details.Description"];
        public string DetailsActions => _localizationService["Details.Actions"];
        public string DetailsEdit => _localizationService["Details.Edit"];
        public string DetailsPreview => _localizationService["Details.Preview"];
        public string DetailsMemberOf => _localizationService["Details.MemberOf"];
        public string DetailsLinkedGPOs => _localizationService["Details.LinkedGPOs"];
        public string DetailsPSOAppliesTo => _localizationService["Details.PSOAppliesTo"];

        // Actions
        public string ActionRename => _localizationService["Action.Rename"];
        public string ActionCopy => _localizationService["Action.Copy"];
        public string ActionDelete => _localizationService["Action.Delete"];
        public string ActionManage => _localizationService["Action.Manage"];

        // TreeView
        public string TreeViewTitle        => _localizationService["TreeView.Title"];
        public string SearchPlaceholder    => _localizationService["TreeView.SearchPlaceholder"];

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
        public string DialogNewDomainServerVersion => _localizationService["Dialog.NewDomain.ServerVersion"];
        public string DialogNewDomainServerVersionTooltip => _localizationService["Dialog.NewDomain.ServerVersionTooltip"];
        public string DialogNewDomainStructurePreview => _localizationService["Dialog.NewDomain.StructurePreview"];
        public string DialogNewDomainCreate => _localizationService["Dialog.NewDomain.Create"];
        public string DialogNewDomainCancel => _localizationService["Dialog.NewDomain.Cancel"];

        // Menu Help
        public string MenuHelp => _localizationService["Menu.Help"];
        public string MenuHelpAbout => _localizationService["Menu.Help.About"];
        public string MenuHelpShortcuts => _localizationService["Menu.Help.Shortcuts"];

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
        public string GraphFilterDelegations => _localizationService["Graph.FilterDelegations"];
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
        public string AboutVersion => string.Format(_localizationService["About.Version"], AppInfo.Version);
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

        // Context Menu
        public string ContextMenuAddOU             => _localizationService["ContextMenu.AddOU"];
        public string ContextMenuAddContainer      => _localizationService["ContextMenu.AddContainer"];
        public string ContextMenuAddUser           => _localizationService["ContextMenu.AddUser"];
        public string ContextMenuAddGroup          => _localizationService["ContextMenu.AddGroup"];
        public string ContextMenuAddComputer       => _localizationService["ContextMenu.AddComputer"];
        public string ContextMenuAddGMSA           => _localizationService["ContextMenu.AddGMSA"];
        public string ContextMenuAddPolicy         => _localizationService["ContextMenu.AddPolicy"];
        public string ContextMenuAddPSO            => _localizationService["ContextMenu.AddPSO"];
        public string ContextMenuRename            => _localizationService["ContextMenu.Rename"];
        public string ContextMenuCopy              => _localizationService["ContextMenu.Copy"];
        public string ContextMenuPaste             => _localizationService["ContextMenu.Paste"];
        public string ContextMenuDelete            => _localizationService["ContextMenu.Delete"];
        public string ContextMenuManageRelations   => _localizationService["ContextMenu.ManageRelations"];
        public string ContextMenuAddToGroup         => _localizationService["ContextMenu.AddToGroup"];
        public string ContextMenuAddMember           => _localizationService["ContextMenu.AddMember"];
        public string ContextMenuAddGroupToGroup      => _localizationService["ContextMenu.AddGroupToGroup"];

        // AD Sites
        public string MenuSites              => _localizationService["Menu.Sites"];
        public string MenuSitesView          => _localizationService["Menu.Sites.View"];
        public string SitesWindowTitle       => _localizationService["Sites.Window.Title"];
        public string SitesTabTable          => _localizationService["Sites.Tab.Table"];
        public string SitesTabGraph          => _localizationService["Sites.Tab.Graph"];
        public string SitesColumnName        => _localizationService["Sites.Column.Name"];
        public string SitesColumnDescription => _localizationService["Sites.Column.Description"];
        public string SitesColumnLocation    => _localizationService["Sites.Column.Location"];
        public string SitesColumnSubnets     => _localizationService["Sites.Column.Subnets"];
        public string SitesColumnDCs         => _localizationService["Sites.Column.DCs"];
        public string SitesColumnCost        => _localizationService["Sites.Column.Cost"];
        public string SitesColumnInterval    => _localizationService["Sites.Column.Interval"];
        public string SitesColumnTransport   => _localizationService["Sites.Column.Transport"];
        public string SitesColumnLinkedSites => _localizationService["Sites.Column.LinkedSites"];
        public string SitesColumnGPOs        => _localizationService["Sites.Column.GPOs"];
        public string SitesColumnLinkName    => _localizationService["Sites.Column.LinkName"];
        public string SitesColumnLinkSites   => _localizationService["Sites.Column.LinkSites"];
        public string SitesNoData            => _localizationService["Sites.NoData"];
        public string SitesNoSites           => _localizationService["Sites.NoSites"];
        public string SitesAdd               => _localizationService["Sites.Add"];
        public string SitesSectionSites      => _localizationService["Sites.Section.Sites"];
        public string SitesSectionLinks      => _localizationService["Sites.Section.Links"];
        public string SitesLabelSites        => _localizationService["Sites.Label.Sites"];
        public string SitesLabelLinks        => _localizationService["Sites.Label.Links"];
        public string SitesFilterPlaceholder => _localizationService["Sites.Filter.Placeholder"];
        public string SitesDetailSelected    => _localizationService["Sites.Detail.Selected"];
        public string SitesDetailSubnets     => _localizationService["Sites.Detail.Subnets"];
        public string SitesDetailDCs         => _localizationService["Sites.Detail.DCs"];
        public string SitesDetailGPOs        => _localizationService["Sites.Detail.GPOs"];

        // Report export
        public string MenuFileExportReport      => _localizationService["Menu.File.ExportReport"];
        public string MenuFileImportDescription => _localizationService["Menu.File.ImportDescription"];
        public string ReportDialogTitle         => _localizationService["Report.Dialog.Title"];
        public string ReportFormatMarkdown      => _localizationService["Report.Format.Markdown"];
        public string ReportFormatDocx          => _localizationService["Report.Format.Docx"];
        public string ReportFormatPdf           => _localizationService["Report.Format.Pdf"];
        public string ReportFormatLabel         => _localizationService["Report.Format.Label"];
        public string ReportScopeLabel          => _localizationService["Report.Scope.Label"];
        public string ReportScopeCombined       => _localizationService["Report.Scope.Combined"];
        public string ReportScopeIndividual     => _localizationService["Report.Scope.Individual"];
        public string ReportScopeObjectLabel    => _localizationService["Report.Scope.Object.Label"];
        public string ReportScopeTypeFilter     => _localizationService["Report.Scope.TypeFilter"];
        public string ReportScopeTypeAll        => _localizationService["Report.Scope.TypeAll"];
        public string ReportScopeSelectAll      => _localizationService["Report.Scope.SelectAll"];
        public string ReportScopeSelectNone     => _localizationService["Report.Scope.SelectNone"];
        public string ReportButtonExport        => _localizationService["Report.Button.Export"];
        public string ReportButtonCancel        => _localizationService["Report.Button.Cancel"];
        public string ReportImportTitle         => _localizationService["Report.Import.Title"];
        public string ReportImportSuccess       => _localizationService["Report.Import.Success"];
        public string ReportImportError         => _localizationService["Report.Import.Error"];
        public string ReportImportNoSelection   => _localizationService["Report.Import.NoSelection"];
        public string ReportThemeLabel          => _localizationService["Report.Theme.Label"];
        public string ReportThemePlain          => _localizationService["Report.Theme.Plain"];
        public string ReportThemeWordLike       => _localizationService["Report.Theme.WordLike"];
        public string ReportThemeTechnicalDocument => _localizationService["Report.Theme.TechnicalDocument"];
        public string ReportThemeGitHubLike     => _localizationService["Report.Theme.GitHubLike"];
        public string ReportThemeCompact        => _localizationService["Report.Theme.Compact"];
        public string ReportThemeReport         => _localizationService["Report.Theme.Report"];
        public string ReportFormatMarkdownAlways => _localizationService["Report.Format.MarkdownAlways"];

        /// <summary>Culture courante : une MultiBinding qui la lit est recalculée au changement de langue.</summary>
        public string CurrentCulture => _localizationService.CurrentCulture.Name;

        // Menu Analyse, barre d'outils, badges et panneau des sites de la fenêtre principale
        public string MenuAnalyse            => _localizationService["Menu.Analyse"];
        public string MenuAnalyseTimeline    => _localizationService["Menu.Analyse.Timeline"];
        public string MenuAnalyseDelegations => _localizationService["Menu.Analyse.Delegations"];
        public string ToolbarExportPNG       => _localizationService["Toolbar.ExportPNG"];
        public string BadgeGpo               => _localizationService["Badge.Gpo"];
        public string BadgeDelegations       => _localizationService["Badge.Delegations"];
        public string BadgeAudit             => _localizationService["Badge.Audit"];
        public string SitesPanelTitle        => _localizationService["Sites.Panel.Title"];

        // À propos (suite)
        public string AboutFeature7 => _localizationService["About.Feature7"];
        public string AboutFeature8 => _localizationService["About.Feature8"];

        // Exercices (suite)
        public string LabPointsUnit => _localizationService["Lab.PointsUnit"];

        // Graphe et relations (suite)
        public string GraphFilterGroupNesting    => _localizationService["Graph.FilterGroupNesting"];
        public string RelationsLabelUserComputer => _localizationService["Relations.LabelUserComputer"];
        public string RelationsChooseUserComputer => _localizationService["Relations.ChooseUserComputer"];
        public string RelationsFilter            => _localizationService["Relations.Filter"];

        // Nouveau domaine (aperçu de la structure créée)
        public string DialogNewDomainPreviewTree => _localizationService["Dialog.NewDomain.PreviewTree"];

        // Sélecteur de couleur
        public string ColorPickerTitle   => _localizationService["ColorPicker.Title"];
        public string ColorPickerRed     => _localizationService["ColorPicker.Red"];
        public string ColorPickerGreen   => _localizationService["ColorPicker.Green"];
        public string ColorPickerBlue    => _localizationService["ColorPicker.Blue"];
        public string ColorPickerHex     => _localizationService["ColorPicker.Hex"];
        public string ColorPickerPresets => _localizationService["ColorPicker.Presets"];
        public string ColorPickerOk      => _localizationService["ColorPicker.Ok"];
        public string ColorPickerCancel  => _localizationService["ColorPicker.Cancel"];

        // Délégations
        public string DelegationsWindowTitle     => _localizationService["Delegations.Window.Title"];
        public string DelegationsTreeTitle       => _localizationService["Delegations.Tree.Title"];
        public string DelegationsShowAll         => _localizationService["Delegations.ShowAll"];
        public string DelegationsFilterTrustee   => _localizationService["Delegations.Filter.Trustee"];
        public string DelegationsFilterTargetDN  => _localizationService["Delegations.Filter.TargetDN"];
        public string DelegationsFilterCategory  => _localizationService["Delegations.Filter.Category"];
        public string DelegationsHideInherited   => _localizationService["Delegations.HideInherited"];
        public string DelegationsApply           => _localizationService["Delegations.Apply"];
        public string DelegationsExportCsv       => _localizationService["Delegations.ExportCsv"];
        public string DelegationsColumnTrustee   => _localizationService["Delegations.Column.Trustee"];
        public string DelegationsColumnType      => _localizationService["Delegations.Column.Type"];
        public string DelegationsColumnCategory  => _localizationService["Delegations.Column.Category"];
        public string DelegationsColumnRight     => _localizationService["Delegations.Column.Right"];
        public string DelegationsColumnTargetDN  => _localizationService["Delegations.Column.TargetDN"];
        public string DelegationsColumnInherited => _localizationService["Delegations.Column.Inherited"];
        public string DelegationsColumnTier      => _localizationService["Delegations.Column.Tier"];
        public string DelegationCategoryPasswordReset      => _localizationService["Delegation.Category.PasswordReset"];
        public string DelegationCategoryComputerManagement => _localizationService["Delegation.Category.ComputerManagement"];
        public string DelegationCategoryAccountUnlock      => _localizationService["Delegation.Category.AccountUnlock"];
        public string DelegationCategoryAttributeWrite     => _localizationService["Delegation.Category.AttributeWrite"];
        public string DelegationCategoryFullControl        => _localizationService["Delegation.Category.FullControl"];
        public string DelegationCategoryOther              => _localizationService["Delegation.Category.Other"];
        public string DelegationCardSensitive              => _localizationService["Delegation.Card.Sensitive"];

        // Évolution du domaine (comparaison de deux fichiers)
        public string TimelineWindowTitle       => _localizationService["Timeline.Window.Title"];
        public string TimelineFileA             => _localizationService["Timeline.FileA"];
        public string TimelineFileB             => _localizationService["Timeline.FileB"];
        public string TimelineOpen              => _localizationService["Timeline.Open"];
        public string TimelineCompare           => _localizationService["Timeline.Compare"];
        public string TimelineAdded             => _localizationService["Timeline.Added"];
        public string TimelineRemoved           => _localizationService["Timeline.Removed"];
        public string TimelineModified          => _localizationService["Timeline.Modified"];
        public string TimelineExportCsv         => _localizationService["Timeline.ExportCsv"];
        public string TimelineFilterText        => _localizationService["Timeline.Filter.Text"];
        public string TimelineFilterChangeType  => _localizationService["Timeline.Filter.ChangeType"];
        public string TimelineFilterCategory    => _localizationService["Timeline.Filter.Category"];
        public string TimelineApply             => _localizationService["Timeline.Apply"];
        public string TimelineColumnChange      => _localizationService["Timeline.Column.Change"];
        public string TimelineColumnCategory    => _localizationService["Timeline.Column.Category"];
        public string TimelineColumnObject      => _localizationService["Timeline.Column.Object"];
        public string TimelineColumnType        => _localizationService["Timeline.Column.Type"];
        public string TimelineColumnDN          => _localizationService["Timeline.Column.DN"];
        public string TimelineColumnDetails     => _localizationService["Timeline.Column.Details"];
    }
}
